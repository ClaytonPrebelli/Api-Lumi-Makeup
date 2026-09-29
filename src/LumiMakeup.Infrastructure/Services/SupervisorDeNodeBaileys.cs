using System.Diagnostics;
using System.Text;
using LumiMakeup.Infrastructure.Integrations;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LumiMakeup.Infrastructure.Services;

/// <summary>
/// Sobe e vigia o processo do Node do Baileys, que fica numa pasta irma da
/// publicacao da API.
///
/// Existe por causa de uma limitacao do servidor: o acesso e so por FTP, sem
/// console. Nao ha como instalar Node, nem Task Scheduler para agendar inicio.
/// Se a API nao iniciar o Node, ele nunca sobe depois de um restart do servidor.
///
/// O supervisor nao derruba a API se o Node falhar. O WhatsApp e acessorio; a
/// loja continua vendendo sem ele, e a falha fica no log.
/// </summary>
public sealed class SupervisorDeNodeBaileys : BackgroundService
{
    private readonly OpcoesDeBaileys _opcoes;
    private readonly IHostEnvironment _ambiente;
    private readonly ILogger<SupervisorDeNodeBaileys> _logger;
    private readonly string _pastaDoNode;

    private Process? _processo;

    public SupervisorDeNodeBaileys(
        IOptions<OpcoesDeBaileys> opcoes,
        IHostEnvironment ambiente,
        ILogger<SupervisorDeNodeBaileys> logger)
    {
        _opcoes = opcoes.Value;
        _ambiente = ambiente;
        _logger = logger;
        _pastaDoNode = ResolverPastaDoNode(_opcoes.PastaDoNode, ambiente.ContentRootPath);
    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        if (!_opcoes.Habilitado || !_opcoes.IniciarProcesso)
        {
            _logger.LogInformation(
                "Supervisor do Baileys desligado (Habilitado={Habilitado}, IniciarProcesso={Iniciar}).",
                _opcoes.Habilitado,
                _opcoes.IniciarProcesso);

            return;
        }

        _logger.LogInformation("Supervisor do Baileys iniciado. Pasta do Node: {Pasta}.", _pastaDoNode);

        while (!cancellationToken.IsCancellationRequested)
        {
            TentarIniciar();

            if (_processo is null)
            {
                // Nao ha processo e nao adianta girar: o executavel pode estar
                // faltando. Espera e tenta de novo, porque no deploy o arquivo
                // pode chegar depois do pool subir.
                if (!await EsperarAsync(TimeSpan.FromSeconds(_opcoes.IntervaloDeReinicioEmSegundos), cancellationToken))
                {
                    return;
                }

                continue;
            }

            if (!await EsperarEncerramentoAsync(cancellationToken))
            {
                return;
            }

            _logger.LogWarning("O Node do Baileys encerrou. Vou subir de novo em {Segundos}s.", _opcoes.IntervaloDeReinicioEmSegundos);

            if (!await EsperarAsync(TimeSpan.FromSeconds(_opcoes.IntervaloDeReinicioEmSegundos), cancellationToken))
            {
                return;
            }
        }
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Encerrando o Node do Baileys junto com a API.");

        Encerrar(_processo, _logger);

        return base.StopAsync(cancellationToken);
    }

    private void TentarIniciar()
    {
        var executavel = Path.GetFullPath(Path.Combine(_pastaDoNode, _opcoes.Executavel));
        var entrada = Path.Combine(_pastaDoNode, "src", "index.js");

        if (!File.Exists(executavel))
        {
            _logger.LogError(
                "Node do Baileys nao subiu: {Executavel} nao existe. Confira ExternalServices:Baileys:PastaDoNode e Executavel. O WhatsApp fica sem envio ate isso ser resolvido.",
                executavel);

            return;
        }

        if (!File.Exists(entrada))
        {
            _logger.LogError("Node do Baileys nao subiu: {Entrada} nao existe.", entrada);

            return;
        }

        try
        {
            var informacoes = MontarInformacoesDoProcesso(
                executavel,
                entrada,
                _pastaDoNode,
                _opcoes.Porta,
                _opcoes.SegredoCompartilhado);

            var processo = new Process { StartInfo = informacoes, EnableRaisingEvents = true };

            processo.OutputDataReceived += (_, evento) => RegistrarSaida(evento.Data, false);
            processo.ErrorDataReceived += (_, evento) => RegistrarSaida(evento.Data, true);

            processo.Start();
            processo.BeginOutputReadLine();
            processo.BeginErrorReadLine();

            _processo = processo;

            _logger.LogInformation(
                "Node do Baileys iniciado (pid {Pid}) em {Pasta}.",
                processo.Id,
                _pastaDoNode);
        }
        catch (Exception excecao)
        {
            _logger.LogError(excecao, "Falha ao iniciar o Node do Baileys.");
            _processo = null;
        }
    }

    /// <summary>
    /// Monta o comando de subida. Fica separado para o teste poder conferir o
    /// executavel, a pasta de trabalho e as variaveis de ambiente sem precisar
    /// subir um Node de verdade.
    /// </summary>
    public static ProcessStartInfo MontarInformacoesDoProcesso(
        string executavel,
        string entrada,
        string pastaDeTrabalho,
        int porta,
        string segredoCompartilhado)
    {
        var informacoes = new ProcessStartInfo
        {
            FileName = executavel,
            WorkingDirectory = pastaDeTrabalho,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        informacoes.ArgumentList.Add(entrada);

        // O segredo vai por ambiente, e nao por linha de comando: linha de
        // comando aparece na lista de processos do Windows, e qualquer usuario
        // da maquina le la.
        informacoes.Environment["PORT"] = porta.ToString();
        informacoes.Environment["BAILEYS_SEGREDO_COMPARTILHADO"] = segredoCompartilhado;
        informacoes.Environment["PASTA_DE_DADOS"] = Path.Combine(pastaDeTrabalho, "dados");
        informacoes.Environment["NODE_ENV"] = "production";

        return informacoes;
    }

    private async Task<bool> EsperarEncerramentoAsync(CancellationToken cancellationToken)
    {
        var processo = _processo;

        if (processo is null)
        {
            return true;
        }

        try
        {
            await processo.WaitForExitAsync(cancellationToken);

            _logger.LogWarning("O Node do Baileys saiu com codigo {Codigo}.", processo.ExitCode);

            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        finally
        {
            Encerrar(processo, _logger);
            _processo = null;
        }
    }

    private async Task<bool> EsperarAsync(TimeSpan tempo, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(tempo, cancellationToken);

            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    private void RegistrarSaida(string? linha, bool ehErro)
    {
        if (string.IsNullOrWhiteSpace(linha))
        {
            return;
        }

        // O Node escreve JSON, e um log por linha. Repassar como mensagem do
        // ILogger mantem o log da API legivel e pesquisavel.
        if (ehErro)
        {
            _logger.LogWarning("baileys: {Linha}", linha);
        }
        else
        {
            _logger.LogInformation("baileys: {Linha}", linha);
        }
    }

    private static void Encerrar(Process? processo, ILogger logger)
    {
        if (processo is null)
        {
            return;
        }

        try
        {
            if (!processo.HasExited)
            {
                // entireProcessTree: o Node pode ter filho, e deixar orfao
                // seguraria a pasta na proxima publicacao.
                processo.Kill(entireProcessTree: true);
                processo.WaitForExit(5000);
            }
        }
        catch (Exception excecao)
        {
            logger.LogWarning(excecao, "Nao foi possivel encerrar o Node do Baileys.");
        }
        finally
        {
            processo.Dispose();
        }
    }

    public static string ResolverPastaDoNode(string pastaDoNode, string contentRootPath)
    {
        if (string.IsNullOrWhiteSpace(pastaDoNode))
        {
            return string.Empty;
        }

        // Mesma regra do ArmazenamentoDeImagens: caminho absoluto quando vem
        // absoluto, e relativo a raiz da publicacao quando vem com "../".
        var combinado = Path.IsPathRooted(pastaDoNode)
            ? pastaDoNode
            : Path.Combine(contentRootPath, pastaDoNode);

        return Path.GetFullPath(combinado);
    }
}
