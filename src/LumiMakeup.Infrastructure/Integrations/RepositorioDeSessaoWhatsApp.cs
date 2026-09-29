using LumiMakeup.Domain.Entities;
using LumiMakeup.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LumiMakeup.Infrastructure.Integrations;

/// <summary>
/// Guarda e devolve a sessao do WhatsApp para o Node do Baileys.
///
/// A sessao fica no banco, e nao em arquivo, porque o Render no plano
/// gratuito nao tem disco: um arquivo se perde a cada sono do servico, e a
/// loja teria que escanear QR de novo. O WhatsApp bloqueia QR repetido com
/// frequencia, entao perder a sessao e perder o numero.
///
/// O Node nao acessa o banco. Ele pede a sessao a API e devolve o que mudou.
/// Isso mantem o MySQL fechado para a internet e evita dar credencial de banco
/// a um servico de terceiros - se a sessao vazar, quem pode ler o QR e o
/// arquivo inteiro, nao a tabela de pedidos.
/// </summary>
public sealed class RepositorioDeSessaoWhatsApp
{
    /// <summary>
    /// Chave da linha. Fixa em 1 porque a loja tem um numero so; a tabela
    /// existe com chave primaria para nao depender de "pegue a primeira linha",
    /// que quebraria no dia que houvesse a segunda.
    /// </summary>
    private const long IdUnico = 1;

    private readonly LumiDbContext _contexto;
    private readonly ILogger<RepositorioDeSessaoWhatsApp> _logger;

    public RepositorioDeSessaoWhatsApp(
        LumiDbContext contexto,
        ILogger<RepositorioDeSessaoWhatsApp> logger)
    {
        _contexto = contexto;
        _logger = logger;
    }

    /// <summary>A sessao guardada, ou null se o numero ainda nao foi pareado.</summary>
    public async Task<SessaoWhatsApp?> ObterAsync(CancellationToken cancellationToken = default) =>
        await _contexto.SessoesWhatsApp
            .AsNoTracking()
            .SingleOrDefaultAsync(s => s.Id == IdUnico, cancellationToken);

    /// <summary>
    /// Grava a sessao, recusando se a versao recebida nao for a atual.
    ///
    /// A recusa existe para o redesplie: o conteiner antigo continua drenando
    /// enquanto o novo sobe, e os dois tentam gravar. Sem o confronto de
    /// versao, o velho sobrescreveria a sessao recem-construida com a dele, e a
    /// loja ficaria sem WhatsApp ate o proximo restart.
    ///
    /// O "<c>false</c>" nao e erro: e o Node novo avisando que recarrega a
    /// sessao do banco, que ainda esta intacta.
    /// </summary>
    public async Task<bool> GravarAsync(
        string credenciais,
        string chaves,
        int versaoEsperada,
        CancellationToken cancellationToken = default)
    {
        var sessao = await _contexto.SessoesWhatsApp
            .SingleOrDefaultAsync(s => s.Id == IdUnico, cancellationToken);

        if (sessao is null)
        {
            _contexto.SessoesWhatsApp.Add(new SessaoWhatsApp
            {
                Id = IdUnico,
                Credenciais = credenciais,
                Chaves = chaves,
                Versao = 1,
                AtualizadoEm = DateTime.UtcNow
            });

            await _contexto.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Sessao do WhatsApp criada no banco (versao 1).");

            return true;
        }

        if (sessao.Versao != versaoEsperada)
        {
            _logger.LogWarning(
                "Gravacao da sessao do WhatsApp recusada: o Node mandou a versao {Esperada} e a do banco e {Atual}. " +
                "Outro contêiner escreveu. A sessao do banco esta intacta e sera recarregada.",
                versaoEsperada,
                sessao.Versao);

            return false;
        }

        sessao.Credenciais = credenciais;
        sessao.Chaves = chaves;
        sessao.Versao = versaoEsperada + 1;
        sessao.AtualizadoEm = DateTime.UtcNow;

        await _contexto.SaveChangesAsync(cancellationToken);

        return true;
    }

    /// <summary>
    /// Apaga a sessao, para o numero ser pareado de novo com QR novo.
    ///
    /// Usado quando o WhatsApp desconecta o numero de proposito. Sem isso a
    /// loja ficaria presa: o Node tentaria reconectar com credenciais que a
    /// Meta invalidou, e nem QR nem envio voltariam.
    /// </summary>
    public async Task ApagarAsync(CancellationToken cancellationToken = default)
    {
        // Remove e salva, em vez de ExecuteDelete: este ultimo e do provedor
        // relacional e nao existe no InMemory que a suite usa. O caminho
        // Remove+Save funciona nos dois e nao muda o resultado.
        var sessao = await _contexto.SessoesWhatsApp
            .SingleOrDefaultAsync(s => s.Id == IdUnico, cancellationToken);

        if (sessao is null)
        {
            return;
        }

        _contexto.SessoesWhatsApp.Remove(sessao);

        await _contexto.SaveChangesAsync(cancellationToken);

        _logger.LogWarning("Sessao do WhatsApp apagada do banco. O proximo QR sera para um novo pareamento.");
    }
}
