using LumiMakeup.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LumiMakeup.Infrastructure.Persistence.Configurations;

public class MovimentoEstoqueConfiguration : IEntityTypeConfiguration<MovimentoEstoque>
{
    public void Configure(EntityTypeBuilder<MovimentoEstoque> builder)
    {
        builder.ToTable("movimentos_estoque");
        builder.HasKey(m => m.Id);

        builder.Property(m => m.Tipo).HasConversion<byte>().IsRequired();
        builder.Property(m => m.Quantidade).IsRequired();
        builder.Property(m => m.Referencia).HasMaxLength(100);
        builder.Property(m => m.Observacao).HasMaxLength(500);

        builder.HasOne(m => m.Produto)
            .WithMany()
            .HasForeignKey(m => m.ProdutoId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(m => m.Usuario)
            .WithMany()
            .HasForeignKey(m => m.UsuarioId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(m => m.ProdutoId);
        builder.HasIndex(m => m.CriadoEm);
    }
}