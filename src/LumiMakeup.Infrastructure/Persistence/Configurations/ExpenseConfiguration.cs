using LumiMakeup.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LumiMakeup.Infrastructure.Persistence.Configurations;

public class ExpenseConfiguration : IEntityTypeConfiguration<Expense>
{
    public void Configure(EntityTypeBuilder<Expense> builder)
    {
        builder.ToTable("expenses");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Description).HasMaxLength(255).IsRequired();
        builder.Property(e => e.Category).HasMaxLength(100).IsRequired();
        builder.Property(e => e.Amount).HasColumnType("decimal(10,2)");
        builder.Property(e => e.ExpenseDate).HasColumnType("date");
        builder.Property(e => e.CreatedAt).HasColumnType("datetime");
    }
}