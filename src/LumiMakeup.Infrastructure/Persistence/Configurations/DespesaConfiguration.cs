using LumiMakeup.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LumiMakeup.Infrastructure.Persistence.Configurations;

public class DespesaConfiguration : IEntityTypeConfiguration<Despesa>
{
    public void Configure(EntityTypeBuilder<Despesa> builder)
    {
        builder.ToTable("despesas");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Descricao).HasMaxLength(255).IsRequired();
        builder.Property(e => e.Categoria).HasMaxLength(100).IsRequired();
        builder.Property(e => e.Valor).HasColumnType("decimal(10,2)");
        builder.Property(e => e.DataDaDespesa).HasColumnType("date");
        builder.Property(e => e.CriadoEm).HasColumnType("datetime");
    }
}