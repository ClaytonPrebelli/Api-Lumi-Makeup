using LumiMakeup.Domain.Entities;
using LumiMakeup.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LumiMakeup.Infrastructure.Persistence.Configurations;

public class WhatsAppLogConfiguration : IEntityTypeConfiguration<WhatsAppLog>
{
    public void Configure(EntityTypeBuilder<WhatsAppLog> builder)
    {
        builder.ToTable("whatsapp_logs");
        builder.HasKey(l => l.Id);

        builder.Property(l => l.PhoneNumber).HasMaxLength(20).IsRequired();
        builder.Property(l => l.Message).HasColumnType("text").IsRequired();

        builder.Property(l => l.Status)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(l => l.SentAt).HasColumnType("datetime");
    }
}