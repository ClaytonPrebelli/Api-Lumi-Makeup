using LumiMakeup.Dominio.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LumiMakeup.Infraestrutura.Persistencia.Configuracoes;

public class ProdutoConfiguration : IEntityTypeConfiguration<Produto>
{
    public void Configure(EntityTypeBuilder<Produto> builder)
    {
        builder.ToTable("produtos");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Nome).HasMaxLength(150).IsRequired();
        builder.Property(p => p.Slug).HasMaxLength(180).IsRequired();
        builder.HasIndex(p => p.Slug).IsUnique();
        builder.Property(p => p.Descricao).HasColumnType("text").IsRequired();
        builder.Property(p => p.PrecoCusto).HasColumnType("decimal(10,2)");
        builder.Property(p => p.PrecoVenda).HasColumnType("decimal(10,2)");
        builder.Property(p => p.QuantidadeEstoque).HasColumnType("int");
        builder.Property(p => p.Ativo).HasColumnType("tinyint(1)");
        builder.Property(p => p.CriadoEm).HasColumnType("datetime");

        builder.HasOne(p => p.Categoria)
            .WithMany(c => c.Produtos)
            .HasForeignKey(p => p.CategoriaId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}