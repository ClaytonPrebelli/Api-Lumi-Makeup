using LumiMakeup.Domain.Entities;
using LumiMakeup.Domain.Enums;
using LumiMakeup.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace LumiMakeup.Infrastructure.Persistence;

public sealed record AdminSeedOptions(string Email, string Password);

public sealed class DatabaseSeeder
{
    private readonly LumiDbContext _dbContext;
    private readonly IPasswordHasher<User> _passwordHasher;

    public DatabaseSeeder(LumiDbContext dbContext, IPasswordHasher<User> passwordHasher)
    {
        _dbContext = dbContext;
        _passwordHasher = passwordHasher;
    }

    public async Task SeedAdminAsync(AdminSeedOptions options, CancellationToken cancellationToken = default)
    {
        var email = options.Email.Trim().ToLowerInvariant();
        var adminExists = await _dbContext.Users.AnyAsync(u => u.Role == UserRole.Admin, cancellationToken);

        if (adminExists)
        {
            return;
        }

        var user = new User
        {
            Name = "Administrador Lumi",
            Email = email,
            Role = UserRole.Admin,
            CreatedAt = DateTime.UtcNow
        };

        user.PasswordHash = _passwordHasher.HashPassword(user, options.Password);

        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}