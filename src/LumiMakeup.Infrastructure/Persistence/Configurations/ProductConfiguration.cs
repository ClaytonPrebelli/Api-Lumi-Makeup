using LumiMakeup.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LumiMakeup.Infrastructure.Persistence.Configurations;

public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("products");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Name).HasMaxLength(150).IsRequired();
        builder.Property(p => p.Slug).HasMaxLength(180).IsRequired();
        builder.HasIndex(p => p.Slug).IsUnique();
        builder.Property(p => p.Description).HasColumnType("text").IsRequired();
        builder.Property(p => p.CostPrice).HasColumnType("decimal(10,2)");
        builder.Property(p => p.SalePrice).HasColumnType("decimal(10,2)");
        builder.Property(p => p.StockQuantity).HasColumnType("int");
        builder.Property(p => p.IsActive).HasColumnType("tinyint(1)");
        builder.Property(p => p.CreatedAt).HasColumnType("datetime");

        builder.HasOne(p => p.Category)
            .WithMany(c => c.Products)
            .HasForeignKey(p => p.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}