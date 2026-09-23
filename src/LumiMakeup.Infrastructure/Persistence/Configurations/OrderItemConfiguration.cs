using LumiMakeup.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LumiMakeup.Infrastructure.Persistence.Configurations;

public class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
{
    public void Configure(EntityTypeBuilder<OrderItem> builder)
    {
        builder.ToTable("order_items");
        builder.HasKey(i => i.Id);

        builder.Property(i => i.ProductNameSnapshot).HasMaxLength(150).IsRequired();
        builder.Property(i => i.UnitCostPrice).HasColumnType("decimal(10,2)");
        builder.Property(i => i.UnitSalePrice).HasColumnType("decimal(10,2)");
        builder.Property(i => i.Quantity).HasColumnType("int");
        builder.Property(i => i.Subtotal).HasColumnType("decimal(10,2)");

        builder.HasOne(i => i.Product)
            .WithMany(p => p.OrderItems)
            .HasForeignKey(i => i.ProductId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}