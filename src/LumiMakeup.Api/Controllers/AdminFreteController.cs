using System.Security.Authentication;
using LumiMakeup.Application.Abstractions;
using LumiMakeup.Application.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LumiMakeup.Api.Controllers;

/*
 * VERBOS: mesma regra de AdminProdutosController. Salvar e simular sao POST,
 * porque o servidor de producao nao encaminha PUT.
 */
[ApiController]
[Authorize(Policy = "SomenteAdministrador")]
[Route("api/admin/frete")]
public sealed class AdminFreteController : ControllerBase
{
    private readonly IGestaoDeFreteService _frete;
    private readonly INominatimService _mapa;
    private readonly IGeocodificador _geocodificador;

    public AdminFreteController(
        IGestaoDeFreteService frete,
        INominatimService mapa,
        IGeocodificador geocodificador)
    {
        _frete = frete;
        _mapa = mapa;
        _geocodificador = geocodificador;
    }

    /// <summary>
    /// A configuração atual, ou uma vazia. Vazio é o estado inicial de uma loja
    /// que ainda não vendeu pelo site, e a tela mostra o formulário para isso.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Obter(CancellationToken cancellationToken)
    {
        var configuracao = await _frete.ObterAsync(cancellationToken);
        return Ok(configuracao);
    }

    [HttpPost]
    public async Task<IActionResult> Salvar(
        [FromBody] RequisicaoDeConfiguracaoDeFrete requisicao,
        CancellationToken cancellationToken)
    {
        try
        {
            var configuracao = await _frete.SalvarAsync(requisicao, cancellationToken);
            return Ok(configuracao);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Testa a conexão com o serviço de mapa, sem calcular frete.
    ///
    /// Existe porque o cálculo recusava em produção sem dizer por quê: o
    /// geocodificador volta sem coordenada tanto quando o mapa não tem o CEP
    /// quanto quando o servidor não conseguiu falar com o mapa, e as duas
    /// causas precisam de correções diferentes. Esta rota diz qual é.
    /// </summary>
    [HttpPost("diagnostico")]
    public async Task<IActionResult> Diagnostico(
        [FromBody] RequisicaoDeSimulacaoDeFrete requisicao,
        CancellationToken cancellationToken)
    {
        var limpo = new string((requisicao?.Cep ?? string.Empty).Where(char.IsDigit).ToArray());

        if (limpo.Length != 8)
        {
            return BadRequest(new { message = "Informe um CEP válido para simular." });
        }

        var consulta = $"{limpo[..5]}-{limpo[5..]}";

        var resultado = await _mapa.GeocodificarAsync(consulta, cancellationToken);

        // O motivo vai cru de propósito. A causa real ficava enterrada em
        // HttpRequestException, e a pista decisiva - "The SSL connection could
        // not be established" - só apareceu quando a mensagem foi devolvida
        // inteira em vez de um genérico "não conseguimos localizar".
        return Ok(new
        {
            cep = $"{limpo[..5]}-{limpo[5..]}",
            consulta,
            coordenadas = resultado,
            situacao = resultado is null ? "sem ponto para este CEP" : "encontrado",
            motivo = _mapa.UltimoFalha,
            instrucao = _mapa.UltimoFalha?.Contains("HandshakeFailure", StringComparison.OrdinalIgnoreCase) == true
                ? "O proxy do mapa recusou a negociacao TLS. O codigo ja pede TLS 1.2; se persistir, habilite SchUseStrongCrypto no Windows e reinicie o servidor."
                : _mapa.UltimoFalha?.Contains("SSL connection", StringComparison.OrdinalIgnoreCase) == true
                    ? "O servidor nao conseguiu fechar a conexao TLS com o mapa. Verifique se o TLS 1.2 esta habilitado e se a raiz certificadora do Windows esta atualizada, e reinicie o site."
                    : null
        });
    }

    /// <summary>
    /// Testa a saída HTTPS do servidor, sem calcular frete.
    ///
    /// Existe porque a falha de TLS só aparecia embrulhada em
    /// HttpRequestException, e a causa real ficava enterrada no log. Comparar
    /// hosts daqui de dentro separa "o servidor não sai para a internet" de
    /// "esse site específico não fecha a conexão" - que precisam de consertos
    /// opostos.
    /// </summary>
    [HttpPost("conexao")]
    public async Task<IActionResult> Conexao(CancellationToken cancellationToken)
    {
        // O mesmo cliente que o geocodificador usa. Testar com outro cliente
        // daria um resultado que não vale para o cálculo.
        var cliente = _mapa.CriarClienteDeDiagnostico();

        var alvos = new[]
        {
            "https://nominatim.openstreetmap.org/status.php",
            "https://viacep.com.br/ws/18072856/json/",
            "https://lumimakeup.com.br/",
            "https://www.google.com/"
        };

        var resultados = new List<object>();

        foreach (var alvo in alvos)
        {
            var url = new Uri(alvo);

            try
            {
                using var resposta = await cliente.GetAsync(alvo, cancellationToken);

                resultados.Add(new
                {
                    alvo,
                    status = (int)resposta.StatusCode,
                    detalhe = "respondeu"
                });
            }
            catch (Exception ex)
            {
                // A cadeia inteira, não só a mensagem de fora. "The SSL
                // connection could not be established" não diz se o problema é
                // protocolo, cadeia de certificação ou bloqueio de rede - e cada
                // um tem um conserto diferente.
                var interno = ex.InnerException?.Message;
                var maisInterno = ex.InnerException?.InnerException?.Message;

                resultados.Add(new
                {
                    alvo,
                    status = (int?)null,
                    erro = ex.GetType().Name,
                    mensagem = ex.Message,
                    causa = interno,
                    causaRaiz = maisInterno
                });
            }
        }

        return Ok(resultados);
    }

    /// <summary>
    /// Testa cada versão de TLS contra o mapa, de dentro do servidor.
    ///
    /// Forçar TLS 1.2 não resolveu, o que significa que a recusa não é só
    /// sobre a versão: o Windows Server antigo pode não ter nenhuma suite de
    /// cifras em comum com o proxy do mapa. Testar as combinações daqui é o
    /// que separa "falta habilitar algo" de "não há solução sem mexer no
    /// servidor".
    /// </summary>
    [HttpPost("tls")]
    public async Task<IActionResult> TestarTls(CancellationToken cancellationToken)
    {
        const string alvo = "https://nominatim.openstreetmap.org/status.php";

        var combinacoes = new (string Nome, SslProtocols Protocolos)[]
        {
            ("TLS 1.2", SslProtocols.Tls12),
            ("TLS 1.3", SslProtocols.Tls13),
            ("TLS 1.2 ou 1.3", SslProtocols.Tls12 | SslProtocols.Tls13),
            ("TLS 1.0", SslProtocols.Tls),
            ("TLS 1.1", SslProtocols.Tls11),
            ("TLS 1.0 ate 1.2", SslProtocols.Tls | SslProtocols.Tls11 | SslProtocols.Tls12)
        };

        var resultados = new List<object>();

        foreach (var (nome, protocolos) in combinacoes)
        {
            try
            {
                using var handler = new HttpClientHandler { SslProtocols = protocolos };
                using var cliente = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(20) };

                using var resposta = await cliente.GetAsync(alvo, cancellationToken);

                resultados.Add(new { nome, status = (int)resposta.StatusCode, resultado = "respondeu" });
            }
            catch (Exception ex)
            {
                resultados.Add(new
                {
                    nome,
                    status = (int?)null,
                    resultado = "falhou",
                    erro = ex.GetType().Name,
                    causa = ex.InnerException?.Message
                });
            }
        }

        return Ok(resultados);
    }

    /// <summary>
    /// Testa cada provedor de mapa, de dentro do servidor.
    ///
    /// Medir daqui não vale: o Windows Server de produção não consegue fechar
    /// TLS com o Nominatim, e a única forma de saber se Photon e ArcGIS
    /// funcionam de lá é chamá-los de lá.
    /// </summary>
    [HttpPost("provedores")]
    public async Task<IActionResult> Provedores(
        [FromBody] RequisicaoDeSimulacaoDeFrete requisicao,
        CancellationToken cancellationToken)
    {
        var resultados = await _geocodificador.TestarProvedoresAsync(requisicao?.Cep ?? string.Empty, cancellationToken);

        return Ok(resultados);
    }

    /// <summary>Simula o frete de um CEP, sem criar pedido.</summary>
    [HttpPost("simular")]
    public async Task<IActionResult> Simular(
        [FromBody] RequisicaoDeSimulacaoDeFrete requisicao,
        CancellationToken cancellationToken)
    {
        try
        {
            var calculo = await _frete.SimularAsync(requisicao.Cep, cancellationToken);
            return Ok(calculo);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
