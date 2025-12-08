using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace Store.Biz.Background
{
    public enum NotificationJobType { SendEmail, RetrySignalR }

    public class NotificationJob
    {
        public NotificationJobType JobType { get; set; }
        public int NotificationId { get; set; }
        public string? Payload { get; set; }
        public string? Email { get; set; }
        public string? UserGroup { get; set; }
    }

    public interface IBackgroundTaskQueue
    {
        void QueueBackgroundWorkItem(NotificationJob job);
        Task<NotificationJob?> DequeueAsync(CancellationToken cancellationToken);
    }

    public class BackgroundTaskQueue : IBackgroundTaskQueue
    {
        private readonly ConcurrentQueue<NotificationJob> _jobs = new();
        private readonly SemaphoreSlim _signal = new(0);

        public void QueueBackgroundWorkItem(NotificationJob job)
        {
            if (job == null) throw new ArgumentNullException(nameof(job));
            _jobs.Enqueue(job);
            _signal.Release();
        }

        public async Task<NotificationJob?> DequeueAsync(CancellationToken cancellationToken)
        {
            await _signal.WaitAsync(cancellationToken);
            _jobs.TryDequeue(out var job);
            return job;
        }
    }
}
