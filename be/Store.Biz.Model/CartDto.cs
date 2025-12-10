namespace Store.Biz.Model;

public class ReplaceCartDto
{
    public ReplaceCartItemDto[] Items { get; set; } = new ReplaceCartItemDto[0];
}

public class ReplaceCartItemDto
{
    public int ProductId { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
}