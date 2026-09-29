using LumiMakeup.Application.DTOs;
using LumiMakeup.Domain.Entities;
using LumiMakeup.Domain.Enums;
using LumiMakeup.Infrastructure.Persistence;
using LumiMakeup.Infrastructure.Services;
using LumiMakeup.Tests.Helpers;
using Microsoft.EntityFrameworkCore;

namespace LumiMakeup.Tests.Infrastructure.Services;

public sealed class GestaoDeClientesServiceTests
{
    private static async Task<Usuario> SemearAsync(
        LumiDbContext contexto,
        string nome,
        string email,
        string? telefone = null,
        string? cpf = null,
        bool comSenha = true,
        PapelUsuario papel = PapelUsuario.Cliente)
    {
        var usuario = new Usuario
        {
            Nome = nome,
            Email = email,
            Telefone = telefone,
            Cpf = cpf,
            Papel = papel,
            HashSenha = comSenha ? "hash" : null,
            CriadoEm = DateTime.UtcNow
        };

        contexto.Usuarios.Add(usuario);
        await contexto.SaveChangesAsync();
        return usuario;
    }

    private static GestaoDeClientesService Servico(LumiDbContext contexto) => new(contexto);

    // ----Busca ------------------------------------------------------------

    [Fact]
    public async Task BuscarAsync_sem_termo_traz_os_clientes_mais_recentes()
    {
        using var contexto = Testes.CriarContextoInMemory();
        await SemearAsync(contexto, "Ana", "ana@exemplo.com");
        await SemearAsync(contexto, "Bruno", "bruno@exemplo.com");

        var clientes = await Servico(contexto).BuscarAsync(null, CancellationToken.None);

        Assert.Equal(2, clientes.Count);
    }

    [Fact]
    public async Task BuscarAsync_acha_por_nome_parcial_e_sem_distinguir_caixa()
    {
        using var contexto = Testes.CriarContextoInMemory();
        await SemearAsync(contexto, "Ana Souza", "ana@exemplo.com");
        await SemearAsync(contexto, "Bruno Lima", "bruno@exemplo.com");

        var clientes = await Servico(contexto).BuscarAsync("sou", CancellationToken.None);

        Assert.Equal("Ana Souza", Assert.Single(clientes).Nome);
    }

    [Fact]
    public async Task BuscarAsync_acha_por_email()
    {
        using var contexto = Testes.CriarContextoInMemory();
        await SemearAsync(contexto, "Ana", "ana@exemplo.com");
        await SemearAsync(contexto, "Bruno", "bruno@exemplo.com");

        var clientes = await Servico(contexto).BuscarAsync("bruno@", CancellationToken.None);

        Assert.Equal("Bruno", Assert.Single(clientes).Nome);
    }

    [Fact]
    public async Task BuscarAsync_acha_por_telefone_como_a_administa_digita()
    {
        using var contexto = Testes.CriarContextoInMemory();
        await SemearAsync(contexto, "Ana", "ana@exemplo.com", telefone: "11988887777");
        await SemearAsync(contexto, "Bruno", "bruno@exemplo.com", telefone: "11977776666");

        // Filtrar por nome só faria a administradora não achar quem ela conhece
        // pelo telefone, e o telefone é o que ela tem à mão na loja.
        var clientes = await Servico(contexto).BuscarAsync("1197777", CancellationToken.None);

        Assert.Equal("Bruno", Assert.Single(clientes).Nome);
    }

    [Fact]
    public async Task BuscarAsync_acha_por_cpf_com_ou_sem_pontuacao()
    {
        using var contexto = Testes.CriarContextoInMemory();
        await SemearAsync(contexto, "Ana", "ana@exemplo.com", cpf: "123.456.789-00");
        await SemearAsync(contexto, "Bruno", "bruno@exemplo.com", cpf: "999.888.777-66");

        // O CPF é gravado com pontos e traços, e a administradora digita dos dois
        // jeitos. Comparar só com o termo cru deixaria de achar o cadastro.
        var comPontos = await Servico(contexto).BuscarAsync("123.456.789-00", CancellationToken.None);
        var semPontos = await Servico(contexto).BuscarAsync("12345678900", CancellationToken.None);

        Assert.Equal("Ana", Assert.Single(comPontos).Nome);
        Assert.Equal("Ana", Assert.Single(semPontos).Nome);
    }

    [Fact]
    public async Task BuscarAsync_nao_traz_administrador_entre_os_clientes()
    {
        using var contexto = Testes.CriarContextoInMemory();
        await SemearAsync(contexto, "Ana", "ana@exemplo.com");
        await SemearAsync(contexto, "Admin", "admin@exemplo.com", papel: PapelUsuario.Administrador);

        var clientes = await Servico(contexto).BuscarAsync(null, CancellationToken.None);

        // A tela é de venda para cliente. A administradora não é cliente de si
        // mesma, e aparecer na lista faria ela se escolher por engano.
        Assert.Equal("Ana", Assert.Single(clientes).Nome);
    }

    [Fact]
    public async Task BuscarAsync_informa_se_o_cliente_ja_tem_senha()
    {
        using var contexto = Testes.CriarContextoInMemory();
        await SemearAsync(contexto, "Ana", "ana@exemplo.com", comSenha: true);
        await SemearAsync(contexto, "Bruno", "bruno@exemplo.com", comSenha: false);

        var clientes = await Servico(contexto).BuscarAsync(null, CancellationToken.None);

        Assert.True(clientes.Single(c => c.Nome == "Ana").TemSenha);
        Assert.False(clientes.Single(c => c.Nome == "Bruno").TemSenha);
    }

    // ----Cadastro ---------------------------------------------------------

    [Fact]
    public async Task CriarAsync_cadastra_sem_senha_e_com_papel_de_cliente()
    {
        using var contexto = Testes.CriarContextoInMemory();
        var servico = Servico(contexto);

        var cliente = await servico.CriarAsync(
            new RequisicaoDeCliente("Ana Souza", "Ana@Exemplo.com ", " 11988887777 ", null),
            CancellationToken.None);

        Assert.Equal("Ana Souza", cliente.Nome);
        Assert.Equal("ana@exemplo.com", cliente.Email);
        Assert.Equal("11988887777", cliente.Telefone);
        // Sem senha: a pessoa comprou na loja e não escolheu nenhuma.
        Assert.False(cliente.TemSenha);

        var gravado = await contexto.Usuarios.SingleAsync();
        Assert.Equal(PapelUsuario.Cliente, gravado.Papel);
        Assert.Null(gravado.HashSenha);
    }

    [Fact]
    public async Task CriarAsync_exige_nome()
    {
        using var contexto = Testes.CriarContextoInMemory();

        var erro = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Servico(contexto).CriarAsync(
                new RequisicaoDeCliente("  ", "ana@exemplo.com", "11988887777", null),
                CancellationToken.None));

        Assert.Contains("nome", erro.Message);
    }

    [Fact]
    public async Task CriarAsync_exige_email_valido()
    {
        using var contexto = Testes.CriarContextoInMemory();

        // O e-mail é por onde o cliente recebe o pedido. Cadastrar sem ele
        // deixaria a venda sem confirmação por escrito.
        var erro = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Servico(contexto).CriarAsync(
                new RequisicaoDeCliente("Ana", "ana", "11988887777", null),
                CancellationToken.None));

        Assert.Contains("e-mail", erro.Message);
    }

    [Fact]
    public async Task CriarAsync_exige_telefone_com_digitos()
    {
        using var contexto = Testes.CriarContextoInMemory();

        // É o telefone que fecha a venda de balcão. Aceitar cadastro sem ele
        // criaria um cliente que a loja nunca conseguiria contatar.
        var erro = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Servico(contexto).CriarAsync(
                new RequisicaoDeCliente("Ana", "ana@exemplo.com", "123", null),
                CancellationToken.None));

        Assert.Contains("telefone", erro.Message);
    }

    [Fact]
    public async Task CriarAsync_recusa_email_repetido()
    {
        using var contexto = Testes.CriarContextoInMemory();
        var servico = Servico(contexto);
        await servico.CriarAsync(
            new RequisicaoDeCliente("Ana", "ana@exemplo.com", "11988887777", null),
            CancellationToken.None);

        var erro = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            servico.CriarAsync(
                new RequisicaoDeCliente("Outra Ana", "ANA@exemplo.com", "11966665555", null),
                CancellationToken.None));

        // A tela de busca existe justamente para achar quem já é cliente antes de
        // cadastrar de novo; cadastrar duplicado quebraria o histórico de pedidos.
        Assert.Contains("ana@exemplo.com", erro.Message);
    }

    [Fact]
    public async Task CriarAsync_recusa_cpf_repetido()
    {
        using var contexto = Testes.CriarContextoInMemory();
        var servico = Servico(contexto);
        await servico.CriarAsync(
            new RequisicaoDeCliente("Ana", "ana@exemplo.com", "11988887777", "123.456.789-00"),
            CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            servico.CriarAsync(
                new RequisicaoDeCliente("Outra", "outra@exemplo.com", "11966665555", "123.456.789-00"),
                CancellationToken.None));
    }
}
