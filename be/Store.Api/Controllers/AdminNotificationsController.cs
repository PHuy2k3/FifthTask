// be/Store.Api/Controllers/AdminNotificationsController.cs
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Store.Data;

namespace Store.Api.Controllers
{
    [ApiController]
    [Route("api/admin/notifications")]
    [Authorize(Roles = "Admin")]
    public class AdminNotificationsController : ControllerBase
    {
        private readonly StoreDbContext _db;

        public AdminNotificationsController(StoreDbContext db)
        {
            _db = db;
        }

        // GET /api/admin/notifications
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var items = await _db.AdminNotifications
                .OrderByDescending(n => n.CreatedAt)
                .Take(100)
                .ToListAsync();

            return Ok(items);
        }

        // POST /api/admin/notifications/{id}/read
        [HttpPost("{id}/read")]
        public async Task<IActionResult> MarkRead(int id)
        {
            var n = await _db.AdminNotifications.FindAsync(id);
            if (n == null) return NotFound();

            n.IsRead = true;
            await _db.SaveChangesAsync();
            return Ok();
        }
    }
}
