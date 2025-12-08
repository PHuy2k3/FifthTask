using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Store.Biz.Interfaces;
using Store.Biz.Background;
using Store.Data;
using Store.Data.Model;

namespace Store.Biz.Services
{
    public class OrderService : IOrderService
    {
        private readonly StoreDbContext _db;
        private readonly IRealtimeNotifier _notifier;
        private readonly IBackgroundTaskQueue _tasks;
        private readonly ILogger<OrderService> _logger;

        public OrderService(
            StoreDbContext db,
            IRealtimeNotifier notifier,
            IBackgroundTaskQueue tasks,
            ILogger<OrderService> logger)
        {
            _db = db;
            _notifier = notifier;
            _tasks = tasks;
            _logger = logger;
        }

        public async Task<Result> UpdateOrderStatusAsync(long orderId, string newStatus, string changedByUserId)
        {
            using var tx = await _db.Database.BeginTransactionAsync();
            try
            {
                var order = await _db.Orders.FirstOrDefaultAsync(o => o.OrderId == orderId);
                if (order == null) return Result.Fail("Order not found");

                var old = order.Status;
                order.Status = newStatus;

                await _db.SaveChangesAsync();

                var notif = new UserNotification
                {
                    UserId = order.CustomerId,
                    OrderId = order.OrderId,
                    Message = $"Đơn #{order.OrderId}: {old} → {newStatus}",
                    CreatedAt = DateTime.UtcNow
                };

                _db.UserNotifications.Add(notif);
                await _db.SaveChangesAsync();

                await tx.CommitAsync();

                if (order.CustomerId != null)
                {
                    var group = $"user-{order.CustomerId}";

                    try
                    {
                        await _notifier.NotifyGroupAsync(group, new
                        {
                            notif.Id,
                            notif.OrderId,
                            notif.Message,
                            notif.CreatedAt
                        });
                    }
                    catch (Exception ex)
                    {
                        _tasks.QueueBackgroundWorkItem(new NotificationJob
                        {
                            JobType = NotificationJobType.RetrySignalR,
                            UserGroup = group,
                            NotificationId = notif.Id,
                            Payload = notif.Message
                        });
                    }
                }

                return Result.Ok();
            }
            catch (Exception ex)
            {
                try { await tx.RollbackAsync(); } catch { }
                return Result.Fail("Internal error");
            }
        }
    }
}
