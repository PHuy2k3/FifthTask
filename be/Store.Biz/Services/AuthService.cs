using Microsoft.Extensions.Configuration;
using Store.Biz.Interfaces;
using Store.Biz.Model;
using Store.Data.Interfaces;
using Store.Data.Model;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Store.Biz.Services;

public class AuthService : IAuthService
{
    private readonly IRepository<User> _userRepo;
    private readonly IConfiguration _cfg;

    public AuthService(IRepository<User> userRepo, IConfiguration cfg)
    {
        _userRepo = userRepo;
        _cfg = cfg;
    }

    public async Task<AuthResultDto> RegisterAsync(RegisterDto dto)
    {
        var exists = _userRepo.Query().Any(u => u.Username == dto.Username || u.Email == dto.Email);
        if (exists) throw new ArgumentException("Username or email already exists.");

        var user = new User
        {
            Username = dto.Username,
            Email = dto.Email,
            PasswordHash = PasswordHasher.Hash(dto.Password),
            Role = "User",
            CreatedAt = DateTime.UtcNow
        };

        await _userRepo.AddAsync(user);
        await _userRepo.SaveChangesAsync();

        var token = BuildToken(user);
        return new AuthResultDto { Token = token, Username = user.Username, Role = user.Role };
    }

    public async Task<AuthResultDto?> LoginAsync(LoginDto dto)
    {
        var user = _userRepo.Query().FirstOrDefault(u => u.Username == dto.Username);
        if (user == null) return null;
        if (!PasswordHasher.Verify(dto.Password, user.PasswordHash)) return null;

        var token = BuildToken(user);
        return new AuthResultDto { Token = token, Username = user.Username, Role = user.Role };
    }

    public async Task<List<UserDto>> GetAllUsersAsync()
    {
        var list = await _userRepo.ListAsync();
        return list.Select(u => new UserDto
        {
            Id = GetUserIdValue(u),
            Username = u.Username,
            Email = u.Email,
            Role = u.Role
        }).ToList();
    }

    public async Task<bool> PromoteToAdminAsync(int userId)
    {
        var user = await _userRepo.GetByIdAsync(userId);
        if (user == null) return false;
        user.Role = "Admin";
        _userRepo.Update(user);
        await _userRepo.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteUserAsync(int userId)
    {
        var user = await _userRepo.GetByIdAsync(userId);
        if (user == null) return false;
        _userRepo.Remove(user);
        await _userRepo.SaveChangesAsync();
        return true;
    }

    // Helper: try to get numeric Id from User entity (supports Id or UserId)
    private int GetUserIdValue(User u)
    {
        // If your User model uses 'Id' (int), return that.
        // If it uses 'UserId' or GUID, adjust accordingly.
        // Try common property names using reflection to avoid compile error when type differs.
        var t = u.GetType();
        var prop = t.GetProperty("Id") ?? t.GetProperty("UserId") ?? t.GetProperty("UserID");
        if (prop != null)
        {
            var val = prop.GetValue(u);
            if (val is int i) return i;
            if (val is long l) return (int)l;
            if (val is Guid g) return Math.Abs(g.GetHashCode());
            if (int.TryParse(val?.ToString(), out var parsed)) return parsed;
        }
        // Fallback: 0 (shouldn't happen in normal case)
        return 0;
    }

    // -----------------------
    // Manual HS256 JWT builder
    // -----------------------
    private string Base64UrlEncode(byte[] input)
    {
        var s = Convert.ToBase64String(input);
        return s.TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private string BuildToken(User user)
    {
        var key = _cfg["Jwt:Key"];
        if (string.IsNullOrEmpty(key)) throw new Exception("Jwt:Key missing");

        var header = JsonSerializer.Serialize(new { alg = "HS256", typ = "JWT" });
        var exp = DateTimeOffset.UtcNow.AddHours(8).ToUnixTimeSeconds();

        // Use Id property if available, else attempt fallback
        var subject = GetUserIdValue(user).ToString();

        var payloadObj = new Dictionary<string, object>
        {
            ["sub"] = subject,
            ["name"] = user.Username,
            ["role"] = user.Role ?? "User",
            ["exp"] = exp
        };
        var payload = JsonSerializer.Serialize(payloadObj);

        var headerEnc = Base64UrlEncode(Encoding.UTF8.GetBytes(header));
        var payloadEnc = Base64UrlEncode(Encoding.UTF8.GetBytes(payload));
        var unsigned = $"{headerEnc}.{payloadEnc}";

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(key));
        var sig = hmac.ComputeHash(Encoding.UTF8.GetBytes(unsigned));
        var sigEnc = Base64UrlEncode(sig);

        return $"{unsigned}.{sigEnc}";
    }
}
