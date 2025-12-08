using System;

namespace Store.Data.Model
{
    public class OrderItem
    {
        public long OrderItemId { get; set; }
        public long OrderId { get; set; }
        public int ProductId { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }

        // Navigation
        public virtual Order? Order { get; set; }
        // If you have a Product model, add nav:
        // public virtual Product? Product { get; set; }
    }
}
