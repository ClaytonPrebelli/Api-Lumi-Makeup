namespace LumiMakeup.Domain.Entities;

public class OrderItem
{
    public long Id { get; set; }
    public long OrderId { get; set; }
    public long ProductId { get; set; }
    public string ProductNameSnapshot { get; set; } = string.Empty;
    public decimal UnitCostPrice { get; set; }
    public decimal UnitSalePrice { get; set; }
    public int Quantity { get; set; }
    public decimal Subtotal { get; set; }

    public Order Order { get; set; } = null!;
    public Product Product { get; set; } = null!;
}