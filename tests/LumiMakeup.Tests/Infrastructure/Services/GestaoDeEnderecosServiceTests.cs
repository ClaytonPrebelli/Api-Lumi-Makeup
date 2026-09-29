using LumiMakeup.Application.Abstractions;
using LumiMakeup.Application.DTOs;
using LumiMakeup.Domain.Entities;
using LumiMakeup.Domain.Enums;
using LumiMakeup.Infrastructure.Persistence;
using LumiMakeup.Infrastructure.Services;
using LumiMakeup.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace LumiMakeup.Tests.Infrastructure.Services;

public sealed class GestaoDeEnderecosServiceTests
{
    private static readonly ResultadoViaCep DoCep = new(
        "01310930",
        "Avenida Paulista",
        "Bela Vista",
        "São Paulo",
        "SP");

    private static async Task<Usuario> SemearUsuarioAsync(LumiDbContext contexto)
    {
        var usuario = new Usuario { Nome = "Ana", Email = "ana@exemplo.com", Papel = PapelUsuario.Cliente };
        contexto.Usuarios.Add(usuario);
        await contexto.SaveChangesAsync();
        return usuario;
    }

    /// <summary>Cep mockado, para o teste não depender da rede.</summary>
    private static Mock<IViaCepService> CepQueResponde(ResultadoViaCep? resultado)
    {
        var mock = new Mock<IViaCepService>();
        mock.Setup(c => c.ConsultarAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(resultado);
        return mock;
    }

    private static RequisicaoDeEnderecoDoPedido Requisicao(
        string cep = "01310-930",
        string numero = "1000",
        string? complemento = null) => new(cep, numero, complemento);

    [Fact]
    public async Task CriarAsync_complementa_o_endereco_pelo_cep()
    {
        using var contexto = Testes.CriarContextoInMemory();
        var usuario = await SemearUsuarioAsync(contexto);
        var servico = new GestaoDeEnderecosService(contexto, CepQueResponde(DoCep).Object);

        var endereco = await servico.CriarAsync(usuario.Id, Requisicao(), CancellationToken.None);

        // Bairro, cidade e estado vêm do CEP e não são pedidos: exigir que a
        // pessoa digite só cria chance de ela escrever diferente do registro.
        Assert.Equal("01310930", endereco.Cep);
        Assert.Equal("Avenida Paulista", endereco.Logradouro);
        Assert.Equal("Bela Vista", endereco.Bairro);
        Assert.Equal("São Paulo", endereco.Cidade);
        Assert.Equal("SP", endereco.Estado);
    }

    [Fact]
    public async Task CriarAsync_tira_o_cinco_do_cep()
    {
        using var contexto = Testes.CriarContextoInMemory();
        var usuario = await SemearUsuarioAsync(contexto);
        var servico = new GestaoDeEnderecosService(contexto, CepQueResponde(DoCep).Object);

        var endereco = await servico.CriarAsync(usuario.Id, Requisicao("01310-930"), CancellationToken.None);

        Assert.Equal("01310930", endereco.Cep);
    }

    [Fact]
    public async Task CriarAsync_guarda_o_complemento_digitado()
    {
        using var contexto = Testes.CriarContextoInMemory();
        var usuario = await SemearUsuarioAsync(contexto);
        var servico = new GestaoDeEnderecosService(contexto, CepQueResponde(DoCep).Object);

        var endereco = await servico.CriarAsync(usuario.Id, Requisicao(complemento: "Apto 42"), CancellationToken.None);

        // Apartamento e bloco não vêm do CEP: o ViaCEP devolve o logradouro "de
        // cima" e não sabe do número.
        Assert.Equal("Apto 42", endereco.Complemento);
    }

    [Fact]
    public async Task CriarAsync_marca_o_primeiro_como_padrao()
    {
        using var contexto = Testes.CriarContextoInMemory();
        var usuario = await SemearUsuarioAsync(contexto);
        var servico = new GestaoDeEnderecosService(contexto, CepQueResponde(DoCep).Object);

        var primeiro = await servico.CriarAsync(usuario.Id, Requisicao(), CancellationToken.None);
        var segundo = await servico.CriarAsync(usuario.Id, Requisicao(numero: "2000"), CancellationToken.None);

        // Deixar a agenda sem padrão forçaria o checkout a perguntar qual usar numa
        // lista de um item só.
        Assert.True(primeiro.Padrao);
        Assert.False(segundo.Padrao);
    }

    [Fact]
    public async Task ListarDoUsuarioAsync_traz_o_padrao_primeiro()
    {
        using var contexto = Testes.CriarContextoInMemory();
        var usuario = await SemearUsuarioAsync(contexto);
        var servico = new GestaoDeEnderecosService(contexto, CepQueResponde(DoCep).Object);

        await servico.CriarAsync(usuario.Id, Requisicao(), CancellationToken.None);
        await servico.CriarAsync(usuario.Id, Requisicao(numero: "2000"), CancellationToken.None);

        var agenda = await servico.ListarDoUsuarioAsync(usuario.Id, CancellationToken.None);

        Assert.Equal(2, agenda.Count);
        Assert.Equal("1000", agenda[0].Numero);
    }

    [Fact]
    public async Task ListarDoUsuarioAsync_nao_traz_o_endereco_de_outro_cliente()
    {
        using var contexto = Testes.CriarContextoInMemory();
        var ana = await SemearUsuarioAsync(contexto);
        var servico = new GestaoDeEnderecosService(contexto, CepQueResponde(DoCep).Object);
        await servico.CriarAsync(ana.Id, Requisicao(), CancellationToken.None);

        var bruno = new Usuario { Nome = "Bruno", Email = "bruno@exemplo.com", Papel = PapelUsuario.Cliente };
        contexto.Usuarios.Add(bruno);
        await contexto.SaveChangesAsync();

        Assert.Empty(await servico.ListarDoUsuarioAsync(bruno.Id, CancellationToken.None));
    }

    [Fact]
    public async Task CriarAsync_recusa_cep_que_o_viacep_nao_conhece()
    {
        using var contexto = Testes.CriarContextoInMemory();
        var usuario = await SemearUsuarioAsync(contexto);
        var servico = new GestaoDeEnderecosService(contexto, CepQueResponde(null).Object);

        var erro = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            servico.CriarAsync(usuario.Id, Requisicao("99999-999"), CancellationToken.None));

        // A mensagem diz o que fazer, e não só que falhou.
        Assert.Contains("CEP", erro.Message);
    }

    [Fact]
    public async Task CriarAsync_recusa_numero_vazio()
    {
        using var contexto = Testes.CriarContextoInMemory();
        var usuario = await SemearUsuarioAsync(contexto);
        var servico = new GestaoDeEnderecosService(contexto, CepQueResponde(DoCep).Object);

        var erro = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            servico.CriarAsync(usuario.Id, Requisicao(numero: "  "), CancellationToken.None));

        Assert.Contains("número", erro.Message);
    }

    [Fact]
    public async Task CriarAsync_recusa_cliente_inexistente()
    {
        using var contexto = Testes.CriarContextoInMemory();
        var servico = new GestaoDeEnderecosService(contexto, CepQueResponde(DoCep).Object);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            servico.CriarAsync(999, Requisicao(), CancellationToken.None));
    }

    [Fact]
    public async Task AtualizarNoPedidoAsync_troca_o_endereco_do_pedido_e_da_agenda()
    {
        using var contexto = Testes.CriarContextoInMemory();
        var usuario = await SemearUsuarioAsync(contexto);
        var servico = new GestaoDeEnderecosService(contexto, CepQueResponde(DoCep).Object);
        var agenda = await servico.CriarAsync(usuario.Id, Requisicao(), CancellationToken.None);

        var pedido = await PedidoNoBancoAsync(contexto, usuario.Id, agenda.Id);

        await servico.AtualizarNoPedidoAsync(
            usuario.Id,
            pedido.Id,
            Requisicao(numero: "2500"),
            CancellationToken.None);

        var gravado = await contexto.Pedidos.AsNoTracking().SingleAsync();
        Assert.Equal("2500", gravado.EnderecoNumero);

        // A agenda recebe a mesma correção, senão a próxima compra traria o
        // endereço antigo e a pessoa corrigiria de novo.
        Assert.Equal("2500", (await servico.ListarDoUsuarioAsync(usuario.Id, CancellationToken.None)).Single().Numero);
    }

    [Fact]
    public async Task AtualizarNoPedidoAsync_recusa_pedido_que_ja_saiu()
    {
        using var contexto = Testes.CriarContextoInMemory();
        var usuario = await SemearUsuarioAsync(contexto);
        var servico = new GestaoDeEnderecosService(contexto, CepQueResponde(DoCep).Object);
        var agenda = await servico.CriarAsync(usuario.Id, Requisicao(), CancellationToken.None);
        var pedido = await PedidoNoBancoAsync(contexto, usuario.Id, agenda.Id);

        var gravado = await contexto.Pedidos.SingleAsync();
        gravado.StatusEntrega = StatusEntrega.Enviado;
        await contexto.SaveChangesAsync();

        // A cópia no pedido é o registro do que foi entregue. Trocar depois mudaria
        // esse registro, e a pessoa não estaria mais no lugar que recebeu.
        var erro = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            servico.AtualizarNoPedidoAsync(usuario.Id, pedido.Id, Requisicao(numero: "2500"), CancellationToken.None));

        Assert.Contains("entrega", erro.Message);
    }

    [Fact]
    public async Task AtualizarNoPedidoAsync_recusa_pedido_de_outro_cliente()
    {
        using var contexto = Testes.CriarContextoInMemory();
        var ana = await SemearUsuarioAsync(contexto);
        var servico = new GestaoDeEnderecosService(contexto, CepQueResponde(DoCep).Object);
        var agenda = await servico.CriarAsync(ana.Id, Requisicao(), CancellationToken.None);
        var pedido = await PedidoNoBancoAsync(contexto, ana.Id, agenda.Id);

        var bruno = new Usuario { Nome = "Bruno", Email = "bruno@exemplo.com", Papel = PapelUsuario.Cliente };
        contexto.Usuarios.Add(bruno);
        await contexto.SaveChangesAsync();

        // O filtro de dono é o que impede um cliente de corrigir o endereço do
        // pedido de outro, o que exporia o lugar onde a pessoa recebe encomenda.
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            servico.AtualizarNoPedidoAsync(bruno.Id, pedido.Id, Requisicao(), CancellationToken.None));
    }

    [Fact]
    public async Task ExcluirAsync_apaga_da_agenda_sem_tocar_no_pedido()
    {
        using var contexto = Testes.CriarContextoInMemory();
        var usuario = await SemearUsuarioAsync(contexto);
        var servico = new GestaoDeEnderecosService(contexto, CepQueResponde(DoCep).Object);
        var agenda = await servico.CriarAsync(usuario.Id, Requisicao(), CancellationToken.None);
        await PedidoNoBancoAsync(contexto, usuario.Id, agenda.Id);

        await servico.ExcluirAsync(usuario.Id, agenda.Id, CancellationToken.None);

        Assert.Empty(await servico.ListarDoUsuarioAsync(usuario.Id, CancellationToken.None));

        // O pedido segue com o endereço: é a cópia imutável, e é ela que o
        // entregador usa.
        var pedido = await contexto.Pedidos.AsNoTracking().SingleAsync();
        Assert.Equal("Avenida Paulista", pedido.EnderecoLogradouro);
    }

    [Fact]
    public async Task ExcluirAsync_recusa_endereco_de_outro_cliente()
    {
        using var contexto = Testes.CriarContextoInMemory();
        var ana = await SemearUsuarioAsync(contexto);
        var servico = new GestaoDeEnderecosService(contexto, CepQueResponde(DoCep).Object);
        var agenda = await servico.CriarAsync(ana.Id, Requisicao(), CancellationToken.None);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            servico.ExcluirAsync(999, agenda.Id, CancellationToken.None));
    }

    /// <summary>
    /// Pedido pronto, com endereço copiado da agenda. Montado direto porque o
    /// serviço de pedido exige cliente com e-mail, e o teste é do endereço.
    /// </summary>
    private static async Task<Pedido> PedidoNoBancoAsync(LumiDbContext contexto, long usuarioId, long enderecoId)
    {
        var agenda = await contexto.Enderecos.AsNoTracking().SingleAsync(e => e.Id == enderecoId);

        var pedido = new Pedido
        {
            UsuarioId = usuarioId,
            NomeCliente = "Ana",
            EnderecoCep = agenda.Cep,
            EnderecoLogradouro = agenda.Logradouro,
            EnderecoNumero = agenda.Numero,
            EnderecoBairro = agenda.Bairro,
            EnderecoCidade = agenda.Cidade,
            EnderecoEstado = agenda.Estado,
            Origem = OrigemPedido.Online,
            Status = StatusPedido.AguardandoPagamento,
            Subtotal = 100m,
            Total = 100m,
            CriadoEm = DateTime.UtcNow
        };

        contexto.Pedidos.Add(pedido);
        await contexto.SaveChangesAsync();
        return pedido;
    }
}
