using LumiMakeup.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LumiMakeup.Infrastructure.Persistence.Configurations;

public class EnderecoConfiguration : IEntityTypeConfiguration<Endereco>
{
    public void Configure(EntityTypeBuilder<Endereco> builder)
    {
        builder.ToTable("enderecos");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Cep).HasMaxLength(9).IsRequired();
        builder.Property(a => a.Logradouro).HasMaxLength(200).IsRequired();
        builder.Property(a => a.Numero).HasMaxLength(20).IsRequired();
        builder.Property(a => a.Complemento).HasMaxLength(100);
        builder.Property(a => a.Bairro).HasMaxLength(100).IsRequired();
        builder.Property(a => a.Cidade).HasMaxLength(100).IsRequired();
        builder.Property(a => a.Estado).HasMaxLength(2).IsRequired();
        builder.Property(a => a.Latitude).HasColumnType("decimal(10,7)");
        builder.Property(a => a.Longitude).HasColumnType("decimal(10,7)");
        builder.Property(a => a.Padrao).HasColumnType("tinyint(1)");
    }
}