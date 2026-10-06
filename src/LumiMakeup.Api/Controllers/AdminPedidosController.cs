using LumiMakeup.Api.Extensions;
using LumiMakeup.Application.Abstractions;
using LumiMakeup.Application.DTOs;
using LumiMakeup.Domain.Enums;
using LumiMakeup.Infrastructure.Integrations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.IO;

namespace LumiMakeup.Api.Controllers;

/*
 * VERBOS: mesma regra de AdminProdutosController. O servidor de producao so
 * encaminha GET, POST, HEAD, OPTIONS e TRACE; PUT e DELETE sao recusados pelo IIS
 * antes de chegar aqui. Por isso registrar pagamento e cancelar sao POST com o
 * verbo no fim da URL.
 */
[ApiController]
[Authorize(Policy = "SomenteAdministrador")]
[Route("api/admin/pedidos")]
public sealed class AdminPedidosController : ControllerBase
{
    private readonly IGestaoDePedidosService _pedidos;
    private readonly ArmazenamentoDeImagensOptions _opcoes;

    public AdminPedidosController(
        IGestaoDePedidosService pedidos,
        IOptions<ArmazenamentoDeImagensOptions> opcoes)
    {
        _pedidos = pedidos;
        _opcoes = opcoes.Value;
    }

    /// <summary>
    /// Listagem do painel. Todos os filtros são opcionais, e `semNotaFiscal` é o
    /// que responde "o que ainda falta emitir".
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Listar(
        [FromQuery] StatusPedido? status,
        [FromQuery] OrigemPedido? origem,
        [FromQuery] bool? semNotaFiscal,
        CancellationToken cancellationToken)
    {
        var pedidos = await _pedidos.ListarAsync(
            status,
            origem,
            // A query string diz "sem nota fiscal" porque é assim que a tela
            // pergunta; o serviço recebe o valor da flag. A negação acontece uma
            // vez, aqui, com nome explícito dos dois lados.
            notaFiscalGerada: semNotaFiscal is null ? null : !semNotaFiscal.Value,
            cancellationToken);

        return Ok(pedidos);
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> ObterPorId(long id, CancellationToken cancellationToken)
    {
        var pedido = await _pedidos.ObterPorIdAsync(id, cancellationToken);

        if (pedido is null)
        {
            return NotFound(new { message = "Pedido não encontrado." });
        }

        return Ok(pedido);
    }

    /// <summary>
    /// Registra a venda de balcão.
    ///
    /// Vai para a mesma rota de criação do checkout, mudando a origem: a venda
    /// presencial baixa estoque, consome cupom e entra no relatório exatamente
    /// como a de online. Passando por um caminho próprio, uma das duas formas
    /// acabaria esquecendo de baixar o estoque ou de avisar o cliente.
    ///
    /// O cliente vem do corpo, escolhido pela administradora: um cadastro
    /// existente ou um que ela acabou de fazer na própria tela. Sem telefone
    /// cadastrado a venda não tem por onde ser confirmada, e é o telefone que
    /// o cliente recebe para finalizar.
    /// </summary>
    [HttpPost("balcao")]
    public async Task<IActionResult> CriarVendaDeBalcao(
        [FromBody] RequisicaoDeVendaDeBalcao requisicao,
        CancellationToken cancellationToken)
    {
        try
        {
            // Aqui o cliente vem do **corpo**, e não do token. É a diferença
            // entre a rota do checkout e esta: no checkout o cliente é quem está
            // comprando, e tirar o id do corpo impede que ele registre a compra
            // na conta de outra pessoa. Na venda de balcão quem compra é a
            // administradora, e o cliente é quem ela escolheu na tela — que
            // pode ser um cadastro existente ou um cadastro que ela acabou de
            // fazer. Vir o id do token registraria a venda na conta da própria
            // administradora, toda vez.
            var pedido = await _pedidos.CriarAsync(
                new RequisicaoDePedido(
                    requisicao.UsuarioId,
                    requisicao.Itens,
                    null,
                    requisicao.CupomCodigo,
                    requisicao.Observacoes,
                    0m,
                    0m,
                    requisicao.CriadoEm),
                OrigemPedido.Balcao,
                cancellationToken);

            return CreatedAtAction(nameof(ObterPorId), new { id = pedido.Id }, pedido);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// A administradora anota a forma de pagamento e dá o aceite. É o "finalizar"
    /// do fluxo: o pedido sai de <c>AguardandoPagamento</c> para <c>Pago</c>.
    ///
    /// Não há verificação de compensação bancária — quem confirma o recebimento é
    /// a administradora.
    /// </summary>
    [HttpPost("{id:long}/pagamento")]
    public async Task<IActionResult> RegistrarPagamento(
        long id,
        [FromBody] RequisicaoDePagamentoDePedido requisicao,
        CancellationToken cancellationToken)
    {
        try
        {
            var pedido = await _pedidos.RegistrarPagamentoAsync(id, requisicao.MetodoPagamento, cancellationToken);
            return Ok(pedido);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>Cancelar devolve o estoque e a unidade de cupom.</summary>
    [HttpPost("{id:long}/cancelar")]
    public async Task<IActionResult> Cancelar(long id, CancellationToken cancellationToken)
    {
        try
        {
            var pedido = await _pedidos.CancelarAsync(id, cancellationToken);
            return Ok(pedido);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Anexa a foto do comprovante de pagamento ao pedido.
    ///
    /// Segue as mesmas regras das imagens de produto e banner (JPG ou PNG até
    /// 5 MB, validação pelo conteúdo), gravando na pasta de comprovantes. O
    /// anexo é opcional no pagamento: pode entrar junto do aceite ou depois,
    /// num pedido já pago.
    /// </summary>
    [HttpPost("{id:long}/pagamento/comprovante")]
    [RequestSizeLimit(6_291_456)]
    public async Task<IActionResult> AnexarComprovante(
        long id,
        IFormFile comprovante,
        CancellationToken cancellationToken)
    {
        if (comprovante is null || comprovante.Length == 0)
        {
            return BadRequest(new { message = "Selecione um arquivo de imagem." });
        }

        if (comprovante.Length > _opcoes.TamanhoMaximoEmBytes)
        {
            return BadRequest(new
            {
                message = $"A imagem excede o limite de {_opcoes.TamanhoMaximoEmBytes / (1024 * 1024)} MB."
            });
        }

        try
        {
            await using var conteudo = comprovante.OpenReadStream();
            var pedido = await _pedidos.AnexarComprovanteAsync(id, conteudo, comprovante.FileName, cancellationToken);
            return Ok(pedido);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (IOException ex)
        {
            Response.Headers["Access-Control-Allow-Origin"] = "*";
            return StatusCode(500, new
            {
                message = $"Falha ao salvar o comprovante: {ex.GetType().Name} ao acessar '{ex.Message}'. Caminho/pasta configurada: {_opcoes.PastaPadrao}"
            });
        }
        catch (UnauthorizedAccessException ex)
        {
            Response.Headers["Access-Control-Allow-Origin"] = "*";
            return StatusCode(500, new
            {
                message = $"Falha ao salvar o comprovante: {ex.GetType().Name}. Caminho/pasta configurada: {_opcoes.PastaPadrao}"
            });
        }
        catch (DbUpdateException ex)
        {
            Response.Headers["Access-Control-Allow-Origin"] = "*";
            return StatusCode(500, new
            {
                message = $"Falha ao salvar o comprovante: {ex.GetType().Name}. Caminho/pasta configurada: {_opcoes.PastaPadrao}"
            });
        }
    }
}
