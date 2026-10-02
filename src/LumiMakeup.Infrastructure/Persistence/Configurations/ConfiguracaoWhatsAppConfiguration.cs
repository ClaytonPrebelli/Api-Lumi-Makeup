using LumiMakeup.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LumiMakeup.Infrastructure.Persistence.Configurations;

public class ConfiguracaoWhatsAppConfiguration : IEntityTypeConfiguration<ConfiguracaoWhatsApp>
{
    public void Configure(EntityTypeBuilder<ConfiguracaoWhatsApp> builder)
    {
        builder.ToTable("configuracao_whatsapp");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.MensagemInicialCliente).HasMaxLength(500).IsRequired();
    }
}
