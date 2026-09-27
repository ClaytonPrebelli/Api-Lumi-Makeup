using LumiMakeup.Domain.Entities;
using LumiMakeup.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LumiMakeup.Infrastructure.Persistence.Configurations;

public class PedidoConfiguration : IEntityTypeConfiguration<Pedido>
{
    public void Configure(EntityTypeBuilder<Pedido> builder)
    {
        builder.ToTable("pedidos");
        builder.HasKey(o => o.Id);

        builder.Property(o => o.Status)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(o => o.StatusEntrega)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(o => o.MetodoPagamento)
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(o => o.EnderecoCep).HasMaxLength(9).IsRequired();
        builder.Property(o => o.EnderecoLogradouro).HasMaxLength(200).IsRequired();
        builder.Property(o => o.EnderecoNumero).HasMaxLength(20).IsRequired();
        builder.Property(o => o.EnderecoComplemento).HasMaxLength(100);
        builder.Property(o => o.EnderecoBairro).HasMaxLength(100).IsRequired();
        builder.Property(o => o.EnderecoCidade).HasMaxLength(100).IsRequired();
        builder.Property(o => o.EnderecoEstado).HasMaxLength(2).IsRequired();

        builder.Property(o => o.DistanciaKm).HasColumnType("decimal(8,2)");
        builder.Property(o => o.CustoFrete).HasColumnType("decimal(10,2)");
        builder.Property(o => o.Subtotal).HasColumnType("decimal(10,2)");
        builder.Property(o => o.Total).HasColumnType("decimal(10,2)");
        builder.Property(o => o.Observacoes).HasColumnType("text");
        builder.Property(o => o.CriadoEm).HasColumnType("datetime");
        builder.Property(o => o.PagoEm).HasColumnType("datetime");
        builder.Property(o => o.EntregueEm).HasColumnType("datetime");

        builder.HasMany(o => o.Itens)
            .WithOne(i => i.Pedido)
            .HasForeignKey(i => i.PedidoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(o => o.NotasFiscais)
            .WithOne(i => i.Pedido)
            .HasForeignKey(i => i.PedidoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(o => o.RegistrosWhatsApp)
            .WithOne(l => l.Pedido)
            .HasForeignKey(l => l.PedidoId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}