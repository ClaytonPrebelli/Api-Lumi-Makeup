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
    private readonly EstadoDoNodeBaileys _estado;
    private readonly string _pastaDoNode;

    private Process? _processo;

    public SupervisorDeNodeBaileys(
        IOptions<OpcoesDeBaileys> opcoes,
        IHostEnvironment ambiente,
        EstadoDoNodeBaileys estado,
        ILogger<SupervisorDeNodeBaileys> logger)
    {
        _opcoes = opcoes.Value;
        _ambiente = ambiente;
        _estado = estado;
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

        _estado.RegistrarSupervisorAtivo(_pastaDoNode);

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

        // Cada motivo vira frase que a administradora le na tela. Todas as
        // causas produzem o mesmo sintoma - Node parado - e sem isso nao
        // haveria como saber qual delas aconteceu, sem console no servidor.
        if (string.IsNullOrWhiteSpace(_pastaDoNode))
        {
            Falhar("A pasta do Node nao esta configurada. Cadastre BAILEYS_PASTA_DO_NODE no secret do GitHub.");
            return;
        }

        if (!Directory.Exists(_pastaDoNode))
        {
            Falhar(
                $"A pasta do Node nao existe: {_pastaDoNode}. Envie os arquivos do servico por FTP para essa pasta.");
            return;
        }

        if (!File.Exists(executavel))
        {
            Falhar(
                $"O node.exe nao esta em {_pastaDoNode}. Envie a versao PORTATIL do Node por FTP, junto do codigo do servico.");
            return;
        }

        if (!File.Exists(entrada))
        {
            Falhar($"O codigo do servico nao esta em {entrada}. Envie a pasta src/ por FTP.");
            return;
        }

        try
        {
            var informacoes = MontarInformacoesDoProcesso(
                executavel,
                entrada,
                _pastaDoNode,
                _opcoes.Porta,
                _opcoes.SegredoCompartilhado,
                UsarArquivoEnv());

            var processo = new Process { StartInfo = informacoes, EnableRaisingEvents = true };

            processo.OutputDataReceived += (_, evento) => RegistrarSaida(evento.Data, false);
            processo.ErrorDataReceived += (_, evento) => RegistrarSaida(evento.Data, true);

            processo.Start();
            processo.BeginOutputReadLine();
            processo.BeginErrorReadLine();

            _processo = processo;

            // Limpa o problema anterior: o Node subiu, entao o que houve antes
            // nao interessa mais para quem olha a tela.
            _estado.RegistrarNodeNoAr();

            _logger.LogInformation(
                "Node do Baileys iniciado (pid {Pid}) em {Pasta}.",
                processo.Id,
                _pastaDoNode);
        }
        catch (Exception excecao)
        {
            // Este e o motivo mais comum em producao: o pool do IIS roda com
            // permissao restrita, e o Windows recusa executar um .exe de fora
            // da propria pasta. A mensagem diz o que fazer.
            Falhar(
                $"A API nao conseguiu executar o Node: {excecao.Message}. " +
                "Se for acesso negado, o pool do IIS precisa de permissao de execucao nessa pasta.");

            _processo = null;
        }
    }

    /// <summary>Registra o motivo e deixa claro no log. Usado em toda falha de subida.</summary>
    private void Falhar(string motivo)
    {
        _estado.RegistrarProblema(motivo);
        _logger.LogError("Node do Baileys nao subiu. {Motivo}", motivo);
    }

    /// <summary>
    /// O Node so le o arquivo .env quando recebe a flag <c>--env-file</c>.
    /// Sem ela o processo sobe sem segredo nenhum e morre na largada.
    ///
    /// A flag so entra quando o arquivo existe. Se o .env nao estiver la - em
    /// desenvolvimento, por exemplo, onde o Node roda na mao - o processo sobe
    /// so com o ambiente, e nao falha por causa de um arquivo que ninguem pediu.
    /// </summary>
    private bool UsarArquivoEnv() =>
        File.Exists(Path.Combine(_pastaDoNode, ".env"));

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
        string segredoCompartilhado,
        bool usarArquivoEnv = false)
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

        // A flag vem ANTES do script. O Node so aplica o --env-file quando ele
        // aparece antes do arquivo de entrada; depois, ele trata como argumento
        // do script e o processo sobe sem ler o .env.
        if (usarArquivoEnv)
        {
            informacoes.ArgumentList.Add("--env-file=.env");
        }

        informacoes.ArgumentList.Add(entrada);

        // O segredo tambem vai por ambiente, e nao so pelo .env. Sao dois
        // caminhos independentes para o mesmo valor: o .env para subir o Node
        // na mao, e o ambiente para quando quem sobe e a API. O ultimo a
        // carregar ganha, e os dois tem o mesmo texto.
        //
        // Por ambiente e nao por linha de comando porque argumento aparece na
        // lista de processos do Windows, visivel para qualquer usuario da
        // maquina.
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

            // Node que sai sozinho reaparece logo: quase sempre e o .env que o
            // processo subiu sem ler. A tela precisa mostrar isso, e nao um
            // "parado" sem pista.
            var causaProvavel = string.IsNullOrWhiteSpace(_opcoes.SegredoCompartilhado)
                ? "O Node subiu e saiu. Quase sempre e o segredo ausente: confira BAILEYS_SEGREDO_COMPARTILHADO no GitHub e o .env na pasta do Node."
                : $"O Node subiu e saiu com codigo {processo.ExitCode}. Veja o log da API, na linha que comeca com 'baileys:', para o motivo.";

            _estado.RegistrarProblema(causaProvavel);

            _logger.LogWarning("O Node do Baileys saiu com codigo {Codigo}. {Causa}", processo.ExitCode, causaProvavel);

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
