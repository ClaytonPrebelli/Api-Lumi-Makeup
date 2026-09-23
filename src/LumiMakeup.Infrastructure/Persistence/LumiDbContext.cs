using LumiMakeup.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace LumiMakeup.Infrastructure.Persistence;

public class LumiDbContext : DbContext
{
    public LumiDbContext(DbContextOptions<LumiDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Address> Addresses => Set<Address>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductImage> ProductImages => Set<ProductImage>();
    public DbSet<ShippingConfig> ShippingConfigs => Set<ShippingConfig>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<Expense> Expenses => Set<Expense>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<WhatsAppLog> WhatsAppLogs => Set<WhatsAppLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(LumiDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}