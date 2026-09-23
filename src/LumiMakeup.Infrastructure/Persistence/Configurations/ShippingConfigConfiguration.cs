using LumiMakeup.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LumiMakeup.Infrastructure.Persistence.Configurations;

public class ShippingConfigConfiguration : IEntityTypeConfiguration<ShippingConfig>
{
    public void Configure(EntityTypeBuilder<ShippingConfig> builder)
    {
        builder.ToTable("shipping_config");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.OriginCep).HasMaxLength(9).IsRequired();
        builder.Property(s => s.OriginLatitude).HasColumnType("decimal(10,7)");
        builder.Property(s => s.OriginLongitude).HasColumnType("decimal(10,7)");
        builder.Property(s => s.PricePerKm).HasColumnType("decimal(10,2)");
        builder.Property(s => s.MinimumFee).HasColumnType("decimal(10,2)");
    }
}