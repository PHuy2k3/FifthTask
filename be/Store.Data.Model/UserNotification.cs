using System;

namespace Store.Data.Model
{
    public class UserNotification
    {
        // Khóa chính: Id
        public int Id { get; set; }

        // Nếu null => global notification
        public int? UserId { get; set; }

        public long OrderId { get; set; }

        public string Message { get; set; } = "";

        public bool IsRead { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
