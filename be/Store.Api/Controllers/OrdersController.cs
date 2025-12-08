using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Store.Data;
using Store.Data.Model;
using Store.Api.Models;

namespace Store.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class OrdersController : ControllerBase
    {
        private readonly StoreDbContext _db;
        private readonly ILogger<OrdersController> _logger;

        public OrdersController(StoreDbContext db, ILogger<OrdersController> logger)
        {
            _db = db;
            _logger = logger;
        }

        // ---------------------------
        // POST /api/orders  (Logged-in user)
        // ---------------------------
        [HttpPost]
        [Authorize]
        public async Task<IActionResult> Create([FromBody] CreateOrderDto dto)
        {
            return await CreateOrderInternal(dto, requireAuth: true);
        }

        // ---------------------------
        // POST /api/orders/pay (Anonymous checkout)
        // ---------------------------
        [HttpPost("pay")]
        [AllowAnonymous]
        public async Task<IActionResult> Pay([FromBody] CreateOrderDto dto)
        {
            return await CreateOrderInternal(dto, requireAuth: false);
        }

        // ---------------------------
        // INTERNAL ORDER CREATION
        // ---------------------------
        private async Task<IActionResult> CreateOrderInternal(CreateOrderDto dto, bool requireAuth)
        {
            if (dto == null || dto.Items == null || dto.Items.Length == 0)
                return BadRequest(new { error = "No items in order" });

            if (requireAuth && !User.Identity.IsAuthenticated)
                return Unauthorized();

            int? userId = null;
            var sub = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
            if (int.TryParse(sub, out var parsed)) userId = parsed;

            await using var tx = await _db.Database.BeginTransactionAsync();
            try
            {
                var order = new Order
                {
                    CustomerId = userId,
                    OrderDate = DateTime.UtcNow,
                    TotalAmount = dto.TotalAmount,
                    Status = "Pending",
                    Note = dto.Note,
                    CustomerName = dto.CustomerName,
                    CustomerEmail = dto.CustomerEmail,
                    CustomerPhone = dto.Phone,
                    ShippingAddress = dto.Address,
                    PaymentMethod = dto.PaymentMethod
                };

                _db.Orders.Add(order);
                await _db.SaveChangesAsync();

                var items = dto.Items.Select(i => new OrderItem
                {
                    OrderId = order.OrderId,
                    ProductId = i.ProductId,
                    Quantity = i.Quantity,
                    UnitPrice = i.UnitPrice
                }).ToList();

                // Stock update
                foreach (var it in items)
                {
                    var p = await _db.Products.FindAsync(it.ProductId);
                    if (p != null)
                    {
                        if (p.Stock < it.Quantity)
                        {
                            await tx.RollbackAsync();
                            return BadRequest(new { error = $"Product {it.ProductId} not enough stock" });
                        }

                        p.Stock -= it.Quantity;
                        _db.Products.Update(p);
                    }
                }

                _db.OrderItems.AddRange(items);
                await _db.SaveChangesAsync();

                _db.AdminNotifications.Add(new AdminNotification
                {
                    OrderId = order.OrderId,
                    Message = $"Đơn hàng #{order.OrderId} đang chờ duyệt",
                    CreatedAt = DateTime.UtcNow,
                    IsRead = false
                });
                await _db.SaveChangesAsync();

                await tx.CommitAsync();

                return Ok(new { orderId = order.OrderId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Create order failed");
                await tx.RollbackAsync();
                return Problem(detail: "Internal server error", statusCode: 500);
            }
        }

        // -------------------------------------------------------
        // GET /api/orders   (Admin list) — ORIGINAL ROUTE
        // -------------------------------------------------------
        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetAll(int page = 1, int pageSize = 50)
        {
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 200);

            var q = _db.Orders.AsNoTracking().OrderByDescending(o => o.OrderDate);
            var total = await q.CountAsync();
            var items = await q.Skip((page - 1) * pageSize).Take(pageSize)
                .Select(o => new {
                    o.OrderId,
                    o.OrderDate,
                    o.TotalAmount,
                    o.Status,
                    o.CustomerName,
                    o.CustomerEmail,
                    o.CustomerPhone,
                    o.ShippingAddress
                }).ToListAsync();

            return Ok(new { total, page, pageSize, items });
        }

        // -------------------------------------------------------
        //  **ADDED ROUTE**
        //  GET /api/orders/admin  (Alias of GetAll)
        // -------------------------------------------------------
        [HttpGet("admin")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetAll_Admin(int page = 1, int pageSize = 50)
        {
            return await GetAll(page, pageSize);
        }

        // -------------------------------------------------------
        // GET /api/orders/mine  (User's orders)
        // -------------------------------------------------------
        [HttpGet("mine")]
        [Authorize]
        public async Task<IActionResult> GetMine()
        {
            var sub = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
            if (!int.TryParse(sub, out var userId)) return Unauthorized();

            var orders = await _db.Orders
                .Where(o => o.CustomerId == userId)
                .OrderByDescending(o => o.OrderDate)
                .Select(o => new {
                    o.OrderId,
                    o.OrderDate,
                    o.TotalAmount,
                    o.Status,
                    o.ShippingAddress
                }).ToListAsync();

            return Ok(orders);
        }

        // -------------------------------------------------------
        // GET /api/orders/{id}
        // -------------------------------------------------------
        [HttpGet("{id:long}")]
        [Authorize]
        public async Task<IActionResult> GetById(long id)
        {
            var order = await _db.Orders.Include(o => o.Items).AsNoTracking()
                .FirstOrDefaultAsync(o => o.OrderId == id);

            if (order == null) return NotFound();

            var sub = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
            var callerIsAdmin = User.IsInRole("Admin");
            var isOwner = int.TryParse(sub, out var uid) && order.CustomerId == uid;

            if (!callerIsAdmin && !isOwner) return Forbid();

            return Ok(new
            {
                order.OrderId,
                order.OrderDate,
                order.TotalAmount,
                order.Status,
                order.Note,
                order.ShippingAddress,
                order.CustomerName,
                order.CustomerEmail,
                order.CustomerPhone,
                items = order.Items.Select(i => new {
                    i.OrderItemId,
                    i.ProductId,
                    i.Quantity,
                    i.UnitPrice
                })
            });
        }

        // -------------------------------------------------------
        // POST /api/orders/{id}/approve  (Admin)
        // -------------------------------------------------------
        [HttpPost("{id}/approve")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Approve(long id)
        {
            var order = await _db.Orders.FindAsync(id);
            if (order == null) return NotFound();

            order.Status = "Approved";
            await _db.SaveChangesAsync();

            var notif = await _db.AdminNotifications
                .FirstOrDefaultAsync(n => n.OrderId == id && !n.IsRead);

            if (notif != null)
            {
                notif.IsRead = true;
                await _db.SaveChangesAsync();
            }

            return Ok(new { message = "Order approved" });
        }

        [HttpPut("{id:long}/status")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateStatus(long id, [FromBody] UpdateStatusDto dto)
        {
            if (dto == null || string.IsNullOrWhiteSpace(dto.Status))
                return BadRequest(new { error = "Missing status" });

            // optional: whitelist allowed statuses
            var allowed = new[] { "Pending", "Preparing", "Shipping", "Done", "Shipped", "Cancelled", "Approved" };
            if (!allowed.Contains(dto.Status))
                return BadRequest(new { error = "Invalid status" });

            var order = await _db.Orders.FindAsync(id);
            if (order == null) return NotFound();

            order.Status = dto.Status;
            if (dto.Status == "Shipped" || dto.Status == "Done") order.ShippedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            // (optional) create admin notification or audit log
            _db.AdminNotifications.Add(new AdminNotification
            {
                OrderId = order.OrderId,
                Message = $"Order #{order.OrderId} status changed to {dto.Status}",
                CreatedAt = DateTime.UtcNow,
                IsRead = false
            });
            await _db.SaveChangesAsync();

            return NoContent();
        }
    }
}
