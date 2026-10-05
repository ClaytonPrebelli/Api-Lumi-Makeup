using LumiMakeup.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LumiMakeup.Infrastructure.Persistence.Configurations;

public sealed class EnvioDeRecompraConfiguration : IEntityTypeConfiguration<EnvioDeRecompra>
{
    public void Configure(EntityTypeBuilder<EnvioDeRecompra> builder)
    {
        builder.ToTable("envios_recompra");
        builder.HasKey(e => e.PedidoId);
        builder.Property(e => e.PedidoId).ValueGeneratedNever();
        builder.Property(e => e.ReservadoEm).HasColumnType("datetime").IsRequired();
        builder.Property(e => e.EnviadoEm).HasColumnType("datetime");
        builder.Property(e => e.Versao).IsConcurrencyToken();
    }
}
