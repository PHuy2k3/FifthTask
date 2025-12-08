// be/Store.Api/Controllers/CartController.cs
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Store.Data;
using Store.Data.Model;

namespace Store.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CartController : ControllerBase
    {
        private readonly StoreDbContext _db;
        public CartController(StoreDbContext db) => _db = db;

        // GET /api/cart -> get current user's cart
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> Get()
        {
            var sub = User.FindFirst("sub")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(sub, out var userId)) return Unauthorized();

            var cart = await _db.Carts
                .Include(c => c.Items)
                .Where(c => c.UserId == userId)
                .Select(c => new {
                    cartId = c.CartId,
                    items = c.Items.Select(i => new { i.ProductId, i.Quantity, i.UnitPrice })
                })
                .FirstOrDefaultAsync();

            if (cart == null) return Ok(new { items = new object[0] });
            return Ok(cart);
        }

        // POST /api/cart -> replace cart for current user (replace whole cart)
        [HttpPost]
        [Authorize]
        public async Task<IActionResult> Replace([FromBody] ReplaceCartDto dto)
        {
            var sub = User.FindFirst("sub")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(sub, out var userId)) return Unauthorized();

            // Remove existing cart if exists
            var existing = await _db.Carts.Include(c => c.Items).FirstOrDefaultAsync(c => c.UserId == userId);
            if (existing != null)
            {
                _db.CartItems.RemoveRange(existing.Items);
                _db.Carts.Remove(existing);
            }

            var cart = new Cart { UserId = userId };
            _db.Carts.Add(cart);
            await _db.SaveChangesAsync(); // ensure CartId assigned

            if (dto.Items != null && dto.Items.Any())
            {
                var items = dto.Items.Select(i => new CartItem
                {
                    CartId = cart.CartId,
                    ProductId = i.ProductId,
                    Quantity = i.Quantity,
                    UnitPrice = i.UnitPrice
                }).ToList();

                _db.CartItems.AddRange(items);
                await _db.SaveChangesAsync();
            }

            return NoContent();
        }

        // DELETE /api/cart -> clear current user's cart
        [HttpDelete]
        [Authorize]
        public async Task<IActionResult> Clear()
        {
            var sub = User.FindFirst("sub")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(sub, out var userId)) return Unauthorized();

            var cart = await _db.Carts.Include(c => c.Items).FirstOrDefaultAsync(c => c.UserId == userId);
            if (cart != null)
            {
                _db.CartItems.RemoveRange(cart.Items);
                _db.Carts.Remove(cart);
                await _db.SaveChangesAsync();
            }
            return NoContent();
        }
    }

    public class ReplaceCartDto
    {
        public ReplaceCartItemDto[] Items { get; set; } = new ReplaceCartItemDto[0];
    }
    public class ReplaceCartItemDto
    {
        public int ProductId { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
    }
}
