using LumiMakeup.Domain.Entities;
using LumiMakeup.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LumiMakeup.Infrastructure.Persistence.Configurations;

public class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("orders");
        builder.HasKey(o => o.Id);

        builder.Property(o => o.Status)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(o => o.DeliveryStatus)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(o => o.PaymentMethod)
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(o => o.DistanceKm).HasColumnType("decimal(8,2)");
        builder.Property(o => o.ShippingCost).HasColumnType("decimal(10,2)");
        builder.Property(o => o.Subtotal).HasColumnType("decimal(10,2)");
        builder.Property(o => o.Total).HasColumnType("decimal(10,2)");
        builder.Property(o => o.Notes).HasColumnType("text");
        builder.Property(o => o.CreatedAt).HasColumnType("datetime");
        builder.Property(o => o.PaidAt).HasColumnType("datetime");
        builder.Property(o => o.DeliveredAt).HasColumnType("datetime");

        builder.HasOne(o => o.ShippingAddress)
            .WithMany()
            .HasForeignKey(o => o.ShippingAddressId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(o => o.Items)
            .WithOne(i => i.Order)
            .HasForeignKey(i => i.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(o => o.Invoices)
            .WithOne(i => i.Order)
            .HasForeignKey(i => i.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(o => o.WhatsAppLogs)
            .WithOne(l => l.Order)
            .HasForeignKey(l => l.OrderId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}