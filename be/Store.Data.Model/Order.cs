using System;
using System.Collections.Generic;

namespace Store.Data.Model
{
    public class Order
    {
        public long OrderId { get; set; }
        public int? CustomerId { get; set; }
        public DateTime OrderDate { get; set; } = DateTime.UtcNow;
        public decimal TotalAmount { get; set; }
        public string? Status { get; set; }
        public string? Note { get; set; }

        public string? CustomerName { get; set; }
        public string? CustomerEmail { get; set; }
        public string? CustomerPhone { get; set; }

        public string? ShippingAddress { get; set; }
        public string? PaymentMethod { get; set; }

        // Optional: shipped date
        public DateTime? ShippedAt { get; set; }

        // Navigation
        public virtual ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();
    }
}
