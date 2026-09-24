using LumiMakeup.Domain.Entities;
using LumiMakeup.Domain.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace LumiMakeup.Infrastructure.Persistence;

public sealed record SeedAdministradorOptions(string Email, string Senha);

public sealed class DatabaseSeeder
{
    private readonly LumiDbContext _contexto;
    private readonly IPasswordHasher<Usuario> _passwordHasher;

    public DatabaseSeeder(LumiDbContext contexto, IPasswordHasher<Usuario> passwordHasher)
    {
        _contexto = contexto;
        _passwordHasher = passwordHasher;
    }

    public async Task SemearAdministradorAsync(SeedAdministradorOptions opcoes, CancellationToken cancellationToken = default)
    {
        var email = opcoes.Email.Trim().ToLowerInvariant();
        var administradorExiste = await _contexto.Usuarios.AnyAsync(u => u.Papel == PapelUsuario.Administrador, cancellationToken);

        if (administradorExiste)
        {
            return;
        }

        var usuario = new Usuario
        {
            Nome = "Administradora Lumi",
            Email = email,
            Papel = PapelUsuario.Administrador,
            CriadoEm = DateTime.UtcNow
        };

        usuario.HashSenha = _passwordHasher.HashPassword(usuario, opcoes.Senha);

        _contexto.Usuarios.Add(usuario);
        await _contexto.SaveChangesAsync(cancellationToken);
    }
}