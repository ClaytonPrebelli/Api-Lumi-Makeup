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
    private readonly IGestaoDeProdutosService _produtos;
    private readonly IArmazenamentoDeImagens _armazenamento;

    private const string PastaDosComprovantes = "comprovantes";

    public GestaoDePedidosService(
        LumiDbContext contexto,
        IGestaoDeCuponsService cupons,
        INotificadorDePedido notificador,
        IGestaoDeProdutosService produtos,
        IArmazenamentoDeImagens armazenamento)
    {
        _contexto = contexto;
        _cupons = cupons;
        _notificador = notificador;
        _produtos = produtos;
        _armazenamento = armazenamento;
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

        // É só a sugestão do cliente, e o pedido continua aguardando: quem
        // confirma (ou troca) é a admin no aceite. Mas o valor precisa existir
        // no enum, senão um número inventado viraria forma de pagamento.
        if (requisicao.MetodoPagamento.HasValue && !Enum.IsDefined(requisicao.MetodoPagamento.Value))
        {
            throw new InvalidOperationException("Forma de pagamento inválida.");
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

            if (requisicao.CustoFrete > 0 || requisicao.DistanciaKm > 0)
            {
                throw new InvalidOperationException("Venda de balcão não tem frete.");
            }
        }
        else if (requisicao.Retirada)
        {
            // Retirada na loja é pedido online sem entrega: foi o cliente quem
            // comprou pelo site, então a origem continua Online e só o destino
            // e o frete ficam zerados.
            if (requisicao.Endereco is not null)
            {
                throw new InvalidOperationException("Retirada na loja não tem endereço de entrega.");
            }

            if (requisicao.CustoFrete > 0 || requisicao.DistanciaKm > 0)
            {
                throw new InvalidOperationException("Retirada na loja não tem frete.");
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
            // Sugestão do cliente, sem confirmar nada: o pedido continua
            // aguardando até a admin dar o aceite (e poder trocar a forma).
            MetodoPagamento = requisicao.MetodoPagamento,
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

        // A transação precisa envolver pedido, estoque, movimentos e cupom juntos.
        // Sem ela, uma falha depois da baixa de estoque deixaria o produto
        // reservado sem pedido, e o cliente receberia a confirmação de uma compra
        // que não foi gravada.
        await using var transacao = await _contexto.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            // Primeiro salva o pedido para obter o ID, que será usado como referência
            // nos movimentos de estoque.
            await _contexto.SaveChangesAsync(cancellationToken);

            // Cria os movimentos de saída de estoque com referência ao pedido.
            await RegistrarMovimentosSaidaAsync(pedido.Id, itens, cancellationToken);

            // Baixa o estoque dos produtos.
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
            // Estoque e quantidade de cupom são tokens de concorrência, então o
            // conflito chega aqui quando outra compra levou a última unidade entre
            // a conferência e a gravação. Traduzir aqui evita que a tela mostre
            // "conflito de concorrência" para quem compra.
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
        // compra que ainda podia ser desfeita. Na venda de balcão a
        // administradora pode pedir para não avisar.
        if (requisicao.AvisarCliente)
        {
            await _notificador.PedidoCriadoAsync(dto, cancellationToken);
        }

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
                p.EnderecoCep,
                p.EnderecoLogradouro,
                p.EnderecoNumero,
                p.EnderecoComplemento,
                p.EnderecoBairro,
                p.EnderecoCidade,
                p.EnderecoEstado,
                p.Itens
                    .OrderBy(i => i.Id)
                    .Select(i => new PedidoItemDto(
                        i.ProdutoId,
                        i.NomeProdutoRegistrado,
                        i.Quantidade,
                        i.PrecoVendaUnitario,
                        i.PrecoPromocionalUnitario,
                        i.Subtotal,
                        i.VarianteProdutoId,
                        i.VarianteNomeRegistrado ?? (i.VarianteProduto == null ? null : i.VarianteProduto.Nome)))
                    .ToList(),
                p.CaminhoComprovante,
                p.NomeOriginalComprovante,
                p.StatusEntrega,
                p.DistanciaKm,
                p.DespachadoEm,
                p.EntregueEm,
                p.EntreguePorNome))
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
                p.EnderecoCep,
                p.EnderecoLogradouro,
                p.EnderecoNumero,
                p.EnderecoComplemento,
                p.EnderecoBairro,
                p.EnderecoCidade,
                p.EnderecoEstado,
                p.Itens
                    .OrderBy(i => i.Id)
                    .Select(i => new PedidoItemDto(
                        i.ProdutoId,
                        i.NomeProdutoRegistrado,
                        i.Quantidade,
                        i.PrecoVendaUnitario,
                        i.PrecoPromocionalUnitario,
                        i.Subtotal,
                        i.VarianteProdutoId,
                        i.VarianteNomeRegistrado ?? (i.VarianteProduto == null ? null : i.VarianteProduto.Nome)))
                    .ToList(),
                p.CaminhoComprovante,
                p.NomeOriginalComprovante,
                p.StatusEntrega,
                p.DistanciaKm,
                p.DespachadoEm,
                p.EntregueEm,
                p.EntreguePorNome))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PedidoListaDto>> ListarAsync(
        StatusPedido? status = null,
        OrigemPedido? origem = null,
        bool? notaFiscalGerada = null,
        StatusEntrega? statusEntrega = null,
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

        if (statusEntrega is { } entregaInformada)
        {
            consulta = consulta.Where(p => p.StatusEntrega == entregaInformada);
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
                p.Itens.Count,
                p.StatusEntrega,
                p.DistanciaKm,
                p.DespachadoEm,
                p.EntregueEm,
                p.EntreguePorNome,
                p.EntreguePorUsuarioId,
                p.EnderecoLogradouro,
                p.EnderecoNumero,
                p.EnderecoBairro,
                p.EnderecoCidade,
                p.EnderecoEstado))
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

    public async Task<PedidoDto> AnexarComprovanteAsync(
        long id,
        Stream conteudo,
        string nomeOriginal,
        CancellationToken cancellationToken = default)
    {
        var pedido = await _contexto.Pedidos
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("Pedido não encontrado.");

        if (pedido.Status is StatusPedido.Cancelado)
        {
            throw new InvalidOperationException("Este pedido está cancelado e não pode receber comprovante.");
        }

        var armazenado = await _armazenamento.ArmazenarEmPastaAsync(
            conteudo,
            nomeOriginal,
            PastaDosComprovantes,
            cancellationToken);

        var anterior = pedido.CaminhoComprovante;
        pedido.CaminhoComprovante = armazenado.CaminhoRelativo;
        pedido.NomeOriginalComprovante = armazenado.NomeOriginal;

        try
        {
            await _contexto.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            // O banco recusou: apaga o arquivo novo para não deixar órfão em
            // disco sem pedido apontando para ele.
            await _armazenamento.ExcluirAsync(armazenado.CaminhoRelativo, cancellationToken);
            throw;
        }

        // Troca de comprovante: o anterior só sai do disco depois do novo
        // gravado, para uma falha no meio não deixar o pedido sem nenhum.
        if (!string.IsNullOrWhiteSpace(anterior) && anterior != armazenado.CaminhoRelativo)
        {
            await _armazenamento.ExcluirAsync(anterior, cancellationToken);
        }

        return await ObterPorIdAsync(pedido.Id, cancellationToken)
            ?? throw new InvalidOperationException("O comprovante foi anexado mas o pedido não pôde ser lido.");
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

        // Pedido pago também pode ser cancelado (desistência antes da entrega):
        // o estoque e o cupom voltam pelo caminho normal abaixo, e o pedido sai
        // do financeiro porque a receita conta só pedido com status Pago. O
        // dinheiro em si não volta sozinho: a devolução ao cliente é tratada
        // fora do sistema, depois do cancelamento.

        await using var transacao = await _contexto.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            // Devolve o estoque antes de marcar como cancelado, para que uma falha
            // no meio deixe o pedido como estava, e nao um pedido cancelado com
            // produto ainda reservado.
            await DevolverEstoqueAsync(pedido.Id, pedido.Itens, cancellationToken);

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
    /// Gera o despacho do dia: marca os pedidos como despachados, de uma vez.
    ///
    /// Só entra pedido pago, ainda a despachar e com endereço de entrega:
    /// retirada e balcão não têm entregador. É tudo-ou-nada: se um id não
    /// serve, nada muda, e a mensagem diz qual.
    /// </summary>
    public async Task<IReadOnlyList<long>> GerarDespachoAsync(
        IReadOnlyList<long> pedidoIds,
        CancellationToken cancellationToken = default)
    {
        if (pedidoIds.Count == 0)
        {
            throw new InvalidOperationException("Escolha ao menos um pedido para despachar.");
        }

        var distintos = pedidoIds.Distinct().ToList();
        var pedidos = await _contexto.Pedidos
            .Where(p => distintos.Contains(p.Id))
            .ToListAsync(cancellationToken);

        var agora = DateTime.UtcNow;

        foreach (var id in distintos)
        {
            var pedido = pedidos.FirstOrDefault(p => p.Id == id)
                ?? throw new KeyNotFoundException($"Pedido {id} não encontrado.");

            if (pedido.Status != StatusPedido.Pago)
            {
                throw new InvalidOperationException($"Somente pedido pago vai a despacho (pedido {id}).");
            }

            if (pedido.StatusEntrega != StatusEntrega.NaoEnviado)
            {
                throw new InvalidOperationException($"O pedido {id} já saiu de 'a despachar'.");
            }

            if (string.IsNullOrWhiteSpace(pedido.EnderecoLogradouro))
            {
                throw new InvalidOperationException($"O pedido {id} não tem endereço de entrega.");
            }

            pedido.StatusEntrega = StatusEntrega.Despachado;
            pedido.DespachadoEm = agora;
        }

        await _contexto.SaveChangesAsync(cancellationToken);

        return distintos;
    }

    /// <summary>
    /// Marca o pedido como entregue, gravando quem entregou e quando.
    ///
    /// O nome vai copiado, como o do cliente: desativar o entregador depois
    /// não pode reescrever o relatório. Vale para entregador e admin.
    /// </summary>
    public async Task<PedidoDto> RegistrarEntregaAsync(
        long id,
        long entregadorId,
        CancellationToken cancellationToken = default)
    {
        var pedido = await _contexto.Pedidos
            .Include(p => p.Itens)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("Pedido não encontrado.");

        if (pedido.Status is StatusPedido.Cancelado)
        {
            throw new InvalidOperationException("Este pedido está cancelado.");
        }

        if (pedido.StatusEntrega != StatusEntrega.Despachado)
        {
            throw new InvalidOperationException("Somente pedido despachado pode ser marcado como entregue.");
        }

        var entregador = await _contexto.Usuarios
            .FirstOrDefaultAsync(u => u.Id == entregadorId, cancellationToken)
            ?? throw new KeyNotFoundException("Entregador não encontrado.");

        pedido.StatusEntrega = StatusEntrega.Entregue;
        pedido.EntregueEm = DateTime.UtcNow;
        pedido.EntreguePorUsuarioId = entregador.Id;
        pedido.EntreguePorNome = entregador.Nome;

        await _contexto.SaveChangesAsync(cancellationToken);

        return await ObterPorIdAsync(pedido.Id, cancellationToken)
            ?? throw new InvalidOperationException("A entrega foi registrada mas o pedido não pôde ser lido.");
    }

    /// <summary>
    /// Lista para o portal do entregador e para os relatórios.
    ///
    /// A despachar/despachado mostra tudo (sem dono); entregue filtra por quem
    /// entregou, a menos que quem pergunte seja admin. O intervalo `de`/`ate`
    /// vale para a data do estágio filtrado (despacho ou entrega).
    /// </summary>
    public async Task<IReadOnlyList<PedidoDto>> ListarEntregasAsync(
        StatusEntrega? statusEntrega,
        DateTime? de,
        DateTime? ate,
        long? entregadorId,
        CancellationToken cancellationToken = default)
    {
        var consulta = _contexto.Pedidos.AsNoTracking();

        if (statusEntrega is { } entregaInformada)
        {
            consulta = consulta.Where(p => p.StatusEntrega == entregaInformada);
        }
        else
        {
            consulta = consulta.Where(p => p.StatusEntrega == StatusEntrega.Despachado || p.StatusEntrega == StatusEntrega.Entregue);
        }

        if (entregadorId is { } entregador)
        {
            // Só restringe o entregue: despachado não tem dono, e esconder a
            // lista de quem vai sair para a rua quebraria a conferência.
            consulta = consulta.Where(p =>
                p.StatusEntrega != StatusEntrega.Entregue || p.EntreguePorUsuarioId == entregador);
        }

        // O intervalo vale para a data do estágio filtrado: entrega ou despacho.
        if (statusEntrega == StatusEntrega.Entregue)
        {
            if (de is { } inicioEntrega)
            {
                var dia = inicioEntrega.Date;
                consulta = consulta.Where(p => p.EntregueEm >= dia);
            }

            if (ate is { } fimEntrega)
            {
                var diaSeguinte = fimEntrega.Date.AddDays(1);
                consulta = consulta.Where(p => p.EntregueEm < diaSeguinte);
            }
        }
        else
        {
            if (de is { } inicioDespacho)
            {
                var dia = inicioDespacho.Date;
                consulta = consulta.Where(p => p.DespachadoEm >= dia);
            }

            if (ate is { } fimDespacho)
            {
                var diaSeguinte = fimDespacho.Date.AddDays(1);
                consulta = consulta.Where(p => p.DespachadoEm < diaSeguinte);
            }
        }

        return await consulta
            .OrderByDescending(p => p.DespachadoEm)
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
                p.EnderecoCep,
                p.EnderecoLogradouro,
                p.EnderecoNumero,
                p.EnderecoComplemento,
                p.EnderecoBairro,
                p.EnderecoCidade,
                p.EnderecoEstado,
                p.Itens
                    .OrderBy(i => i.Id)
                    .Select(i => new PedidoItemDto(
                        i.ProdutoId,
                        i.NomeProdutoRegistrado,
                        i.Quantidade,
                        i.PrecoVendaUnitario,
                        i.PrecoPromocionalUnitario,
                        i.Subtotal,
                        i.VarianteProdutoId,
                        i.VarianteNomeRegistrado ?? (i.VarianteProduto == null ? null : i.VarianteProduto.Nome)))
                    .ToList(),
                p.CaminhoComprovante,
                p.NomeOriginalComprovante,
                p.StatusEntrega,
                p.DistanciaKm,
                p.DespachadoEm,
                p.EntregueEm,
                p.EntreguePorNome))
            .ToListAsync(cancellationToken);
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

        var variantesAtivas = await _contexto.VariantesProduto
            .AsNoTracking()
            .Where(v => ids.Contains(v.ProdutoId) && v.Ativo)
            .ToListAsync(cancellationToken);
        var variantesPorProduto = variantesAtivas
            .GroupBy(v => v.ProdutoId)
            .ToDictionary(g => g.Key, g => g.ToList());

        foreach (var grupo in requisitados.GroupBy(i => (i.ProdutoId, i.VarianteProdutoId)))
        {
            var produto = produtos.First(p => p.Id == grupo.Key.ProdutoId);
            var temVariantesAtivas = variantesPorProduto.TryGetValue(produto.Id, out var opcoes) &&
                opcoes.Count > 0;

            if (temVariantesAtivas && grupo.Key.VarianteProdutoId is null)
            {
                throw new InvalidOperationException(
                    $"Escolha uma opção para o produto {produto.Nome}.");
            }

            if (grupo.Key.VarianteProdutoId is { } varianteId)
            {
                var variante = opcoes?.FirstOrDefault(v => v.Id == varianteId);
                if (variante is null)
                {
                    throw new InvalidOperationException(
                        $"A opção escolhida para {produto.Nome} não existe ou está indisponível.");
                }

                var quantidadeSolicitada = grupo.Sum(i => i.Quantidade);
                if (variante.QuantidadeEstoque < quantidadeSolicitada)
                {
                    throw new InvalidOperationException(
                        $"Estoque insuficiente da opção {variante.Nome} de {produto.Nome}. Disponível: {variante.QuantidadeEstoque}.");
                }
            }
            else
            {
                var quantidadeSolicitada = grupo.Sum(i => i.Quantidade);
                if (produto.QuantidadeEstoque < quantidadeSolicitada)
                {
                    throw new InvalidOperationException(
                        $"Estoque insuficiente de {produto.Nome}. Disponível: {produto.QuantidadeEstoque}.");
                }
            }
        }

        var itens = new List<ItemPedido>();

        foreach (var requisitado in requisitados)
        {
            var produto = produtos.First(p => p.Id == requisitado.ProdutoId);
            VarianteProduto? variante = null;
            if (requisitado.VarianteProdutoId is { } varianteId)
            {
                variante = variantesPorProduto[produto.Id].First(v => v.Id == varianteId);
            }

            // O preco promocional tem precedencia sobre o de venda, e os dois sao
            // gravados. Guardar so o cobrado impediria o relatorio de dizer quanto
            // foi discounting de promocao alem do cupom.
            var adicional = variante?.PrecoAdicional ?? 0m;
            var precoVenda = produto.PrecoVenda + adicional;
            decimal? precoPromocional = produto.PrecoPromocional is { } promocional
                ? promocional + adicional
                : null;
            var precoPraticado = precoPromocional ?? precoVenda;

            itens.Add(new ItemPedido
            {
                ProdutoId = produto.Id,
                VarianteProdutoId = variante?.Id,
                VarianteNomeRegistrado = variante?.Nome,
                NomeProdutoRegistrado = produto.Nome,
                PrecoCustoUnitario = produto.PrecoCusto,
                PrecoVendaUnitario = precoVenda,
                PrecoPromocionalUnitario = precoPromocional,
                Quantidade = requisitado.Quantidade,
                Subtotal = Arredondar(precoPraticado * requisitado.Quantidade)
            });
        }

        return itens;
    }

    private async Task RegistrarMovimentosSaidaAsync(
        long pedidoId,
        List<ItemPedido> itens,
        CancellationToken cancellationToken)
    {
        var porProdutoEVariante = itens
            .GroupBy(i => (i.ProdutoId, i.VarianteProdutoId, i.VarianteNomeRegistrado))
            .Select(g => new
            {
                g.Key.ProdutoId,
                g.Key.VarianteProdutoId,
                g.Key.VarianteNomeRegistrado,
                Quantidade = g.Sum(i => i.Quantidade)
            })
            .ToList();

        foreach (var item in porProdutoEVariante)
        {
            var produto = await _contexto.Produtos
                .FirstOrDefaultAsync(p => p.Id == item.ProdutoId, cancellationToken);

            if (produto is null)
            {
                throw new KeyNotFoundException($"Produto não encontrado: {item.ProdutoId}.");
            }

            var movimento = new MovimentoEstoque
            {
                ProdutoId = produto.Id,
                Tipo = TipoMovimentoEstoque.Saida,
                Quantidade = item.Quantidade,
                Referencia = $"Pedido #{pedidoId}",
                Observacao = item.VarianteNomeRegistrado is null
                    ? $"Saída automática por criação do pedido #{pedidoId}."
                    : $"Saída automática por criação do pedido #{pedidoId}. Opção: {item.VarianteNomeRegistrado}."
            };

            _contexto.MovimentosEstoque.Add(movimento);
        }
    }

    private async Task BaixarEstoqueAsync(List<ItemPedido> itens, CancellationToken cancellationToken)
    {
        var porProdutoEVariante = itens
            .GroupBy(i => (i.ProdutoId, i.VarianteProdutoId))
            .Select(g => new { g.Key.ProdutoId, g.Key.VarianteProdutoId, Quantidade = g.Sum(i => i.Quantidade) })
            .ToList();

        // Somar por produto antes de baixar evita que duas linhas do mesmo produto
        // validassem 3 + 3 contra um estoque de 5 e o deixassem negativo.
        foreach (var item in porProdutoEVariante)
        {
            if (item.VarianteProdutoId is { } varianteId)
            {
                var variante = await _contexto.VariantesProduto
                    .FirstOrDefaultAsync(v => v.Id == varianteId && v.ProdutoId == item.ProdutoId, cancellationToken)
                    ?? throw new InvalidOperationException("A opção do produto não existe mais.");

                if (variante.QuantidadeEstoque < item.Quantidade)
                {
                    throw new InvalidOperationException(
                        $"Estoque insuficiente da opção {variante.Nome}. Disponível: {variante.QuantidadeEstoque}.");
                }

                variante.QuantidadeEstoque -= item.Quantidade;
                continue;
            }

            var produto = await _contexto.Produtos
                .FirstOrDefaultAsync(p => p.Id == item.ProdutoId, cancellationToken)
                ?? throw new KeyNotFoundException($"Produto não encontrado: {item.ProdutoId}.");

            if (produto.QuantidadeEstoque < item.Quantidade)
            {
                throw new InvalidOperationException(
                    $"Estoque insuficiente de {produto.Nome}. Disponível: {produto.QuantidadeEstoque}.");
            }
            produto.QuantidadeEstoque -= item.Quantidade;
        }
    }

    private async Task DevolverEstoqueAsync(
        long pedidoId,
        ICollection<ItemPedido> itens,
        CancellationToken cancellationToken)
    {
        var porProdutoEVariante = itens
            .GroupBy(i => (i.ProdutoId, i.VarianteProdutoId, i.VarianteNomeRegistrado))
            .Select(g => new
            {
                g.Key.ProdutoId,
                g.Key.VarianteProdutoId,
                g.Key.VarianteNomeRegistrado,
                Quantidade = g.Sum(i => i.Quantidade)
            })
            .ToList();

        foreach (var item in porProdutoEVariante)
        {
            var produto = await _contexto.Produtos
                .FirstOrDefaultAsync(p => p.Id == item.ProdutoId, cancellationToken);

            if (produto is null)
            {
                // Produto excluído depois da venda: não há onde devolver nem
                // movimento a registrar sem violar a FK.
                continue;
            }

            if (item.VarianteProdutoId is { } varianteId)
            {
                var variante = await _contexto.VariantesProduto
                    .FirstOrDefaultAsync(v => v.Id == varianteId && v.ProdutoId == item.ProdutoId, cancellationToken);

                if (variante is not null)
                {
                    variante.QuantidadeEstoque += item.Quantidade;
                }
            }
            else
            {
                produto.QuantidadeEstoque += item.Quantidade;
            }

            // Sem a entrada, o extrato mostra a saída da criação sem a volta do
            // cancelamento, e o movimento do produto nunca fecha.
            _contexto.MovimentosEstoque.Add(new MovimentoEstoque
            {
                ProdutoId = produto.Id,
                Tipo = TipoMovimentoEstoque.Entrada,
                Quantidade = item.Quantidade,
                Referencia = $"Pedido #{pedidoId}",
                Observacao = item.VarianteNomeRegistrado is null
                    ? $"Entrada automática por cancelamento do pedido #{pedidoId}."
                    : $"Entrada automática por cancelamento do pedido #{pedidoId}. Opção: {item.VarianteNomeRegistrado}."
            });
        }

        await _contexto.SaveChangesAsync(cancellationToken);
    }

    private static decimal Arredondar(decimal valor) => Math.Round(valor, 2, MidpointRounding.AwayFromZero);
}
