namespace LumiMakeup.Domain.Entities;

public class Product
{
    public long Id { get; set; }
    public long CategoryId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal CostPrice { get; set; }
    public decimal SalePrice { get; set; }
    public int StockQuantity { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Category Category { get; set; } = null!;
    public ICollection<ProductImage> Images { get; set; } = new List<ProductImage>();
    public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
}