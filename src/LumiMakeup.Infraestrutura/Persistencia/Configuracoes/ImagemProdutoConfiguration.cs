using LumiMakeup.Dominio.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LumiMakeup.Infraestrutura.Persistencia.Configuracoes;

public class ImagemProdutoConfiguration : IEntityTypeConfiguration<ImagemProduto>
{
    public void Configure(EntityTypeBuilder<ImagemProduto> builder)
    {
        builder.ToTable("imagens_produto");
        builder.HasKey(i => i.Id);

        builder.Property(i => i.UrlImagem).HasMaxLength(500).IsRequired();
        builder.Property(i => i.Ordem).HasColumnType("int");

        builder.HasOne(i => i.Produto)
            .WithMany(p => p.Imagens)
            .HasForeignKey(i => i.ProdutoId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}