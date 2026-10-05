using LumiMakeup.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LumiMakeup.Infrastructure.Persistence.Configurations;

public sealed class ConfiguracaoDeRecompraConfiguration : IEntityTypeConfiguration<ConfiguracaoDeRecompra>
{
    public void Configure(EntityTypeBuilder<ConfiguracaoDeRecompra> builder)
    {
        builder.ToTable("configuracao_recompra");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Assunto).HasMaxLength(200).IsRequired();
        builder.Property(c => c.Mensagem).HasColumnType("longtext").IsRequired();
        builder.Property(c => c.AtualizadoEm).HasColumnType("datetime").IsRequired();
    }
}
