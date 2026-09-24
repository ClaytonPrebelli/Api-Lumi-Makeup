using LumiMakeup.Dominio.Entidades;
using LumiMakeup.Dominio.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace LumiMakeup.Infraestrutura.Persistencia;

public sealed record OpcoesDeSeedDeAdministrador(string Email, string Senha);

public sealed class GeradorDeDadosIniciais
{
    private readonly ContextoLumi _contexto;
    private readonly IPasswordHasher<Usuario> _passwordHasher;

    public GeradorDeDadosIniciais(ContextoLumi contexto, IPasswordHasher<Usuario> passwordHasher)
    {
        _contexto = contexto;
        _passwordHasher = passwordHasher;
    }

    public async Task SemearAdministradorAsync(OpcoesDeSeedDeAdministrador opcoes, CancellationToken cancellationToken = default)
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