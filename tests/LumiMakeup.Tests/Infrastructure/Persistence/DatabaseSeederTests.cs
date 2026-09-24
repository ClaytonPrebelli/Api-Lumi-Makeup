using LumiMakeup.Domain.Entities;
using LumiMakeup.Domain.Enums;
using LumiMakeup.Infrastructure.Persistence;
using LumiMakeup.Tests.Helpers;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace LumiMakeup.Tests.Infrastructure.Persistence;

public class DatabaseSeederTests
{
    private static DatabaseSeeder CriarSeeder(LumiDbContext contexto, IPasswordHasher<Usuario>? hasher = null)
    {
        return new DatabaseSeeder(contexto, hasher ?? new PasswordHasher<Usuario>());
    }

    [Fact]
    public async Task SemearAdministradorAsync_cria_usuario_administrador_quando_nao_existe()
    {
        using var contexto = Testes.CriarContextoInMemory();
        var seeder = CriarSeeder(contexto);

        await seeder.SemearAdministradorAsync(new SeedAdministradorOptions(" Admin@LUMIMAKEUP.com.br ", "Admin@2026!"));

        var administrador = await contexto.Usuarios.SingleAsync();
        Assert.Equal(PapelUsuario.Administrador, administrador.Papel);
        Assert.Equal("admin@lumimakeup.com.br", administrador.Email);
        Assert.Equal("Administradora Lumi", administrador.Nome);
        Assert.NotNull(administrador.HashSenha);
        Assert.True(administrador.HashSenha.Length > 0);
    }

    [Fact]
    public async Task SemearAdministradorAsync_nao_cria_novamente_quando_administrador_ja_existe()
    {
        using var contexto = Testes.CriarContextoInMemory();
        contexto.Usuarios.Add(new Usuario
        {
            Nome = "Administradora Lumi",
            Email = "admin@lumimakeup.com.br",
            Papel = PapelUsuario.Administrador
        });
        await contexto.SaveChangesAsync();
        var seeder = CriarSeeder(contexto);

        await seeder.SemearAdministradorAsync(new SeedAdministradorOptions("admin@lumimakeup.com.br", "Outra"));

        Assert.Single(contexto.Usuarios);
        Assert.True(contexto.Usuarios.Single().HashSenha is null);
    }

    [Fact]
    public void SeedAdministradorOptions_expoe_email_e_senha()
    {
        var opcoes = new SeedAdministradorOptions("email", "senha");

        Assert.Equal("email", opcoes.Email);
        Assert.Equal("senha", opcoes.Senha);
    }
}