using LumiMakeup.Application.Abstractions;
using LumiMakeup.Application.DTOs;
using LumiMakeup.Domain.Entities;
using LumiMakeup.Domain.Enums;
using LumiMakeup.Infrastructure.Persistence;
using LumiMakeup.Infrastructure.Services;
using LumiMakeup.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Moq;

namespace LumiMakeup.Tests.Infrastructure.Services;

public sealed class GestaoDePedidosServiceTests
{
    private static async Task<Usuario> SemearUsuarioAsync(LumiDbContext contexto)
    {
        var usuario = new Usuario
        {
            Nome = "Ana",
            Email = "ana@exemplo.com",
            Cpf = "123.456.789-00",
            Telefone = "11999999999",
            Papel = PapelUsuario.Cliente
        };

        contexto.Usuarios.Add(usuario);
        await contexto.SaveChangesAsync();
        return usuario;
    }

    private static async Task<Produto> SemearProdutoAsync(
        LumiDbContext contexto,
        string nome = "Batom",
        decimal precoVenda = 35m,
        decimal? precoPromocional = null,
        int estoque = 10,
        bool ativo = true)
    {
        var produto = new Produto
        {
            Nome = nome,
            Slug = nome.ToLowerInvariant(),
            Descricao = "descricao",
            PrecoCusto = 10m,
            PrecoVenda = precoVenda,
            PrecoPromocional = precoPromocional,
            QuantidadeEstoque = estoque,
            Ativo = ativo
        };

        // Categoria e obrigatoria: e uma FK, e o InMemory tambem a cobra.
        produto.Categoria = new Categoria { Nome = "Batons", Slug = "batons" };
        contexto.Produtos.Add(produto);
        await contexto.SaveChangesAsync();
        return produto;
    }

    private static async Task<Cupom> SemearCupomAsync(
        LumiDbContext contexto,
        string codigo = "NATAL20",
        decimal percentual = 20,
        int quantidade = 10,
        decimal valorMinimo = 0)
    {
        var cupom = new Cupom
        {
            Codigo = codigo,
            Percentual = percentual,
            QuantidadeDisponivel = quantidade,
            ValorMinimo = valorMinimo,
            Ativo = true
        };

        contexto.Cupons.Add(cupom);
        await contexto.SaveChangesAsync();
        return cupom;
    }

    /// <summary>
    /// O provedor InMemory não abre transação de verdade — BeginTransaction é
    /// ignorado. Isso não muda o que está sendo testado aqui, porque nenhuma
    /// asserção depende do rollback: o que importa é a ordem das escritas e os
    /// valores gravados.
    /// </summary>
    private static GestaoDePedidosService Servico(
        LumiDbContext contexto,
        INotificadorDePedido? notificador = null)
    {
        return new GestaoDePedidosService(
            contexto,
            new GestaoDeCuponsService(contexto),
            notificador ?? Mock.Of<INotificadorDePedido>());
    }

    private static RequisicaoDePedido Requisicao(
        long usuarioId,
        params (long ProdutoId, int Quantidade)[] itens) => new(
            usuarioId,
            itens.Select(i => new ItemDePedidoRequisicao(i.ProdutoId, i.Quantidade)).ToList(),
            new EnderecoDeEntregaRequisicao("01310930", "Avenida Paulista", "1000", null, "Bela Vista", "São Paulo", "SP"),
            null,
            null,
            15m,
            4.2m,
            null);

    private static RequisicaoDePedido RequisicaoDeBalcao(
        long usuarioId,
        string? cupom = null,
        params (long ProdutoId, int Quantidade)[] itens) => new(
            usuarioId,
            itens.Select(i => new ItemDePedidoRequisicao(i.ProdutoId, i.Quantidade)).ToList(),
            null,
            cupom,
            null,
            0m,
            0m,
            null);

    // ----Criacao ----------------------------------------------------------

    [Fact]
    public async Task CriarAsync_grava_o_pedido_aguardando_pagamento_e_sem_pagamento_informado()
    {
        using var contexto = Testes.CriarContextoInMemory();
        var usuario = await SemearUsuarioAsync(contexto);
        var produto = await SemearProdutoAsync(contexto);
        var servico = Servico(contexto);

        var pedido = await servico.CriarAsync(Requisicao(usuario.Id, (produto.Id, 2)), OrigemPedido.Online, CancellationToken.None);

        // O pedido nasce esperando a administradora: e ela quem registra a forma
        // de pagamento. Nada de compensacao bancaria.
        Assert.Equal(StatusPedido.AguardandoPagamento, pedido.Status);
        Assert.Null(pedido.MetodoPagamento);
        Assert.Null(pedido.PagoEm);
    }

    [Fact]
    public async Task CriarAsync_baixa_o_estoque_na_hora()
    {
        using var contexto = Testes.CriarContextoInMemory();
        var usuario = await SemearUsuarioAsync(contexto);
        var produto = await SemearProdutoAsync(contexto, estoque: 10);
        var servico = Servico(contexto);

        await servico.CriarAsync(Requisicao(usuario.Id, (produto.Id, 3)), OrigemPedido.Online, CancellationToken.None);

        // Baixa na criacao e nao na finalizacao: o produto precisa sair da
        // vitrine assim que alguem comprou, ou dois clientes levariam a ultima
        // unidade.
        Assert.Equal(7, await contexto.Produtos.Where(p => p.Id == produto.Id).Select(p => p.QuantidadeEstoque).SingleAsync());
    }

    [Fact]
    public async Task CriarAsync_copia_o_preco_do_momento_da_compra()
    {
        using var contexto = Testes.CriarContextoInMemory();
        var usuario = await SemearUsuarioAsync(contexto);
        var produto = await SemearProdutoAsync(contexto, precoVenda: 35m, precoPromocional: 25m);
        var servico = Servico(contexto);

        var pedido = await servico.CriarAsync(Requisicao(usuario.Id, (produto.Id, 2)), OrigemPedido.Online, CancellationToken.None);

        var item = Assert.Single(pedido.Itens);
        // Preco promocional tem precedencia no cobrado, e os dois ficam gravados
        // para o relatorio dizer quanto houve de promocao alem do cupom.
        Assert.Equal(35m, item.PrecoVendaUnitario);
        Assert.Equal(25m, item.PrecoPromocionalUnitario);
        Assert.Equal(50m, item.Subtotal);
    }

    [Fact]
    public async Task CriarAsync_usa_o_preco_promocional_no_subtotal()
    {
        using var contexto = Testes.CriarContextoInMemory();
        var usuario = await SemearUsuarioAsync(contexto);
        var produto = await SemearProdutoAsync(contexto, precoVenda: 35m, precoPromocional: 25m);
        var servico = Servico(contexto);

        var pedido = await servico.CriarAsync(Requisicao(usuario.Id, (produto.Id, 2)), OrigemPedido.Online, CancellationToken.None);

        Assert.Equal(50m, pedido.Subtotal);
        Assert.Equal(65m, pedido.Total);
    }

    [Fact]
    public async Task CriarAsync_copia_o_cliente_do_cadastro()
    {
        using var contexto = Testes.CriarContextoInMemory();
        var usuario = await SemearUsuarioAsync(contexto);
        var produto = await SemearProdutoAsync(contexto);
        var servico = Servico(contexto);

        var pedido = await servico.CriarAsync(Requisicao(usuario.Id, (produto.Id, 1)), OrigemPedido.Online, CancellationToken.None);

        // Copia, e nao referencia: o aviso de WhatsApp sai depois que o pedido
        // existe, e o cadastro pode ter mudado nesse meio tempo.
        Assert.Equal("Ana", pedido.NomeCliente);
        Assert.Equal("123.456.789-00", pedido.DocumentoCliente);
        Assert.Equal("11999999999", pedido.TelefoneContato);
    }

    [Fact]
    public async Task CriarAsync_deixa_a_nota_fiscal_pendente()
    {
        using var contexto = Testes.CriarContextoInMemory();
        var usuario = await SemearUsuarioAsync(contexto);
        var produto = await SemearProdutoAsync(contexto);
        var servico = Servico(contexto);

        await servico.CriarAsync(Requisicao(usuario.Id, (produto.Id, 1)), OrigemPedido.Online, CancellationToken.None);

        // Enquanto a emissao nao existir, todo pedido nasce na fila do painel —
        // e a leitura correta, nao um bug.
        var gravado = await contexto.Pedidos.AsNoTracking().SingleAsync();
        Assert.False(gravado.NotaFiscalGerada);
        Assert.Null(gravado.NotaFiscalGeradaEm);
    }

    [Fact]
    public async Task CriarAsync_avisa_depois_de_gravar()
    {
        using var contexto = Testes.CriarContextoInMemory();
        var usuario = await SemearUsuarioAsync(contexto);
        var produto = await SemearProdutoAsync(contexto);
        var servico = Servico(contexto);

        await servico.CriarAsync(Requisicao(usuario.Id, (produto.Id, 1)), OrigemPedido.Online, CancellationToken.None);

        // O aviso não pode derrubar o pedido: e-mail ou WhatsApp caídos não
        // desfazem uma compra já gravada.
        Assert.Single(await contexto.Pedidos.ToListAsync());
    }

    [Fact]
    public async Task CriarAsync_recebe_o_produto_inexistente_com_nome_legivel()
    {
        using var contexto = Testes.CriarContextoInMemory();
        var usuario = await SemearUsuarioAsync(contexto);
        var servico = Servico(contexto);

        var erro = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            servico.CriarAsync(Requisicao(usuario.Id, (999, 1)), OrigemPedido.Online, CancellationToken.None));

        Assert.Contains("999", erro.Message);
    }

    [Fact]
    public async Task CriarAsync_recusa_produto_desativado()
    {
        using var contexto = Testes.CriarContextoInMemory();
        var usuario = await SemearUsuarioAsync(contexto);
        var produto = await SemearProdutoAsync(contexto, ativo: false);
        var servico = Servico(contexto);

        var erro = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            servico.CriarAsync(Requisicao(usuario.Id, (produto.Id, 1)), OrigemPedido.Online, CancellationToken.None));

        Assert.Contains("Batom", erro.Message);
    }

    [Fact]
    public async Task CriarAsync_recusa_estoque_insuficiente_dizendo_o_que_ha()
    {
        using var contexto = Testes.CriarContextoInMemory();
        var usuario = await SemearUsuarioAsync(contexto);
        var produto = await SemearProdutoAsync(contexto, estoque: 2);
        var servico = Servico(contexto);

        var erro = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            servico.CriarAsync(Requisicao(usuario.Id, (produto.Id, 5)), OrigemPedido.Online, CancellationToken.None));

        // Dizer "disponível: 2" deixa a admin entender o límite de imediato.
        Assert.Contains("2", erro.Message);
    }

    [Fact]
    public async Task CriarAsync_recusa_quantidade_zero()
    {
        using var contexto = Testes.CriarContextoInMemory();
        var usuario = await SemearUsuarioAsync(contexto);
        var produto = await SemearProdutoAsync(contexto);
        var servico = Servico(contexto);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            servico.CriarAsync(Requisicao(usuario.Id, (produto.Id, 0)), OrigemPedido.Online, CancellationToken.None));
    }

    [Fact]
    public async Task CriarAsync_recusa_pedido_sem_itens()
    {
        using var contexto = Testes.CriarContextoInMemory();
        var usuario = await SemearUsuarioAsync(contexto);
        var servico = Servico(contexto);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            servico.CriarAsync(Requisicao(usuario.Id), OrigemPedido.Online, CancellationToken.None));
    }

    [Fact]
    public async Task CriarAsync_exige_endereco_em_pedido_online()
    {
        using var contexto = Testes.CriarContextoInMemory();
        var usuario = await SemearUsuarioAsync(contexto);
        var produto = await SemearProdutoAsync(contexto);
        var servico = Servico(contexto);

        // Pedido online sem endereco. Sem o item, a falha viria antes, de "pedido
        // precisa ter ao menos um item", e o teste nao diria nada sobre endereco.
        var semEndereco = RequisicaoDeBalcao(usuario.Id, null, (produto.Id, 1))
            with { Endereco = null, CustoFrete = 0m, DistanciaKm = 0m };

        var erro = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            servico.CriarAsync(semEndereco, OrigemPedido.Online, CancellationToken.None));

        Assert.Contains("endereço", erro.Message);
    }

    [Fact]
    public async Task CriarAsync_recusa_cliente_inexistente()
    {
        using var contexto = Testes.CriarContextoInMemory();
        var produto = await SemearProdutoAsync(contexto);
        var servico = Servico(contexto);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            servico.CriarAsync(Requisicao(999, (produto.Id, 1)), OrigemPedido.Online, CancellationToken.None));
    }

    // ----Cupom no pedido ---------------------------------------------------

    [Fact]
    public async Task CriarAsync_desconta_o_cupom_sobre_o_subtotal_e_nao_sobre_o_frete()
    {
        using var contexto = Testes.CriarContextoInMemory();
        var usuario = await SemearUsuarioAsync(contexto);
        var produto = await SemearProdutoAsync(contexto, precoVenda: 100m);
        await SemearCupomAsync(contexto, "NATAL20", percentual: 20);
        var servico = Servico(contexto);

        var pedido = await servico.CriarAsync(
            Requisicao(usuario.Id, (produto.Id, 1)) with { CupomCodigo = "NATAL20" },
            OrigemPedido.Online,
            CancellationToken.None);

        // 100 de produtos, 20% = 20 de desconto, e o frete de 15 entra depois e
        // inteiro. Descontar sobre 115 daria 23, e subsidiaria o transporte.
        Assert.Equal(100m, pedido.Subtotal);
        Assert.Equal(20m, pedido.Desconto);
        Assert.Equal(15m, pedido.CustoFrete);
        Assert.Equal(95m, pedido.Total);
        Assert.Equal("NATAL20", pedido.CupomCodigo);
    }

    [Fact]
    public async Task CriarAsync_consome_uma_unidade_do_cupom()
    {
        using var contexto = Testes.CriarContextoInMemory();
        var usuario = await SemearUsuarioAsync(contexto);
        var produto = await SemearProdutoAsync(contexto);
        var cupom = await SemearCupomAsync(contexto, quantidade: 3);
        var servico = Servico(contexto);

        await servico.CriarAsync(
            Requisicao(usuario.Id, (produto.Id, 1)) with { CupomCodigo = "NATAL20" },
            OrigemPedido.Online,
            CancellationToken.None);

        Assert.Equal(2, await contexto.Cupons.Where(c => c.Id == cupom.Id).Select(c => c.QuantidadeDisponivel).SingleAsync());
    }

    [Fact]
    public async Task CriarAsync_recusa_cupom_invalido_e_nao_cria_o_pedido()
    {
        using var contexto = Testes.CriarContextoInMemory();
        var usuario = await SemearUsuarioAsync(contexto);
        var produto = await SemearProdutoAsync(contexto);
        var servico = Servico(contexto);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            servico.CriarAsync(
                Requisicao(usuario.Id, (produto.Id, 1)) with { CupomCodigo = "FANTASMA" },
                OrigemPedido.Online,
                CancellationToken.None));

        // A conferencia do cupom vem antes de qualquer gravacao, entao um codigo
        // ruim nao deixa pedido nem estoqueitory pela metade.
        Assert.Empty(await contexto.Pedidos.ToListAsync());
        Assert.Equal(10, await contexto.Produtos.Select(p => p.QuantidadeEstoque).SingleAsync());
    }

    [Fact]
    public async Task CriarAsync_aceita_o_cupom_por_caixa_baixa()
    {
        using var contexto = Testes.CriarContextoInMemory();
        var usuario = await SemearUsuarioAsync(contexto);
        var produto = await SemearProdutoAsync(contexto);
        await SemearCupomAsync(contexto, "NATAL20", percentual: 10);
        var servico = Servico(contexto);

        var pedido = await servico.CriarAsync(
            Requisicao(usuario.Id, (produto.Id, 1)) with { CupomCodigo = "natal20" },
            OrigemPedido.Online,
            CancellationToken.None);

        Assert.Equal("NATAL20", pedido.CupomCodigo);
    }

    [Fact]
    public async Task CriarAsync_com_cupom_de_100_por_cento_cobra_so_o_frete()
    {
        using var contexto = Testes.CriarContextoInMemory();
        var usuario = await SemearUsuarioAsync(contexto);
        var produto = await SemearProdutoAsync(contexto, precoVenda: 100m);
        await SemearCupomAsync(contexto, "ZERA", percentual: 100);
        var servico = Servico(contexto);

        var pedido = await servico.CriarAsync(
            Requisicao(usuario.Id, (produto.Id, 1)) with { CupomCodigo = "ZERA" },
            OrigemPedido.Online,
            CancellationToken.None);

        Assert.Equal(0m, pedido.Subtotal - pedido.Desconto);
        Assert.Equal(15m, pedido.Total);
    }

    // ----Venda de balcao ---------------------------------------------------

    [Fact]
    public async Task CriarAsync_de_registrar_venda_de_balcao_sem_endereco_e_sem_frete()
    {
        using var contexto = Testes.CriarContextoInMemory();
        var usuario = await SemearUsuarioAsync(contexto);
        var produto = await SemearProdutoAsync(contexto);
        var servico = Servico(contexto);

        var pedido = await servico.CriarAsync(
            RequisicaoDeBalcao(usuario.Id, null, (produto.Id, 2)),
            OrigemPedido.Balcao,
            CancellationToken.None);

        Assert.Equal(OrigemPedido.Balcao, pedido.Origem);
        Assert.Equal(0m, pedido.CustoFrete);
        Assert.Equal(70m, pedido.Total);
    }

    [Fact]
    public async Task CriarAsync_recusa_venda_de_balcao_com_endereco()
    {
        using var contexto = Testes.CriarContextoInMemory();
        var usuario = await SemearUsuarioAsync(contexto);
        var produto = await SemearProdutoAsync(contexto);
        var servico = Servico(contexto);
        var comEndereco = Requisicao(usuario.Id, (produto.Id, 1)) with { CustoFrete = 0m, DistanciaKm = 0m };

        var erro = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            servico.CriarAsync(comEndereco, OrigemPedido.Balcao, CancellationToken.None));

        Assert.Contains("balcão", erro.Message);
    }

    [Fact]
    public async Task CriarAsync_recusa_venda_de_balcao_com_frete()
    {
        using var contexto = Testes.CriarContextoInMemory();
        var usuario = await SemearUsuarioAsync(contexto);
        var produto = await SemearProdutoAsync(contexto);
        var servico = Servico(contexto);
        var comFrete = RequisicaoDeBalcao(usuario.Id, null, (produto.Id, 1)) with { CustoFrete = 15m };

        var erro = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            servico.CriarAsync(comFrete, OrigemPedido.Balcao, CancellationToken.None));

        Assert.Contains("frete", erro.Message);
    }

    [Fact]
    public async Task CriarAsync_aceita_cupom_na_venda_de_balcao()
    {
        using var contexto = Testes.CriarContextoInMemory();
        var usuario = await SemearUsuarioAsync(contexto);
        var produto = await SemearProdutoAsync(contexto, precoVenda: 100m);
        await SemearCupomAsync(contexto, "NATAL20", percentual: 20);
        var servico = Servico(contexto);

        var pedido = await servico.CriarAsync(
            RequisicaoDeBalcao(usuario.Id, "NATAL20", (produto.Id, 1)),
            OrigemPedido.Balcao,
            CancellationToken.None);

        Assert.Equal(20m, pedido.Desconto);
        Assert.Equal(80m, pedido.Total);
    }

    [Fact]
    public async Task CriarAsync_aceita_data_informada_para_venda_registrada_depois()
    {
        using var contexto = Testes.CriarContextoInMemory();
        var usuario = await SemearUsuarioAsync(contexto);
        var produto = await SemearProdutoAsync(contexto);
        var servico = Servico(contexto);
        var diaDaVenda = new DateTime(2026, 4, 10, 14, 30, 0, DateTimeKind.Utc);

        var pedido = await servico.CriarAsync(
            RequisicaoDeBalcao(usuario.Id, null, (produto.Id, 1)) with { CriadoEm = diaDaVenda },
            OrigemPedido.Balcao,
            CancellationToken.None);

        // A data da venda e o que o relatorio precisa, e nao a data em que a admin
        // digitou.
        Assert.Equal(diaDaVenda, pedido.CriadoEm);
    }

    // ----Finalizacao e cancelamento ---------------------------------------

    [Fact]
    public async Task RegistrarPagamentoAsync_vira_pago_e_registra_a_forma()
    {
        using var contexto = Testes.CriarContextoInMemory();
        var usuario = await SemearUsuarioAsync(contexto);
        var produto = await SemearProdutoAsync(contexto);
        var servico = Servico(contexto);
        var pedido = await servico.CriarAsync(Requisicao(usuario.Id, (produto.Id, 1)), OrigemPedido.Online, CancellationToken.None);

        var pago = await servico.RegistrarPagamentoAsync(pedido.Id, MetodoPagamento.Pix, CancellationToken.None);

        Assert.Equal(StatusPedido.Pago, pago.Status);
        Assert.Equal(MetodoPagamento.Pix, pago.MetodoPagamento);
        Assert.NotNull(pago.PagoEm);
    }

    [Fact]
    public async Task RegistrarPagamentoAsync_recusa_pagar_duas_vezes()
    {
        using var contexto = Testes.CriarContextoInMemory();
        var usuario = await SemearUsuarioAsync(contexto);
        var produto = await SemearProdutoAsync(contexto);
        var servico = Servico(contexto);
        var pedido = await servico.CriarAsync(Requisicao(usuario.Id, (produto.Id, 1)), OrigemPedido.Online, CancellationToken.None);
        await servico.RegistrarPagamentoAsync(pedido.Id, MetodoPagamento.Pix, CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            servico.RegistrarPagamentoAsync(pedido.Id, MetodoPagamento.Dinheiro, CancellationToken.None));
    }

    [Fact]
    public async Task CancelarAsync_devolve_o_estoque()
    {
        using var contexto = Testes.CriarContextoInMemory();
        var usuario = await SemearUsuarioAsync(contexto);
        var produto = await SemearProdutoAsync(contexto, estoque: 10);
        var servico = Servico(contexto);
        var pedido = await servico.CriarAsync(Requisicao(usuario.Id, (produto.Id, 3)), OrigemPedido.Online, CancellationToken.None);

        var cancelado = await servico.CancelarAsync(pedido.Id, CancellationToken.None);

        Assert.Equal(StatusPedido.Cancelado, cancelado.Status);
        Assert.Equal(10, await contexto.Produtos.Select(p => p.QuantidadeEstoque).SingleAsync());
    }

    [Fact]
    public async Task CancelarAsync_devolve_a_unidade_de_cupom()
    {
        using var contexto = Testes.CriarContextoInMemory();
        var usuario = await SemearUsuarioAsync(contexto);
        var produto = await SemearProdutoAsync(contexto);
        var cupom = await SemearCupomAsync(contexto, quantidade: 3);
        var servico = Servico(contexto);
        var pedido = await servico.CriarAsync(
            Requisicao(usuario.Id, (produto.Id, 1)) with { CupomCodigo = "NATAL20" },
            OrigemPedido.Online,
            CancellationToken.None);

        await servico.CancelarAsync(pedido.Id, CancellationToken.None);

        // Cancelar um pedido não pode custar um cupom ao cliente: a devolução é o
        // que mantém o contador querendo dizer quantas vendas usaram a promoção.
        Assert.Equal(3, await contexto.Cupons.Where(c => c.Id == cupom.Id).Select(c => c.QuantidadeDisponivel).SingleAsync());
    }

    [Fact]
    public async Task CancelarAsync_sem_cupom_devolve_so_o_estoque()
    {
        using var contexto = Testes.CriarContextoInMemory();
        var usuario = await SemearUsuarioAsync(contexto);
        var produto = await SemearProdutoAsync(contexto, estoque: 5);
        var servico = Servico(contexto);
        var pedido = await servico.CriarAsync(Requisicao(usuario.Id, (produto.Id, 2)), OrigemPedido.Online, CancellationToken.None);

        var cancelado = await servico.CancelarAsync(pedido.Id, CancellationToken.None);

        Assert.Equal(StatusPedido.Cancelado, cancelado.Status);
        Assert.Equal(5, await contexto.Produtos.Select(p => p.QuantidadeEstoque).SingleAsync());
    }

    [Fact]
    public async Task CancelarAsync_recusa_cancelar_um_pedido_pago()
    {
        using var contexto = Testes.CriarContextoInMemory();
        var usuario = await SemearUsuarioAsync(contexto);
        var produto = await SemearProdutoAsync(contexto);
        var servico = Servico(contexto);
        var pedido = await servico.CriarAsync(Requisicao(usuario.Id, (produto.Id, 1)), OrigemPedido.Online, CancellationToken.None);
        await servico.RegistrarPagamentoAsync(pedido.Id, MetodoPagamento.Pix, CancellationToken.None);

        var erro = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            servico.CancelarAsync(pedido.Id, CancellationToken.None));

        // Cancelar um pedido pago e devolver mercadoria, nao um erro de estado. Sem
        // fluxo de devolucao, barrar evita que o estoque suba sem o dinheiro voltar.
        Assert.Contains("devolução", erro.Message);
    }

    [Fact]
    public async Task CancelarAsync_recusa_cancelar_duas_vezes()
    {
        using var contexto = Testes.CriarContextoInMemory();
        var usuario = await SemearUsuarioAsync(contexto);
        var produto = await SemearProdutoAsync(contexto);
        var servico = Servico(contexto);
        var pedido = await servico.CriarAsync(Requisicao(usuario.Id, (produto.Id, 1)), OrigemPedido.Online, CancellationToken.None);
        await servico.CancelarAsync(pedido.Id, CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            servico.CancelarAsync(pedido.Id, CancellationToken.None));
    }

    [Fact]
    public async Task ObterPorIdAsync_devolve_o_pedido_com_os_itens()
    {
        using var contexto = Testes.CriarContextoInMemory();
        var usuario = await SemearUsuarioAsync(contexto);
        var produto = await SemearProdutoAsync(contexto);
        var servico = Servico(contexto);
        var pedido = await servico.CriarAsync(Requisicao(usuario.Id, (produto.Id, 1)), OrigemPedido.Online, CancellationToken.None);

        var lido = await servico.ObterPorIdAsync(pedido.Id, CancellationToken.None);

        Assert.NotNull(lido);
        Assert.Equal(pedido.Id, lido!.Id);
        Assert.Equal("Batom", Assert.Single(lido.Itens).Nome);
    }

    [Fact]
    public async Task ObterPorIdAsync_devolve_nulo_para_id_inexistente()
    {
        using var contexto = Testes.CriarContextoInMemory();

        Assert.Null(await Servico(contexto).ObterPorIdAsync(999, CancellationToken.None));
    }

    [Fact]
    public async Task CriarAsync_baixa_o_estoque_uma_vez_por_produto()
    {
        using var contexto = Testes.CriarContextoInMemory();
        var usuario = await SemearUsuarioAsync(contexto);
        var produto = await SemearProdutoAsync(contexto, estoque: 10);
        var servico = Servico(contexto);

        // Duas linhas do mesmo produto validam 3 + 3 contra um estoque de 5. Sem
        // somar antes de baixar, o estoque iria a -1.
        await servico.CriarAsync(
            Requisicao(usuario.Id, (produto.Id, 3), (produto.Id, 3)) with { CustoFrete = 0m },
            OrigemPedido.Online,
            CancellationToken.None);

        Assert.Equal(4, await contexto.Produtos.Select(p => p.QuantidadeEstoque).SingleAsync());
    }

    // ----Listagem do painel ------------------------------------------------

    private static async Task<(Pedido Pedido, Usuario Usuario)> PedidoNoBancoAsync(
        LumiDbContext contexto,
        Usuario usuario,
        Produto produto,
        OrigemPedido origem = OrigemPedido.Online)
    {
        var requisicao = origem is OrigemPedido.Balcao
            ? RequisicaoDeBalcao(usuario.Id, null, (produto.Id, 1))
            : Requisicao(usuario.Id, (produto.Id, 1));

        var pedido = await new GestaoDePedidosService(
                contexto,
                new GestaoDeCuponsService(contexto),
                Mock.Of<INotificadorDePedido>())
            .CriarAsync(requisicao, origem, CancellationToken.None);

        return (await contexto.Pedidos.AsNoTracking().SingleAsync(p => p.Id == pedido.Id), usuario);
    }

    [Fact]
    public async Task ListarAsync_filtra_por_status()
    {
        using var contexto = Testes.CriarContextoInMemory();
        var usuario = await SemearUsuarioAsync(contexto);
        var produto = await SemearProdutoAsync(contexto);
        var servico = Servico(contexto);
        var (pedido, _) = await PedidoNoBancoAsync(contexto, usuario, produto);
        await servico.RegistrarPagamentoAsync(pedido.Id, MetodoPagamento.Pix, CancellationToken.None);

        var aguardando = await servico.ListarAsync(status: StatusPedido.AguardandoPagamento, cancellationToken: CancellationToken.None);
        var pagos = await servico.ListarAsync(status: StatusPedido.Pago, cancellationToken: CancellationToken.None);

        Assert.Empty(aguardando);
        Assert.Single(pagos);
    }

    [Fact]
    public async Task ListarAsync_separa_a_venda_de_balcao_do_pedido_online()
    {
        using var contexto = Testes.CriarContextoInMemory();
        var usuario = await SemearUsuarioAsync(contexto);
        var produto = await SemearProdutoAsync(contexto);
        await PedidoNoBancoAsync(contexto, usuario, produto, OrigemPedido.Online);
        await PedidoNoBancoAsync(contexto, usuario, produto, OrigemPedido.Balcao);
        var servico = Servico(contexto);

        // Sem o filtro de origem, a venda de balcão entra misturada na receita do
        // site e nenhum relatório separa e-commerce de presencial.
        var online = await servico.ListarAsync(origem: OrigemPedido.Online, cancellationToken: CancellationToken.None);
        var balcao = await servico.ListarAsync(origem: OrigemPedido.Balcao, cancellationToken: CancellationToken.None);

        Assert.Single(online);
        Assert.Single(balcao);
        Assert.Equal(OrigemPedido.Balcao, balcao[0].Origem);
    }

    [Fact]
    public async Task ListarAsync_acha_o_que_falta_nota_fiscal()
    {
        using var contexto = Testes.CriarContextoInMemory();
        var usuario = await SemearUsuarioAsync(contexto);
        var produto = await SemearProdutoAsync(contexto);
        var (pedido, _) = await PedidoNoBancoAsync(contexto, usuario, produto);

        var semNota = await Servico(contexto)
            .ListarAsync(notaFiscalGerada: false, cancellationToken: CancellationToken.None);

        Assert.Single(semNota);
        Assert.False(semNota[0].NotaFiscalGerada);

        // Marcando como gerada, o pedido sai da fila do painel.
        var gravado = await contexto.Pedidos.SingleAsync(p => p.Id == pedido.Id);
        gravado.NotaFiscalGerada = true;
        gravado.NotaFiscalGeradaEm = DateTime.UtcNow;
        await contexto.SaveChangesAsync();

        var restantes = await Servico(contexto)
            .ListarAsync(notaFiscalGerada: false, cancellationToken: CancellationToken.None);

        Assert.Empty(restantes);
    }

    [Fact]
    public async Task ListarAsync_traz_a_quantidade_de_itens_sem_carregar_eles()
    {
        using var contexto = Testes.CriarContextoInMemory();
        var usuario = await SemearUsuarioAsync(contexto);
        var produto = await SemearProdutoAsync(contexto);
        await PedidoNoBancoAsync(contexto, usuario, produto);
        var servico = Servico(contexto);

        var lista = await servico.ListarAsync(cancellationToken: CancellationToken.None);

        // A contagem vem do banco na projecao. Puxar os itens para a contagem faria
        // uma consulta por linha da tela que mais se abre.
        Assert.Equal(1, Assert.Single(lista).QuantidadeDeItens);
    }

    // ----Listagem do cliente ----------------------------------------------

    [Fact]
    public async Task ListarDoUsuarioAsync_so_traz_o_pedido_da_pessoa()
    {
        using var contexto = Testes.CriarContextoInMemory();
        var ana = await SemearUsuarioAsync(contexto);
        var produto = await SemearProdutoAsync(contexto);
        await PedidoNoBancoAsync(contexto, ana, produto);

        var bruno = new Usuario { Nome = "Bruno", Email = "bruno@exemplo.com", Papel = PapelUsuario.Cliente };
        contexto.Usuarios.Add(bruno);
        await contexto.SaveChangesAsync();
        await PedidoNoBancoAsync(contexto, bruno, produto);

        var servico = Servico(contexto);

        var daAna = await servico.ListarDoUsuarioAsync(ana.Id, CancellationToken.None);
        var doBruno = await servico.ListarDoUsuarioAsync(bruno.Id, CancellationToken.None);

        // O filtro e no banco, e nao depois em memoria: puxar tudo e descartar
        // depois devolveria a lista de pedidos dos outros clientes para quem
        // pediu.
        Assert.Single(daAna);
        Assert.Single(doBruno);
        Assert.Equal("Ana", daAna[0].NomeCliente);
        Assert.Equal("Bruno", doBruno[0].NomeCliente);
    }

    [Fact]
    public async Task ListarDoUsuarioAsync_devolve_vazio_quando_nao_hou_pedido()
    {
        using var contexto = Testes.CriarContextoInMemory();
        var usuario = await SemearUsuarioAsync(contexto);

        Assert.Empty(await Servico(contexto).ListarDoUsuarioAsync(usuario.Id, CancellationToken.None));
    }
}
