using LumiMakeup.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LumiMakeup.Infrastructure.Persistence.Configurations;

public class AddressConfiguration : IEntityTypeConfiguration<Address>
{
    public void Configure(EntityTypeBuilder<Address> builder)
    {
        builder.ToTable("addresses");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Cep).HasMaxLength(9).IsRequired();
        builder.Property(a => a.Street).HasMaxLength(200).IsRequired();
        builder.Property(a => a.Number).HasMaxLength(20).IsRequired();
        builder.Property(a => a.Complement).HasMaxLength(100);
        builder.Property(a => a.Neighborhood).HasMaxLength(100).IsRequired();
        builder.Property(a => a.City).HasMaxLength(100).IsRequired();
        builder.Property(a => a.State).HasMaxLength(2).IsRequired();
        builder.Property(a => a.Latitude).HasColumnType("decimal(10,7)");
        builder.Property(a => a.Longitude).HasColumnType("decimal(10,7)");
        builder.Property(a => a.IsDefault).HasColumnType("tinyint(1)");
    }
}