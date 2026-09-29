using LumiMakeup.Application.Abstractions;
using LumiMakeup.Application.DTOs;
using LumiMakeup.Infrastructure.Integrations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace LumiMakeup.Api.Controllers;

/*
 * VERBOS: mesma regra de AdminProdutosController. O servidor de producao so
 * encaminha GET, POST, HEAD, OPTIONS e TRACE; PUT e DELETE sao recusados pelo IIS
 * antes de chegar aqui. Por isso atualizar, ativar, reordenar e excluir sao POST
 * com o verbo no fim da URL.
 *
 * Os dois arquivos do banner (desktop e celular) sobem juntos, no mesmo
 * multipart. A alternativa seria duas chamadas, mas ai o slide poderia ficar
 * gravado pela metade se a segunda falhasse, e o painel teria de tratar um
 * estado intermediario que a home nunca sabe exibir.
 */
[ApiController]
[Authorize(Policy = "SomenteAdministrador")]
[Route("api/admin/banners")]
public sealed class AdminBannersController : ControllerBase
{
    /// <summary>
    /// Margem sobre o limite por arquivo, porque cada requisição carrega duas
    /// imagens. O teto efetivo continua sendo o de <c>TamanhoMaximoEmBytes</c>,
    /// conferido arquivo por arquivo abaixo.
    /// </summary>
    private const long TamanhoMaximoDaRequisicao = 12_582_912;

    private readonly IGestaoDeBannersService _gestaoDeBanners;
    private readonly ArmazenamentoDeImagensOptions _opcoes;

    public AdminBannersController(
        IGestaoDeBannersService gestaoDeBanners,
        IOptions<ArmazenamentoDeImagensOptions> opcoes)
    {
        _gestaoDeBanners = gestaoDeBanners;
        _opcoes = opcoes.Value;
    }

    [HttpGet]
    public async Task<IActionResult> ObterTodos(CancellationToken cancellationToken)
    {
        var banners = await _gestaoDeBanners.ObterTodosAsync(cancellationToken);
        return Ok(banners);
    }

    [HttpPost]
    [RequestSizeLimit(TamanhoMaximoDaRequisicao)]
    public async Task<IActionResult> Criar(
        IFormFile imagemDesktop,
        IFormFile imagemMobile,
        [FromForm] string? textoAlternativo,
        CancellationToken cancellationToken)
    {
        // Na criacao os dois arquivos sao obrigatorios: um banner so com a arte de
        // desktop quebraria a home no celular, e a tag picture nao tem o que
        // carregar no <source>.
        if (imagemDesktop is null || imagemMobile is null)
        {
            return BadRequest(new { message = "Envie as duas imagens do banner: uma para desktop e uma para celular." });
        }

        var erroDeTamanho = ValidarArquivos(imagemDesktop, imagemMobile);
        if (erroDeTamanho is not null)
        {
            return BadRequest(new { message = erroDeTamanho });
        }

        try
        {
            await using var streamDesktop = imagemDesktop.OpenReadStream();
            await using var streamMobile = imagemMobile.OpenReadStream();

            var banner = await _gestaoDeBanners.CriarAsync(
                streamDesktop,
                imagemDesktop.FileName,
                streamMobile,
                imagemMobile.FileName,
                textoAlternativo,
                cancellationToken);

            return CreatedAtAction(nameof(ObterTodos), banner);
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

    [HttpPost("{id:long}/atualizar")]
    [RequestSizeLimit(TamanhoMaximoDaRequisicao)]
    public async Task<IActionResult> Atualizar(
        long id,
        IFormFile? imagemDesktop,
        IFormFile? imagemMobile,
        [FromForm] string? textoAlternativo,
        CancellationToken cancellationToken)
    {
        // Aqui os dois arquivos sao opcionais de proposito: editar so o texto
        // alternativo nao deve exigir reenviar as artes, e o painel nem sempre
        // tem os arquivos originais a mao. Ausente ou vazio nao entra no laco de
        // ValidarArquivos, entao nao ha checagem de presenca — so de tamanho e
        // formato, quando ha algo enviado.
        var erroDeTamanho = ValidarArquivos(imagemDesktop, imagemMobile);
        if (erroDeTamanho is not null)
        {
            return BadRequest(new { message = erroDeTamanho });
        }

        try
        {
            await using var streamDesktop = imagemDesktop?.OpenReadStream();
            await using var streamMobile = imagemMobile?.OpenReadStream();

            var banner = await _gestaoDeBanners.AtualizarAsync(
                id,
                streamDesktop,
                imagemDesktop?.FileName,
                streamMobile,
                imagemMobile?.FileName,
                textoAlternativo,
                cancellationToken);

            return Ok(banner);
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

    [HttpPost("{id:long}/ativo")]
    public async Task<IActionResult> DefinirAtivo(
        long id,
        [FromBody] RequisicaoDeAtivacaoDeBanner requisicao,
        CancellationToken cancellationToken)
    {
        try
        {
            var banner = await _gestaoDeBanners.DefinirAtivoAsync(id, requisicao.Ativo, cancellationToken);
            return Ok(banner);
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

    [HttpPost("{id:long}/excluir")]
    public async Task<IActionResult> Excluir(long id, CancellationToken cancellationToken)
    {
        try
        {
            await _gestaoDeBanners.ExcluirAsync(id, cancellationToken);
            return NoContent();
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

    [HttpPost("ordem")]
    public async Task<IActionResult> Reordenar(
        [FromBody] RequisicaoDeOrdenacaoDeBanners requisicao,
        CancellationToken cancellationToken)
    {
        try
        {
            var banners = await _gestaoDeBanners.ReordenarAsync(requisicao.Ordem, cancellationToken);
            return Ok(banners);
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
    /// Retorna a mensagem do primeiro arquivo inválido, ou nulo se tudo passou.
    /// Na criação ambos são obrigatórios; na atualização, ausentes são aceitáveis.
    /// </summary>
    private string? ValidarArquivos(IFormFile? desktop, IFormFile? mobile)
    {
        foreach (var arquivo in new[] { desktop, mobile })
        {
            if (arquivo is null)
            {
                continue;
            }

            if (arquivo.Length == 0)
            {
                return "Selecione um arquivo de imagem válido.";
            }

            if (arquivo.Length > _opcoes.TamanhoMaximoEmBytes)
            {
                return $"A imagem {arquivo.Name} excede o limite de {_opcoes.TamanhoMaximoEmBytes / (1024 * 1024)} MB.";
            }
        }

        return null;
    }
}
