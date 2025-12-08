using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Store.Data;
using Store.Data.Model;
using System.Security.Claims;

namespace Store.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class NotificationsController : ControllerBase
    {
        private readonly StoreDbContext _db;

        public NotificationsController(StoreDbContext db)
        {
            _db = db;
        }

        // GET /api/notifications -> all notifications for current user (most recent first)
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> GetMyNotifications(int page = 1, int pageSize = 50)
        {
            var sub = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
            int? userId = null;
            if (int.TryParse(sub, out var u)) userId = u;

            // include global notifications (UserId IS NULL) + those for this user
            var q = _db.UserNotifications
                .AsNoTracking()
                .Where(n => n.UserId == null || (userId != null && n.UserId == userId))
                .OrderByDescending(n => n.CreatedAt);

            var total = await q.CountAsync();
            var items = await q.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

            return Ok(new { total, page, pageSize, items });
        }

        // PUT /api/notifications/{id}/read -> mark read
        [HttpPut("{id:int}/read")]
        [Authorize]
        public async Task<IActionResult> MarkRead(int id)
        {
            var sub = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
            int? userId = null;
            if (int.TryParse(sub, out var u)) userId = u;

            var n = await _db.UserNotifications.FindAsync(id);
            if (n == null) return NotFound();

            // only the user (or admin) can mark their notifications; global notification marking allowed if user is recipient
            if (n.UserId != null && userId != n.UserId && !User.IsInRole("Admin"))
                return Forbid();

            n.IsRead = true;
            await _db.SaveChangesAsync();
            return NoContent();
        }

        // (Optional) POST /api/notifications/markallread
        [HttpPut("markallread")]
        [Authorize]
        public async Task<IActionResult> MarkAllRead()
        {
            var sub = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
            if (!int.TryParse(sub, out var userId)) return Unauthorized();

            var list = await _db.UserNotifications
                        .Where(n => (n.UserId == null || n.UserId == userId) && !n.IsRead)
                        .ToListAsync();
            foreach (var n in list) n.IsRead = true;
            await _db.SaveChangesAsync();
            return NoContent();
        }
    }
}
