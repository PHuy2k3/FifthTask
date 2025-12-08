using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Store.Data;

namespace Store.Biz.Background
{
    // NOTE: IEmailService vẫn khai báo ở đây; nếu bạn implement ở nơi khác, đảm bảo DI
    public interface IEmailService
    {
        Task SendAsync(string to, string subject, string body);
    }

    public class NotificationWorker : BackgroundService
    {
        private readonly IBackgroundTaskQueue _queue;
        private readonly ILogger<NotificationWorker> _logger;
        private readonly IServiceProvider _services;

        public NotificationWorker(IBackgroundTaskQueue queue, IServiceProvider services, ILogger<NotificationWorker> logger)
        {
            _queue = queue;
            _services = services;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("NotificationWorker started.");
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var job = await _queue.DequeueAsync(stoppingToken);
                    if (job == null) continue;

                    using var scope = _services.CreateScope();

                    // NOTE: sử dụng interface trong namespace Store.Biz.Interfaces
                    var notifier = (Store.Biz.Interfaces.IRealtimeNotifier)scope.ServiceProvider.GetService(typeof(Store.Biz.Interfaces.IRealtimeNotifier));
                    var db = (StoreDbContext)scope.ServiceProvider.GetService(typeof(StoreDbContext));
                    var emailService = scope.ServiceProvider.GetService(typeof(IEmailService)) as IEmailService;

                    if (job.JobType == NotificationJobType.RetrySignalR)
                    {
                        try
                        {
                            if (notifier != null)
                            {
                                await notifier.NotifyGroupAsync(job.UserGroup!, new
                                {
                                    notificationId = job.NotificationId,
                                    message = job.Payload,
                                    createdAt = DateTime.UtcNow
                                });
                                _logger.LogInformation("RetrySignalR success for notif {Id}", job.NotificationId);
                            }
                            else
                            {
                                _logger.LogWarning("IRealtimeNotifier not registered - cannot RetrySignalR for {Id}", job.NotificationId);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "RetrySignalR failed for notif {Id}", job.NotificationId);
                        }
                    }
                    else if (job.JobType == NotificationJobType.SendEmail)
                    {
                        try
                        {
                            if (emailService != null && !string.IsNullOrEmpty(job.Email))
                            {
                                await emailService.SendAsync(job.Email, "Cập nhật đơn hàng", job.Payload ?? "");
                                _logger.LogInformation("Email sent for notif {Id}", job.NotificationId);
                            }
                            else
                            {
                                _logger.LogWarning("Email service not configured or email missing for notif {Id}", job.NotificationId);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "SendEmail failed for notif {Id}", job.NotificationId);
                        }
                    }
                }
                catch (OperationCanceledException) { }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "NotificationWorker error");
                    await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
                }
            }
            _logger.LogInformation("NotificationWorker stopping.");
        }
    }
}
