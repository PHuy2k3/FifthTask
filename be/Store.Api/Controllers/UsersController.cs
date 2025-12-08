using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Store.Data;


namespace Store.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UsersController : ControllerBase
    {
        private readonly StoreDbContext _db;
        private readonly ILogger<UsersController> _logger;

        public UsersController(StoreDbContext db, ILogger<UsersController> logger)
        {
            _db = db;
            _logger = logger;
        }

        // ----------------------------------------------------------------
        // ADMIN: list users (paged). Admin only.
        // ----------------------------------------------------------------
        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetAll(int page = 1, int pageSize = 50)
        {
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 200);

            var q = _db.Users.AsNoTracking().OrderBy(u => u.Username);
            var total = await q.CountAsync();
            var items = await q.Skip((page - 1) * pageSize).Take(pageSize)
                .Select(u => new {
                    Username = u.Username,
                    Email = u.Email,
                    Role = u.Role,
                    IsActive = u.IsActive,
                    CreatedAt = u.CreatedAt
                }).ToListAsync();

            return Ok(new { total, page, pageSize, items });
        }

        // ----------------------------------------------------------------
        // Any authenticated user: get profile for currently logged-in user
        // GET /api/users/me
        // ----------------------------------------------------------------
        [HttpGet("me")]
        [Authorize]
        public async Task<IActionResult> Me()
        {
            try
            {
                // Lấy claim 'sub' ưu tiên, fallback ClaimTypes.NameIdentifier
                var sub = User.FindFirst("sub")?.Value
                          ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

                if (string.IsNullOrEmpty(sub))
                    return Unauthorized(new { error = "Missing subject claim" });

                // Nếu sub có thể parse thành int -> tìm theo Id
                if (int.TryParse(sub, out var id))
                {
                    var byId = await _db.Users.AsNoTracking()
                        .Where(x => x.Id == id)
                        .Select(x => new {
                            Username = x.Username,
                            Email = x.Email,
                            Role = x.Role,
                            IsActive = x.IsActive,
                            CreatedAt = x.CreatedAt
                        })
                        .FirstOrDefaultAsync();

                    if (byId != null) return Ok(byId);
                }

                // Nếu không tìm theo Id (hoặc sub không phải số) -> thử username
                var byUsername = await _db.Users.AsNoTracking()
                    .Where(x => x.Username == sub)
                    .Select(x => new {
                        Username = x.Username,
                        Email = x.Email,
                        Role = x.Role,
                        IsActive = x.IsActive,
                        CreatedAt = x.CreatedAt
                    })
                    .FirstOrDefaultAsync();

                if (byUsername != null) return Ok(byUsername);

                // Fallback: thử email
                var byEmail = await _db.Users.AsNoTracking()
                    .Where(x => x.Email == sub)
                    .Select(x => new {
                        Username = x.Username,
                        Email = x.Email,
                        Role = x.Role,
                        IsActive = x.IsActive,
                        CreatedAt = x.CreatedAt
                    })
                    .FirstOrDefaultAsync();

                if (byEmail != null) return Ok(byEmail);

                // nếu không tìm thấy --> NotFound
                return NotFound(new { error = "User not found for subject claim" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Me() failed");
                return Problem(detail: "Internal server error", statusCode: 500);
            }
        }

        // PUT api/users/me
        [HttpPut("me")]
        [Authorize]
        public async Task<IActionResult> UpdateMe([FromBody] UpdateUserDto dto)
        {
            try
            {
                // Lấy chủ thể từ token
                var sub = User.FindFirst("sub")?.Value
                          ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

                if (string.IsNullOrEmpty(sub)) return Unauthorized(new { error = "Missing subject claim" });

                // Tìm user: nếu sub có thể parse thành Id thì tìm theo Id, else tìm theo username/email
                Store.Data.Model.User? user = null;

                if (int.TryParse(sub, out var id))
                {
                    user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id);
                }
                if (user == null)
                {
                    user = await _db.Users.FirstOrDefaultAsync(u => u.Username == sub || u.Email == sub);
                }

                if (user == null) return NotFound(new { error = "User not found" });

                // Chỉ update những trường an toàn
                if (!string.IsNullOrWhiteSpace(dto.Email)) user.Email = dto.Email;
                // Role và IsActive chỉ admin mới đổi được -> kiểm tra role
                var callerIsAdmin = User.IsInRole("Admin");
                if (callerIsAdmin && !string.IsNullOrWhiteSpace(dto.Role)) user.Role = dto.Role;
                if (callerIsAdmin && dto.IsActive.HasValue) user.IsActive = dto.IsActive.Value;

                await _db.SaveChangesAsync();
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "UpdateMe() failed");
                return Problem(detail: "Internal server error", statusCode: 500);
            }
        }

        // ----------------------------------------------------------------
        // Get user by username (Admin OR the user themself)
        // GET /api/users/{username}
        // ----------------------------------------------------------------
        [HttpGet("{username}")]
        public async Task<IActionResult> GetByUsername(string username)
        {
            try
            {
                // caller identification
                var callerSub = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                                ?? User.FindFirst("sub")?.Value;

                var callerIsAdmin = User.IsInRole("Admin");
                var isOwner = !string.IsNullOrEmpty(callerSub) &&
                              string.Equals(callerSub, username, StringComparison.OrdinalIgnoreCase);

                if (!callerIsAdmin && !isOwner) return Forbid();

                var user = await _db.Users.AsNoTracking()
                    .Where(u => u.Username == username)
                    .Select(u => new {
                        Username = u.Username,
                        Email = u.Email,
                        Role = u.Role,
                        IsActive = u.IsActive,
                        CreatedAt = u.CreatedAt
                    })
                    .FirstOrDefaultAsync();

                if (user == null) return NotFound();
                return Ok(user);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetByUsername({Username}) failed", username);
                return Problem(detail: "Internal server error", statusCode: 500);
            }
        }

        // ----------------------------------------------------------------
        // Update user by username (owner or admin)
        // PUT /api/users/{username}
        // ----------------------------------------------------------------
        [HttpPut("{username}")]
        public async Task<IActionResult> UpdateByUsername(string username, [FromBody] UpdateUserDto dto)
        {
            try
            {
                var callerSub = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                                ?? User.FindFirst("sub")?.Value;
                var callerIsAdmin = User.IsInRole("Admin");
                var isOwner = !string.IsNullOrEmpty(callerSub) &&
                              string.Equals(callerSub, username, StringComparison.OrdinalIgnoreCase);

                if (!callerIsAdmin && !isOwner) return Forbid();

                var user = await _db.Users.FirstOrDefaultAsync(u => u.Username == username);
                if (user == null) return NotFound();

                // safe updates
                if (!string.IsNullOrEmpty(dto.Email)) user.Email = dto.Email;
                if (callerIsAdmin && !string.IsNullOrEmpty(dto.Role)) user.Role = dto.Role;
                if (callerIsAdmin && dto.IsActive.HasValue) user.IsActive = dto.IsActive.Value;

                // password change should be handled via dedicated endpoint with hashing
                await _db.SaveChangesAsync();
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "UpdateByUsername({Username}) failed", username);
                return Problem(detail: "Internal server error", statusCode: 500);
            }
        }

        // ----------------------------------------------------------------
        // Delete user (admin only) by username
        // DELETE /api/users/{username}
        // ----------------------------------------------------------------
        [HttpDelete("{username}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteByUsername(string username)
        {
            try
            {
                var user = await _db.Users.FirstOrDefaultAsync(u => u.Username == username);
                if (user == null) return NotFound();

                _db.Users.Remove(user);
                await _db.SaveChangesAsync();
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DeleteByUsername({Username}) failed", username);
                return Problem(detail: "Internal server error", statusCode: 500);
            }
        }
    }

    // DTO used by Update action (keeps shape small & safe)
    public class UpdateUserDto
    {
        public string? Email { get; set; }
        // Admin-only fields:
        public string? Role { get; set; }
        public bool? IsActive { get; set; }
    }
}
