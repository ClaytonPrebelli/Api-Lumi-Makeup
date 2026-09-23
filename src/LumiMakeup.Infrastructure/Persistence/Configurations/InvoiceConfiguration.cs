using LumiMakeup.Domain.Entities;
using LumiMakeup.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LumiMakeup.Infrastructure.Persistence.Configurations;

public class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
{
    public void Configure(EntityTypeBuilder<Invoice> builder)
    {
        builder.ToTable("invoices");
        builder.HasKey(i => i.Id);

        builder.Property(i => i.Status)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(i => i.FocusNfeReference).HasMaxLength(100);
        builder.Property(i => i.XmlUrl).HasMaxLength(500);
        builder.Property(i => i.PdfUrl).HasMaxLength(500);
        builder.Property(i => i.IssuedAt).HasColumnType("datetime");
    }
}