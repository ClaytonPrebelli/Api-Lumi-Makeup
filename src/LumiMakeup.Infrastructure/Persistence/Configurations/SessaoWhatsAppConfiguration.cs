using LumiMakeup.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LumiMakeup.Infrastructure.Persistence.Configurations;

public class SessaoWhatsAppConfiguration : IEntityTypeConfiguration<SessaoWhatsApp>
{
    public void Configure(EntityTypeBuilder<SessaoWhatsApp> builder)
    {
        builder.ToTable("sessao_whatsapp");
        builder.HasKey(s => s.Id);

        // LONGTEXT, e nao VARCHAR: o JSON de credenciais tem alguns KB e o de
        // chaves de sinal passa de 64 KB com o uso. Um VARCHAR curto faria o
        // MySQL truncar o valor em silencio, e a sessao recem-lida viraria
        // invalida - sintoma de "o Node subiu e saiu" sem nenhuma pista.
        builder.Property(s => s.Credenciais).HasColumnType("LONGTEXT").IsRequired();
        builder.Property(s => s.Chaves).HasColumnType("LONGTEXT").IsRequired();
    }
}
