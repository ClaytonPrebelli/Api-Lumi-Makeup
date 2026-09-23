using LumiMakeup.Domain.Enums;

namespace LumiMakeup.Domain.Entities;

public class User
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? PasswordHash { get; set; }
    public string? GoogleId { get; set; }
    public string? Cpf { get; set; }
    public string? Phone { get; set; }
    public UserRole Role { get; set; } = UserRole.Customer;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Address> Addresses { get; set; } = new List<Address>();
    public ICollection<Order> Orders { get; set; } = new List<Order>();
    public ICollection<Expense> Expenses { get; set; } = new List<Expense>();
}