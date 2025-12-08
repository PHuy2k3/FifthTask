// be/Store.Api/Controllers/StatsController.cs
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Store.Data;
using Store.Data.Model;

namespace Store.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class StatsController : ControllerBase
    {
        private readonly StoreDbContext _db;
        private readonly ILogger<StatsController> _logger;

        public StatsController(StoreDbContext db, ILogger<StatsController> logger)
        {
            _db = db;
            _logger = logger;
        }

        /// <summary>
        /// GET /api/stats?days=30&useCache=true
        /// useCache: nếu true sẽ cố đọc từ analytics.OrderStatistics (nếu tồn tại).
        /// Nếu bảng cache không tồn tại hoặc rỗng => fallback sang live compute.
        /// </summary>
        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetStats([FromQuery] int days = 30, [FromQuery] bool useCache = true)
        {
            days = Math.Clamp(days, 1, 365);
            var to = DateTime.UtcNow.Date;
            var from = to.AddDays(-days + 1);

            try
            {
                if (useCache)
                {
                    var exists = await TableExistsAsync("analytics", "OrderStatistics");
                    if (exists)
                    {
                        // đọc cache
                        var cached = await _db.OrderStatistics
                            .AsNoTracking()
                            .Where(s => s.StatDate >= from && s.StatDate <= to)
                            .OrderBy(s => s.StatDate)
                            .ToListAsync();

                        if (cached != null && cached.Count > 0)
                        {
                            var totalOrders = cached.Sum(s => s.OrdersCount);
                            var totalRevenue = cached.Sum(s => s.Revenue);

                            var byDay = cached.Select(s => new
                            {
                                date = s.StatDate.ToString("yyyy-MM-dd"),
                                orders = s.OrdersCount,
                                revenue = s.Revenue
                            }).ToList();

                            return Ok(new
                            {
                                source = "cache",
                                period = new { from = from.ToString("yyyy-MM-dd"), to = to.ToString("yyyy-MM-dd") },
                                totalOrders,
                                totalRevenue,
                                byDay
                            });
                        }

                        // nếu cache tồn tại nhưng rỗng thì fallback xuống live compute
                        _logger.LogInformation("analytics.OrderStatistics exists but empty for range {from}..{to}, fallback to live compute", from, to);
                    }
                    else
                    {
                        _logger.LogInformation("analytics.OrderStatistics not found, fallback to live compute.");
                    }
                }

                // Live compute (fallback)
                return await GetStatsLiveInternal(days);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetStats failed");
                return Problem(detail: "Lỗi lấy thống kê", statusCode: 500);
            }
        }

        /// <summary>
        /// GET /api/stats/live?days=30
        /// Tính trực tiếp, không dùng cache.
        /// </summary>
        [HttpGet("live")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetStatsLive([FromQuery] int days = 30)
        {
            days = Math.Clamp(days, 1, 365);
            try
            {
                return await GetStatsLiveInternal(days);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetStatsLive failed");
                return Problem(detail: "Lỗi tính toán thống kê", statusCode: 500);
            }
        }

        /// <summary>
        /// POST /api/stats/refresh?days=30
        /// Gọi stored procedure analytics.usp_RefreshOrderStatistics để rebuild cache.
        /// </summary>
        [HttpPost("refresh")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> RefreshStats([FromQuery] int days = 30)
        {
            days = Math.Clamp(days, 1, 365);
            try
            {
                var sql = "EXEC analytics.usp_RefreshOrderStatistics @Days";
                var p = new SqlParameter("@Days", days);
                await _db.Database.ExecuteSqlRawAsync(sql, p);
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "RefreshStats failed");
                return Problem(detail: "Không thể cập nhật cache thống kê", statusCode: 500);
            }
        }

        // -------------------------
        // Private helpers
        // -------------------------
        private async Task<bool> TableExistsAsync(string schema, string table)
        {
            try
            {
                // Tạo connection MỚI từ connection string của DbContext
                var connString = _db.Database.GetDbConnection().ConnectionString;
                await using var conn = new SqlConnection(connString);
                await conn.OpenAsync();

                await using var cmd = conn.CreateCommand();
                cmd.CommandText = @"
                        SELECT COUNT(1)
                        FROM INFORMATION_SCHEMA.TABLES
                        WHERE TABLE_SCHEMA = @schema AND TABLE_NAME = @table";
                cmd.Parameters.Add(new SqlParameter("@schema", schema));
                cmd.Parameters.Add(new SqlParameter("@table", table));

                var obj = await cmd.ExecuteScalarAsync();
                if (obj == null) return false;
                if (int.TryParse(obj.ToString(), out var cnt))
                    return cnt > 0;
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "TableExistsAsync check failed for {schema}.{table}", schema, table);
                return false;
            }
        }

        private async Task<IActionResult> GetStatsLiveInternal(int days)
        {
            var to = DateTime.UtcNow.Date;
            var from = to.AddDays(-days + 1);

            var q = _db.Orders
                .AsNoTracking()
                .Where(o => o.OrderDate >= from && o.OrderDate <= to.AddDays(1).AddTicks(-1));

            var totalOrders = await q.CountAsync();
            var totalRevenue = await q.SumAsync(o => (decimal?)o.TotalAmount) ?? 0m;

            var raw = await q
                .GroupBy(o => o.OrderDate.Date)
                .Select(g => new
                {
                    Date = g.Key,
                    Orders = g.Count(),
                    Revenue = g.Sum(x => x.TotalAmount)
                })
                .ToListAsync();

            var byDay = new List<object>();
            for (var d = from; d <= to; d = d.AddDays(1))
            {
                var found = raw.FirstOrDefault(r => r.Date == d);
                byDay.Add(new
                {
                    date = d.ToString("yyyy-MM-dd"),
                    orders = found?.Orders ?? 0,
                    revenue = found?.Revenue ?? 0m
                });
            }

            return Ok(new
            {
                source = "live",
                period = new { from = from.ToString("yyyy-MM-dd"), to = to.ToString("yyyy-MM-dd") },
                totalOrders,
                totalRevenue,
                byDay
            });
        }
    }
}
