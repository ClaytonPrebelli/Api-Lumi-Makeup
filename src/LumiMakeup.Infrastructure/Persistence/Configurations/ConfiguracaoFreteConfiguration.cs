using LumiMakeup.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LumiMakeup.Infrastructure.Persistence.Configurations;

public class ConfiguracaoFreteConfiguration : IEntityTypeConfiguration<ConfiguracaoFrete>
{
    public void Configure(EntityTypeBuilder<ConfiguracaoFrete> builder)
    {
        builder.ToTable("configuracao_frete");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.CepOrigem).HasMaxLength(9).IsRequired();
        builder.Property(s => s.LatitudeOrigem).HasColumnType("decimal(10,7)");
        builder.Property(s => s.LongitudeOrigem).HasColumnType("decimal(10,7)");
        builder.Property(s => s.PrecoPorKm).HasColumnType("decimal(10,2)");
        builder.Property(s => s.TaxaMinima).HasColumnType("decimal(10,2)");
    }
}