using System.Security.Cryptography;
using System.Text;
using LumiMakeup.Application.DTOs;
using LumiMakeup.Infrastructure.Integrations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace LumiMakeup.Api.Controllers;

/*
 * VERBOS: POST para gravar, GET para ler. O servidor de producao nao
 * encaminha PUT nem PATCH.
 *
 * AUTENTICACAO: segredo compartilhado no cabecalho, e nao a politica
 * "SomenteAdministrador".
 *
 * Isso e deliberado e e o oposto do restante do painel. Estas rotas sao
 * maquina-para-maquina: quem chama e o Node do Baileys, que roda no Render e
 * nao tem login de navegador. Exigir token de admin obrigaria o Node a ter
 * uma sessao de usuario, e o segredo e o que ele ja tem.
 *
 * O detalhe que importa: a sessao que passa por aqui E a conta do WhatsApp da
 * loja. Quem le esta resposta com o segredo certo assume o numero. Por isso
 * estas rotas nao passam pelo painel e nao entram no proxy publico do site do
 * Node - o web.config bloqueia tudo, e a API e o unico caminho.
 */
[ApiController]
[Route("api/whatsapp-sessao")]
public sealed class SessaoWhatsAppController : ControllerBase
{
    private readonly RepositorioDeSessaoWhatsApp _sessao;
    private readonly OpcoesDeBaileys _opcoes;
    private readonly ILogger<SessaoWhatsAppController> _logger;

    public SessaoWhatsAppController(
        RepositorioDeSessaoWhatsApp sessao,
        IOptions<OpcoesDeBaileys> opcoes,
        ILogger<SessaoWhatsAppController> logger)
    {
        _sessao = sessao;
        _opcoes = opcoes.Value;
        _logger = logger;
    }

    /// <summary>
    /// A sessao guardada. <c>existe: false</c> quando o numero ainda nao foi
    /// pareado, e e assim que o Node sabe que precisa gerar QR.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Obter(CancellationToken cancellationToken)
    {
        if (!SecretoValido())
        {
            return NaoAutorizado();
        }

        var sessao = await _sessao.ObterAsync(cancellationToken);

        if (sessao is null)
        {
            return Ok(new { existe = false, versao = 0, credenciais = (string?)null, chaves = (string?)null });
        }

        return Ok(new
        {
            existe = true,
            sessao.Versao,
            credenciais = sessao.Credenciais,
            chaves = sessao.Chaves
        });
    }

    /// <summary>
    /// Grava o que mudou. Responde com a versao ja gravada, ou 409 se outro
    /// contêiner escreveu antes - nesse caso o Node recarrega do banco.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Gravar(
        [FromBody] RequisicaoDeGravacaoDeSessao requisicao,
        CancellationToken cancellationToken)
    {
        if (!SecretoValido())
        {
            return NaoAutorizado();
        }

        if (string.IsNullOrWhiteSpace(requisicao?.Credenciais))
        {
            return BadRequest(new { message = "Credenciais vazias: o Node nao mandou a sessao." });
        }

        var gravado = await _sessao.GravarAsync(
            requisicao.Credenciais,
            requisicao.Chaves ?? "{}",
            requisicao.VersaoEsperada,
            cancellationToken);

        if (!gravado)
        {
            // 409 e nao 500: o pedido estava certo, quem escreveu por ultimo
            // foi outro. O Node trata recarregando, que e a saida correta.
            var atual = await _sessao.ObterAsync(cancellationToken);

            return Conflict(new
            {
                message = "Outra gravacao aconteceu antes desta. Recarregue a sessao.",
                versaoAtual = atual?.Versao ?? 0
            });
        }

        return Ok(new { gravado = true, versao = requisicao.VersaoEsperada + 1 });
    }

    /// <summary>
    /// Apaga a sessao para o numero ser pareado de novo. Chamado pelo Node
    /// quando o WhatsApp desconecta o numero de proposito.
    /// </summary>
    [HttpPost("apagar")]
    public async Task<IActionResult> Apagar(CancellationToken cancellationToken)
    {
        if (!SecretoValido())
        {
            return NaoAutorizado();
        }

        await _sessao.ApagarAsync(cancellationToken);

        return Ok(new { apagado = true });
    }

    private bool SecretoValido()
    {
        var esperado = _opcoes.SegredoCompartilhado;

        if (string.IsNullOrWhiteSpace(esperado))
        {
            return false;
        }

        var recebido = Request.Headers["x-segredo"].ToString();

        if (string.IsNullOrEmpty(recebido))
        {
            return false;
        }

        // timingSafeEqual exige mesmo tamanho, e comparar tamanhos antes de
        // saber o valor ja entrega o tamanho do segredo. O mesmo cuidado do
        // lado do Node.
        var a = Encoding.UTF8.GetBytes(recebido);
        var b = Encoding.UTF8.GetBytes(esperado);

        return a.Length == b.Length && CryptographicOperations.FixedTimeEquals(a, b);
    }

    private IActionResult NaoAutorizado()
    {
        _logger.LogWarning(
            "Acesso a rota de sessao do WhatsApp recusado: segredo ausente ou invalido. Vem de {Origem}.",
            Request.Headers.Origin.ToString() is { Length: > 0 } origem ? origem : "servico (sem Origin)");

        // 401 e nao 403: com 403 o chamador saberia que o segredo existe e so
        // errou o valor, o que ajuda a tentar de novo.
        return Unauthorized(new { message = "Segredo invalido." });
    }
}
