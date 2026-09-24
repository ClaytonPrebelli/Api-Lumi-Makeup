using LumiMakeup.Dominio.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LumiMakeup.Infraestrutura.Persistencia.Configuracoes;

public class CategoriaConfiguration : IEntityTypeConfiguration<Categoria>
{
    public void Configure(EntityTypeBuilder<Categoria> builder)
    {
        builder.ToTable("categorias");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Nome).HasMaxLength(100).IsRequired();
        builder.Property(c => c.Slug).HasMaxLength(120).IsRequired();
        builder.HasIndex(c => c.Slug).IsUnique();
        builder.Property(c => c.Descricao).HasColumnType("text");
        builder.Property(c => c.Ativo).HasColumnType("tinyint(1)");
    }
}