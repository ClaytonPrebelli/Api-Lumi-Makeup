using LumiMakeup.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LumiMakeup.Infrastructure.Persistence.Configurations;

public class RecuperacaoDeSenhaConfiguration : IEntityTypeConfiguration<RecuperacaoDeSenha>
{
    public void Configure(EntityTypeBuilder<RecuperacaoDeSenha> builder)
    {
        builder.ToTable("recuperacoes_de_senha");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.HashToken)
            .HasMaxLength(64)
            .IsRequired();
        builder.HasIndex(r => r.HashToken).IsUnique();
        builder.Property(r => r.CriadoEm).HasColumnType("datetime");
        builder.Property(r => r.ExpiracaoEm).HasColumnType("datetime");
        builder.Property(r => r.UtilizadoEm).HasColumnType("datetime");

        builder.HasOne(r => r.Usuario)
            .WithMany(u => u.RecuperacoesDeSenha)
            .HasForeignKey(r => r.UsuarioId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}