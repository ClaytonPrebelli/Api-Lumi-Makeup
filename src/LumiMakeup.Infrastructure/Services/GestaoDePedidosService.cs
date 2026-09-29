using LumiMakeup.Application.Abstractions;
using LumiMakeup.Application.DTOs;
using LumiMakeup.Domain.Entities;
using LumiMakeup.Domain.Enums;
using LumiMakeup.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LumiMakeup.Infrastructure.Services;

public sealed class GestaoDePedidosService : IGestaoDePedidosService
{
    private readonly LumiDbContext _contexto;
    private readonly IGestaoDeCuponsService _cupons;
    private readonly INotificadorDePedido _notificador;

    public GestaoDePedidosService(
        LumiDbContext contexto,
        IGestaoDeCuponsService cupons,
        INotificadorDePedido notificador)
    {
        _contexto = contexto;
        _cupons = cupons;
        _notificador = notificador;
    }

    public async Task<PedidoDto> CriarAsync(
        RequisicaoDePedido requisicao,
        OrigemPedido origem,
        CancellationToken cancellationToken = default)
    {
        if (requisicao.Itens.Count == 0)
        {
            throw new InvalidOperationException("O pedido precisa ter ao menos um item.");
        }

        if (requisicao.CustoFrete < 0 || requisicao.DistanciaKm < 0)
        {
            throw new InvalidOperationException("Frete e distância não podem ser negativos.");
        }

        // A regra de origem fica aqui, e nao no controller: e a mesma para o
        // checkout e para a tela de balcao, e um controller esquecendo de aplicar
        // deixaria passar venda de balcao com endereco, ou entrega cobrada ao
        // balcao.
        if (origem is OrigemPedido.Balcao)
        {
            if (requisicao.Endereco is not null)
            {
                throw new InvalidOperationException("Venda de balcão não tem endereço de entrega.");
            }

            if (requisicao.CustoFrete > 0)
            {
                throw new InvalidOperationException("Venda de balcão não tem frete.");
            }
        }
        else if (requisicao.Endereco is null)
        {
            throw new InvalidOperationException("Escolha o endereço de entrega.");
        }

        var usuario = await _contexto.Usuarios
            .FirstOrDefaultAsync(u => u.Id == requisicao.UsuarioId, cancellationToken)
            ?? throw new KeyNotFoundException("Cliente não encontrado.");

        // Data do pedido: a venda de balcao pode ser registrada depois de
        // acontecer, e e a data da venda que o relatorio precisa.
        var dataDoPedido = requisicao.CriadoEm ?? DateTime.UtcNow;

        var itens = await MontarItensAsync(requisicao.Itens, cancellationToken);

        var subtotal = itens.Sum(i => i.Subtotal);

        // O cupom e conferido contra o subtotal dos produtos, antes do frete entrar
        // na conta. E por isso que o parametro se chama subtotalDosProdutos: cupom
        // sobre frete subsidiaria o transporte em vez da mercadoria.
        var desconto = 0m;
        string? codigoDoCupom = null;

        if (!string.IsNullOrWhiteSpace(requisicao.CupomCodigo))
        {
            var aplicacao = await _cupons.CalcularAsync(
                requisicao.CupomCodigo,
                subtotal,
                dataDoPedido,
                cancellationToken);

            desconto = aplicacao.Desconto;
            codigoDoCupom = aplicacao.Codigo;
        }

        var total = Arredondar(subtotal - desconto + requisicao.CustoFrete);

        if (total < 0)
        {
            // Só alcançável com desconto de 100% sobre um subtotal menor que o
            // frete. Sem esta trava o total viraria negativo e a loja pagaria
            // entrega ao cliente.
            throw new InvalidOperationException("O total do pedido não pode ser negativo.");
        }

        var pedido = new Pedido
        {
            UsuarioId = usuario.Id,
            NomeCliente = usuario.Nome,
            DocumentoCliente = usuario.Cpf,
            TelefoneContato = usuario.Telefone,
            EmailContato = usuario.Email,
            Origem = origem,
            Status = StatusPedido.AguardandoPagamento,
            Subtotal = Arredondar(subtotal),
            Desconto = Arredondar(desconto),
            CupomCodigo = codigoDoCupom,
            CustoFrete = Arredondar(requisicao.CustoFrete),
            DistanciaKm = requisicao.DistanciaKm,
            Total = total,
            Observacoes = string.IsNullOrWhiteSpace(requisicao.Observacoes)
                ? null
                : requisicao.Observacoes.Trim(),
            // A nota fiscal entra pendente. Enquanto a emissao nao existir, e o que
            // mantem o pedido na fila do painel.
            NotaFiscalGerada = false,
            CriadoEm = dataDoPedido
        };

        if (requisicao.Endereco is { } endereco)
        {
            pedido.EnderecoCep = endereco.Cep;
            pedido.EnderecoLogradouro = endereco.Logradouro;
            pedido.EnderecoNumero = endereco.Numero;
            pedido.EnderecoComplemento = endereco.Complemento;
            pedido.EnderecoBairro = endereco.Bairro;
            pedido.EnderecoCidade = endereco.Cidade;
            pedido.EnderecoEstado = endereco.Estado;
        }

        foreach (var item in itens)
        {
            pedido.Itens.Add(item);
        }

        _contexto.Pedidos.Add(pedido);

        // A transicao precisa envolver pedido, estoque e cupom juntos. Sem ela, uma
        // falha depois da baixa de estoque deixaria o produto reservado sem pedido,
        // e o cliente receberia a confirmacao de uma compra que nao foi gravada.
        await using var transacao = await _contexto.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            await BaixarEstoqueAsync(itens, cancellationToken);

            if (codigoDoCupom is not null)
            {
                var cupom = await _contexto.Cupons
                    .FirstAsync(c => c.Codigo == codigoDoCupom, cancellationToken);

                await _cupons.ConsumirAsync(cupom.Id, cancellationToken);
            }

            await _contexto.SaveChangesAsync(cancellationToken);
            await transacao.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Estoque e quantidade de cupom sao tokens de concorrencia, entao o
            // conflito chega aqui quando outra compra levou a ultima unidade entre
            // a conferencia e a gravacao. Traduzir aqui evita que a tela mostre
            // "conflito de concorrencia" para quem compra.
            await transacao.RollbackAsync(cancellationToken);
            throw new InvalidOperationException(
                "Um dos itens do pedido foi vendido enquanto o carrinho estava aberto. Confira o carrinho e tente de novo.");
        }
        catch
        {
            await transacao.RollbackAsync(cancellationToken);
            throw;
        }

        var dto = await ObterPorIdAsync(pedido.Id, cancellationToken)
            ?? throw new InvalidOperationException("O pedido foi criado mas não pôde ser lido.");

        // Depois do commit, nunca antes. O aviso vai para fora do banco: se o SMTP
        // ou o WhatsApp falharem, o pedido existe e o aviso pode ser reenviado.
        // Ao contrario, avisar antes do commit mandaria o cliente confirmar uma
        // compra que ainda podia ser desfeita.
        await _notificador.PedidoCriadoAsync(dto, cancellationToken);

        return dto;
    }

    public async Task<PedidoDto?> ObterPorIdAsync(long id, CancellationToken cancellationToken = default)
    {
        return await _contexto.Pedidos
            .AsNoTracking()
            .Where(p => p.Id == id)
            .Select(p => new PedidoDto(
                p.Id,
                p.UsuarioId,
                p.NomeCliente,
                p.DocumentoCliente,
                p.TelefoneContato,
                p.EmailContato,
                p.Origem,
                p.Status,
                p.MetodoPagamento,
                p.CupomCodigo,
                p.Subtotal,
                p.Desconto,
                p.CustoFrete,
                p.Total,
                p.Observacoes,
                p.CriadoEm,
                p.PagoEm,
                p.Itens
                    .OrderBy(i => i.Id)
                    .Select(i => new PedidoItemDto(
                        i.ProdutoId,
                        i.NomeProdutoRegistrado,
                        i.Quantidade,
                        i.PrecoVendaUnitario,
                        i.PrecoPromocionalUnitario,
                        i.Subtotal))
                    .ToList()))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PedidoDto>> ListarDoUsuarioAsync(
        long usuarioId,
        CancellationToken cancellationToken = default)
    {
        // O filtro por UsuarioId é do lado do banco, e não depois: puxar todos e
        // filtrar em memória seria devolver a lista de pedidos dos outros clientes
        // para o dono do token antes de descartar.
        return await _contexto.Pedidos
            .AsNoTracking()
            .Where(p => p.UsuarioId == usuarioId)
            .OrderByDescending(p => p.CriadoEm)
            .ThenByDescending(p => p.Id)
            .Select(p => new PedidoDto(
                p.Id,
                p.UsuarioId,
                p.NomeCliente,
                p.DocumentoCliente,
                p.TelefoneContato,
                p.EmailContato,
                p.Origem,
                p.Status,
                p.MetodoPagamento,
                p.CupomCodigo,
                p.Subtotal,
                p.Desconto,
                p.CustoFrete,
                p.Total,
                p.Observacoes,
                p.CriadoEm,
                p.PagoEm,
                p.Itens
                    .OrderBy(i => i.Id)
                    .Select(i => new PedidoItemDto(
                        i.ProdutoId,
                        i.NomeProdutoRegistrado,
                        i.Quantidade,
                        i.PrecoVendaUnitario,
                        i.PrecoPromocionalUnitario,
                        i.Subtotal))
                    .ToList()))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PedidoListaDto>> ListarAsync(
        StatusPedido? status = null,
        OrigemPedido? origem = null,
        bool? notaFiscalGerada = null,
        CancellationToken cancellationToken = default)
    {
        var consulta = _contexto.Pedidos.AsNoTracking();

        if (status is { } statusInformado)
        {
            consulta = consulta.Where(p => p.Status == statusInformado);
        }

        if (origem is { } origemInformada)
        {
            consulta = consulta.Where(p => p.Origem == origemInformada);
        }

        if (notaFiscalGerada is { } emitida)
        {
            consulta = consulta.Where(p => p.NotaFiscalGerada == emitida);
        }

        return await consulta
            .OrderByDescending(p => p.CriadoEm)
            .ThenByDescending(p => p.Id)
            .Select(p => new PedidoListaDto(
                p.Id,
                p.NomeCliente,
                p.TelefoneContato,
                p.Origem,
                p.Status,
                p.MetodoPagamento,
                p.CupomCodigo,
                p.Total,
                p.NotaFiscalGerada,
                p.CriadoEm,
                p.Itens.Count))
            .ToListAsync(cancellationToken);
    }

    public async Task<PedidoDto> RegistrarPagamentoAsync(
        long id,
        MetodoPagamento metodoPagamento,
        CancellationToken cancellationToken = default)
    {
        var pedido = await _contexto.Pedidos
            .Include(p => p.Itens)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("Pedido não encontrado.");

        if (pedido.Status is StatusPedido.Cancelado)
        {
            throw new InvalidOperationException("Este pedido está cancelado e não pode ser pago.");
        }

        if (pedido.Status is StatusPedido.Pago)
        {
            throw new InvalidOperationException("Este pedido já está pago.");
        }

        pedido.MetodoPagamento = metodoPagamento;
        pedido.Status = StatusPedido.Pago;
        pedido.PagoEm = DateTime.UtcNow;

        await _contexto.SaveChangesAsync(cancellationToken);

        return await ObterPorIdAsync(pedido.Id, cancellationToken)
            ?? throw new InvalidOperationException("O pedido foi atualizado mas não pôde ser lido.");
    }

    public async Task<PedidoDto> CancelarAsync(long id, CancellationToken cancellationToken = default)
    {
        var pedido = await _contexto.Pedidos
            .Include(p => p.Itens)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("Pedido não encontrado.");

        if (pedido.Status is StatusPedido.Cancelado)
        {
            throw new InvalidOperationException("Este pedido já está cancelado.");
        }

        if (pedido.Status is StatusPedido.Pago)
        {
            // Cancelar um pedido pago e devolver merchandise, nao um erro de estado.
            // Enquanto nao existir o fluxo de devolucao, barrar e pedir para tratar
            // a mao evita que o estoque suba sem o dinheiro de volta.
            throw new InvalidOperationException(
                "Este pedido já está pago. Trate a devolução separadamente antes de cancelar.");
        }

        await using var transacao = await _contexto.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            // Devolve o estoque antes de marcar como cancelado, para que uma falha
            // no meio deixe o pedido como estava, e nao um pedido cancelado com
            // produto ainda reservado.
            await DevolverEstoqueAsync(pedido.Itens, cancellationToken);

            if (!string.IsNullOrWhiteSpace(pedido.CupomCodigo))
            {
                var cupom = await _contexto.Cupons
                    .FirstOrDefaultAsync(c => c.Codigo == pedido.CupomCodigo, cancellationToken);

                if (cupom is not null)
                {
                    await _cupons.DevolverAsync(cupom.Id, cancellationToken);
                }
            }

            pedido.Status = StatusPedido.Cancelado;
            await _contexto.SaveChangesAsync(cancellationToken);
            await transacao.CommitAsync(cancellationToken);
        }
        catch
        {
            await transacao.RollbackAsync(cancellationToken);
            throw;
        }

        return await ObterPorIdAsync(pedido.Id, cancellationToken)
            ?? throw new InvalidOperationException("O pedido foi cancelado mas não pôde ser lido.");
    }

    /// <summary>
    /// Monta os itens já com o preço que vai ser cobrado.
    ///
    /// Os produtos são carregados **sem rastreamento**: o que o pedido precisa deles
    /// é o nome e os preços do momento da compra, e tudo isso vai copiado para o
    /// <see cref="ItemPedido"/>. O estoque é tratado separadamente, em
    /// <see cref="BaixarEstoqueAsync"/>.
    /// </summary>
    private async Task<List<ItemPedido>> MontarItensAsync(
        IReadOnlyList<ItemDePedidoRequisicao> requisitados,
        CancellationToken cancellationToken)
    {
        if (requisitados.Any(i => i.Quantidade <= 0))
        {
            throw new InvalidOperationException("A quantidade de cada item precisa ser maior que zero.");
        }

        var ids = requisitados.Select(i => i.ProdutoId).Distinct().ToList();

        var produtos = await _contexto.Produtos
            .AsNoTracking()
            .Where(p => ids.Contains(p.Id))
            .ToListAsync(cancellationToken);

        if (produtos.Count != ids.Count)
        {
            var inexistentes = ids.Where(id => produtos.All(p => p.Id != id));
            throw new InvalidOperationException(
                $"Produto não encontrado: {string.Join(", ", inexistentes)}.");
        }

        var inativos = produtos.Where(p => !p.Ativo).Select(p => p.Nome).ToList();

        if (inativos.Count > 0)
        {
            throw new InvalidOperationException(
                $"Produto indisponível: {string.Join(", ", inativos)}.");
        }

        var itens = new List<ItemPedido>();

        foreach (var requisitado in requisitados)
        {
            var produto = produtos.First(p => p.Id == requisitado.ProdutoId);

            if (produto.QuantidadeEstoque < requisitado.Quantidade)
            {
                throw new InvalidOperationException(
                    $"Estoque insuficiente de {produto.Nome}. Disponível: {produto.QuantidadeEstoque}.");
            }

            // O preco promocional tem precedencia sobre o de venda, e os dois sao
            // gravados. Guardar so o cobrado impediria o relatorio de dizer quanto
            // foi discounting de promocao alem do cupom.
            var precoPraticado = produto.PrecoPromocional ?? produto.PrecoVenda;

            itens.Add(new ItemPedido
            {
                ProdutoId = produto.Id,
                NomeProdutoRegistrado = produto.Nome,
                PrecoCustoUnitario = produto.PrecoCusto,
                PrecoVendaUnitario = produto.PrecoVenda,
                PrecoPromocionalUnitario = produto.PrecoPromocional,
                Quantidade = requisitado.Quantidade,
                Subtotal = Arredondar(precoPraticado * requisitado.Quantidade)
            });
        }

        return itens;
    }

    private async Task BaixarEstoqueAsync(List<ItemPedido> itens, CancellationToken cancellationToken)
    {
        var porProduto = itens
            .GroupBy(i => i.ProdutoId)
            .Select(g => new { ProdutoId = g.Key, Quantidade = g.Sum(i => i.Quantidade) })
            .ToList();

        // Somar por produto antes de baixar evita que duas linhas do mesmo produto
        // validassem 3 + 3 contra um estoque de 5 e o deixassem negativo.
        foreach (var item in porProduto)
        {
            var produto = await _contexto.Produtos
                .FirstOrDefaultAsync(p => p.Id == item.ProdutoId, cancellationToken);

            if (produto is null)
            {
                throw new KeyNotFoundException($"Produto não encontrado: {item.ProdutoId}.");
            }

            produto.QuantidadeEstoque -= item.Quantidade;
        }
    }

    private async Task DevolverEstoqueAsync(ICollection<ItemPedido> itens, CancellationToken cancellationToken)
    {
        var porProduto = itens
            .GroupBy(i => i.ProdutoId)
            .Select(g => new { ProdutoId = g.Key, Quantidade = g.Sum(i => i.Quantidade) })
            .ToList();

        foreach (var item in porProduto)
        {
            var produto = await _contexto.Produtos
                .FirstOrDefaultAsync(p => p.Id == item.ProdutoId, cancellationToken);

            if (produto is not null)
            {
                produto.QuantidadeEstoque += item.Quantidade;
            }
        }

        await _contexto.SaveChangesAsync(cancellationToken);
    }

    private static decimal Arredondar(decimal valor) => Math.Round(valor, 2, MidpointRounding.AwayFromZero);
}
