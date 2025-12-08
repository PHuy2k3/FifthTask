// be/Store.Api/Models/OrderDtos.cs
namespace Store.Api.Models
{
    public class CreateOrderItemDto
    {
        public int ProductId { get; set; }
        public int Quantity { get; set; } = 1;
        public decimal UnitPrice { get; set; } = 0;
    }

    public class CreateOrderDto
    {
        public CreateOrderItemDto[] Items { get; set; } = new CreateOrderItemDto[0];
        public decimal TotalAmount { get; set; } = 0;
        public string? Address { get; set; }
        public string? Phone { get; set; }
        public string? PaymentMethod { get; set; }
        public string? Note { get; set; }
        public string? CustomerName { get; set; }
        public string? CustomerEmail { get; set; }
    }

    public class UpdateStatusDto
    {
        public string Status { get; set; } = "";
    }
}
