using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Store.Biz.Interfaces;
using Store.Biz.Model;
using Store.Data.Interfaces;
using Store.Data.Model;
using System.Linq;

namespace Store.Api.Controllers
{
    [ApiController]
    [Route("api/seed")]
    public class AdminSeedController : ControllerBase
    {
        private readonly IAuthService _auth;
        private readonly IRepository<User> _userRepo;
        private readonly ILogger<AdminSeedController> _logger;

        public AdminSeedController(IAuthService auth, IRepository<User> userRepo, ILogger<AdminSeedController> logger)
        {
            _auth = auth;
            _userRepo = userRepo;
            _logger = logger;
        }

        // POST /api/seed/admin
        [HttpPost("admin")]
        public async Task<IActionResult> SeedAdmin([FromBody] SeedAdminDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Username) || string.IsNullOrWhiteSpace(dto.Password))
                return BadRequest(new { error = "username and password required" });

            try
            {
                // Tạo user bằng RegisterAsync để hash password đúng chuẩn
                var reg = new RegisterDto
                {
                    Username = dto.Username,
                    Password = dto.Password,
                    Email = dto.Email ?? ""
                };

                await _auth.RegisterAsync(reg);

                // Lấy toàn bộ list user từ repository
                var allUsers = await _userRepo.ListAsync();
                var user = allUsers.FirstOrDefault(u => u.Username == dto.Username);

                if (user == null)
                    return Problem(detail: "User created but not found", statusCode: 500);

                // Set role admin
                user.Role = "Admin";

                _userRepo.Update(user);
                await _userRepo.SaveChangesAsync();

                return Created($"/api/users/{user.Id}",
                    new { id = user.Id, username = user.Username, role = user.Role });
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "SeedAdmin failed");
                return BadRequest(new { error = ex.Message });
            }
        }
    }

    public class SeedAdminDto
    {
        public string Username { get; set; } = "";
        public string Password { get; set; } = "";
        public string? Email { get; set; }
    }
}
