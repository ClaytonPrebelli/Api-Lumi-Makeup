using LumiMakeup.Domain.Entities;
using LumiMakeup.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LumiMakeup.Infrastructure.Persistence.Configurations;

public class UsuarioConfiguration : IEntityTypeConfiguration<Usuario>
{
    public void Configure(EntityTypeBuilder<Usuario> builder)
    {
        builder.ToTable("usuarios");
        builder.HasKey(u => u.Id);

        builder.Property(u => u.Nome).HasMaxLength(150).IsRequired();
        builder.Property(u => u.Email).HasMaxLength(150).IsRequired();
        builder.HasIndex(u => u.Email).IsUnique();
        builder.Property(u => u.HashSenha).HasMaxLength(255);
        builder.Property(u => u.IdGoogle).HasMaxLength(255);
        builder.HasIndex(u => u.IdGoogle).IsUnique();
        builder.Property(u => u.Cpf).HasMaxLength(11);
        builder.HasIndex(u => u.Cpf).IsUnique();
        builder.Property(u => u.Telefone).HasMaxLength(20);
        builder.Property(u => u.Papel)
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(u => u.CriadoEm).HasColumnType("datetime");

        builder.HasMany(u => u.Enderecos)
            .WithOne(a => a.Usuario)
            .HasForeignKey(a => a.UsuarioId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(u => u.Pedidos)
            .WithOne(o => o.Usuario)
            .HasForeignKey(o => o.UsuarioId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(u => u.Despesas)
            .WithOne(e => e.CriadoPorUsuario)
            .HasForeignKey(e => e.CriadoPor)
            .OnDelete(DeleteBehavior.Restrict);
    }
}