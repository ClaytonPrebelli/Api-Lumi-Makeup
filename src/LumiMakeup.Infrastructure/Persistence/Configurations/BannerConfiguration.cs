using LumiMakeup.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LumiMakeup.Infrastructure.Persistence.Configurations;

public class BannerConfiguration : IEntityTypeConfiguration<Banner>
{
    public void Configure(EntityTypeBuilder<Banner> builder)
    {
        builder.ToTable("banners");
        builder.HasKey(b => b.Id);

        builder.Property(b => b.CaminhoRelativoDesktop).HasMaxLength(500).IsRequired();
        builder.Property(b => b.CaminhoRelativoMobile).HasMaxLength(500).IsRequired();
        builder.Property(b => b.NomeOriginalDesktop).HasMaxLength(255).IsRequired();
        builder.Property(b => b.NomeOriginalMobile).HasMaxLength(255).IsRequired();
        builder.Property(b => b.TextoAlternativo).HasMaxLength(160);
        builder.Property(b => b.Ordem).HasColumnType("int");
        builder.Property(b => b.Ativo).HasColumnType("tinyint(1)");
        builder.Property(b => b.CriadoEm).HasColumnType("datetime(6)");

        builder.HasIndex(b => b.Ordem);
        builder.HasIndex(b => b.Ativo);
    }
}
