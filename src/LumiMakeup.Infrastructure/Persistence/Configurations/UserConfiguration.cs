using LumiMakeup.Domain.Entities;
using LumiMakeup.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LumiMakeup.Infrastructure.Persistence.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");
        builder.HasKey(u => u.Id);

        builder.Property(u => u.Name).HasMaxLength(150).IsRequired();
        builder.Property(u => u.Email).HasMaxLength(150).IsRequired();
        builder.HasIndex(u => u.Email).IsUnique();
        builder.Property(u => u.PasswordHash).HasMaxLength(255);
builder.Property(u => u.GoogleId).HasMaxLength(255);
        builder.HasIndex(u => u.GoogleId).IsUnique();
        builder.Property(u => u.Cpf).HasMaxLength(11);
        builder.HasIndex(u => u.Cpf).IsUnique();
        builder.Property(u => u.Phone).HasMaxLength(20);
        builder.Property(u => u.Role)
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(u => u.CreatedAt).HasColumnType("datetime");

        builder.HasMany(u => u.Addresses)
            .WithOne(a => a.User)
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(u => u.Orders)
            .WithOne(o => o.User)
            .HasForeignKey(o => o.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(u => u.Expenses)
            .WithOne(e => e.CreatedByUser)
            .HasForeignKey(e => e.CreatedBy)
            .OnDelete(DeleteBehavior.Restrict);
    }
}