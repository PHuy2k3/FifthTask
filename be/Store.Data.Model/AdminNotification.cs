using System;

namespace Store.Data.Model
{
    public class AdminNotification
    {
        // Trường Id phải có để khớp với mapping (bản mapping dùng "Id")
        public int Id { get; set; }

        // Khớp với mapping/DB
        public long OrderId { get; set; }
        public string? Message { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public bool IsRead { get; set; } = false;
    }
}
