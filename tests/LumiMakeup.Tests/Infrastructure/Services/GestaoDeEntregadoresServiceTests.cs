using LumiMakeup.Domain.Enums;
using LumiMakeup.Infrastructure.Persistence;
using LumiMakeup.Infrastructure.Services;
using LumiMakeup.Tests.Helpers;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace LumiMakeup.Tests.Infrastructure.Services;

public sealed class GestaoDeEntregadoresServiceTests
{
    private static GestaoDeEntregadoresService Servico(LumiDbContext contexto)
    {
        var hash = new Mock<IPasswordHasher<LumiMakeup.Domain.Entities.Usuario>>();
        hash.Setup(h => h.HashPassword(It.IsAny<LumiMakeup.Domain.Entities.Usuario>(), It.IsAny<string>()))
            .Returns("HASH");
        return new(contexto, hash.Object);
    }

    [Fact]
    public async Task CriarAsync_cadastra_com_usuario_e_senha()
    {
        using var contexto = Testes.CriarContextoInMemory();
        var servico = Servico(contexto);

        var entregador = await servico.CriarAsync(
            new("João", "joao", "secreta123"),
            CancellationToken.None);

        Assert.True(entregador.Id > 0);
        Assert.Equal("João", entregador.Nome);
        Assert.Equal("joao", entregador.Login);
        Assert.True(entregador.Ativo);

        var gravado = await contexto.Usuarios.SingleAsync();
        Assert.Equal(PapelUsuario.Entregador, gravado.Papel);
        Assert.Null(gravado.Email);
        Assert.NotNull(gravado.HashSenha);
    }

    [Fact]
    public async Task CriarAsync_recusa_login_repetido()
    {
        using var contexto = Testes.CriarContextoInMemory();
        var servico = Servico(contexto);
        await servico.CriarAsync(new("João", "joao", "secreta123"), CancellationToken.None);

        var erro = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            servico.CriarAsync(new("Outro", "JOAO", "outra123"), CancellationToken.None));

        Assert.Contains("joao", erro.Message);
    }

    [Fact]
    public async Task CriarAsync_recusa_senha_curta()
    {
        using var contexto = Testes.CriarContextoInMemory();
        var servico = Servico(contexto);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            servico.CriarAsync(new("João", "joao", "123"), CancellationToken.None));
    }

    [Fact]
    public async Task ListarAsync_traz_so_entregador()
    {
        using var contexto = Testes.CriarContextoInMemory();
        var servico = Servico(contexto);
        await servico.CriarAsync(new("João", "joao", "secreta123"), CancellationToken.None);
        contexto.Usuarios.Add(new LumiMakeup.Domain.Entities.Usuario
        {
            Nome = "Ana",
            Email = "ana@exemplo.com",
            Papel = PapelUsuario.Cliente
        });
        await contexto.SaveChangesAsync();

        var lista = await servico.ListarAsync(CancellationToken.None);

        var unico = Assert.Single(lista);
        Assert.Equal("joao", unico.Login);
    }

    [Fact]
    public async Task AtualizarAsync_troca_nome_e_desativa_sem_trocar_senha()
    {
        using var contexto = Testes.CriarContextoInMemory();
        var servico = Servico(contexto);
        var criado = await servico.CriarAsync(new("João", "joao", "secreta123"), CancellationToken.None);
        var hashAntes = (await contexto.Usuarios.SingleAsync()).HashSenha;

        var atualizado = await servico.AtualizarAsync(
            criado.Id,
            new("João Silva", "joao", null, false),
            CancellationToken.None);

        Assert.Equal("João Silva", atualizado.Nome);
        Assert.False(atualizado.Ativo);
        Assert.Equal(hashAntes, (await contexto.Usuarios.SingleAsync()).HashSenha);
    }
}
