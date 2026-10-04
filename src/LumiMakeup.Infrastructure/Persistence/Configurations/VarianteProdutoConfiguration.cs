using LumiMakeup.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LumiMakeup.Infrastructure.Persistence.Configurations;

public class VarianteProdutoConfiguration : IEntityTypeConfiguration<VarianteProduto>
{
    public void Configure(EntityTypeBuilder<VarianteProduto> builder)
    {
        builder.ToTable("variantes_produto");
        builder.HasKey(v => v.Id);

        builder.Property(v => v.Nome).HasMaxLength(80).IsRequired();
        builder.Property(v => v.CorHex).HasMaxLength(7).IsRequired(); // #RRGGBB
        builder.Property(v => v.QuantidadeEstoque).HasColumnType("int");
        builder.Property(v => v.PrecoAdicional).HasColumnType("decimal(10,2)");
        builder.Property(v => v.Ativo).HasColumnType("tinyint(1)");
        builder.Property(v => v.Ordem).HasColumnType("int");
        builder.Property(v => v.CriadoEm).HasColumnType("datetime");

        // Token de concorrência para estoque
        builder.Property(v => v.QuantidadeEstoque).IsConcurrencyToken();

        builder.HasOne(v => v.Produto)
            .WithMany(p => p.Variantes)
            .HasForeignKey(v => v.ProdutoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(v => v.ProdutoId);
    }
}