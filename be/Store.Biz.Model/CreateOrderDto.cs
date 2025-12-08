namespace Store.Biz.Models
{
    public class CreateOrderDto
    {
        public decimal TotalAmount { get; set; }
        public string? Note { get; set; }

        // buyer info (allow guest to fill)
        public string? CustomerName { get; set; }
        public string? CustomerEmail { get; set; }
        public string? Phone { get; set; }
        public string? Address { get; set; }
        public string? PaymentMethod { get; set; }

        public OrderItemDto[] Items { get; set; } = Array.Empty<OrderItemDto>();
    }

    public class OrderItemDto
    {
        public int ProductId { get; set; }
        public int Quantity { get; set; } = 1;
        public decimal UnitPrice { get; set; }
    }
}
