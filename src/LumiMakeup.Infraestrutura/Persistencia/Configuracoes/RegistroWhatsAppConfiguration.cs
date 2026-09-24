using LumiMakeup.Dominio.Entidades;
using LumiMakeup.Dominio.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LumiMakeup.Infraestrutura.Persistencia.Configuracoes;

public class RegistroWhatsAppConfiguration : IEntityTypeConfiguration<RegistroWhatsApp>
{
    public void Configure(EntityTypeBuilder<RegistroWhatsApp> builder)
    {
        builder.ToTable("registros_whatsapp");
        builder.HasKey(l => l.Id);

        builder.Property(l => l.Telefone).HasMaxLength(20).IsRequired();
        builder.Property(l => l.Mensagem).HasColumnType("text").IsRequired();

        builder.Property(l => l.Status)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(l => l.EnviadoEm).HasColumnType("datetime");
    }
}