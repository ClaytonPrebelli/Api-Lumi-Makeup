namespace LumiMakeup.Domain.Entities;

public class ProductImage
{
    public long Id { get; set; }
    public long ProductId { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    public int SortOrder { get; set; }

    public Product Product { get; set; } = null!;
}