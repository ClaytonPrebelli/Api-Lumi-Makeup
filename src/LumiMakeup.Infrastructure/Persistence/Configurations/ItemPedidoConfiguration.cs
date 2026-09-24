using LumiMakeup.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LumiMakeup.Infrastructure.Persistence.Configurations;

public class ItemPedidoConfiguration : IEntityTypeConfiguration<ItemPedido>
{
    public void Configure(EntityTypeBuilder<ItemPedido> builder)
    {
        builder.ToTable("itens_pedido");
        builder.HasKey(i => i.Id);

        builder.Property(i => i.NomeProdutoRegistrado).HasMaxLength(150).IsRequired();
        builder.Property(i => i.PrecoCustoUnitario).HasColumnType("decimal(10,2)");
        builder.Property(i => i.PrecoVendaUnitario).HasColumnType("decimal(10,2)");
        builder.Property(i => i.Quantidade).HasColumnType("int");
        builder.Property(i => i.Subtotal).HasColumnType("decimal(10,2)");

        builder.HasOne(i => i.Produto)
            .WithMany(p => p.ItensPedido)
            .HasForeignKey(i => i.ProdutoId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}