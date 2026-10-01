using LumiMakeup.Application.Abstractions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LumiMakeup.Infrastructure.Integrations;

public sealed class ArmazenamentoDeImagensOptions
{
    public string CaminhoBase { get; set; } = string.Empty;
    public string PastaPadrao { get; set; } = "produtos";
    public long TamanhoMaximoEmBytes { get; set; } = 5_242_880;
    public string[] ExtensoesPermitidas { get; set; } = { "jpg", "jpeg", "png" };
}

public sealed class ArmazenamentoDeImagensLocal : IArmazenamentoDeImagens
{
    private static readonly byte[] AssinaturaJpeg = { 0xFF, 0xD8, 0xFF };
    private static readonly byte[] AssinaturaPng = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

    private const int BytesDeCabecalho = 8;
    private const string ContentTypeJpeg = "image/jpeg";
    private const string ContentTypePng = "image/png";

    private readonly ArmazenamentoDeImagensOptions _opcoes;
    private readonly ILogger<ArmazenamentoDeImagensLocal> _logger;
    private readonly string _raiz;

    public ArmazenamentoDeImagensLocal(
        IOptions<ArmazenamentoDeImagensOptions> opcoes,
        IHostEnvironment ambiente,
        ILogger<ArmazenamentoDeImagensLocal> logger)
    {
        _opcoes = opcoes.Value;
        _logger = logger;
        _raiz = ResolverRaiz(_opcoes.CaminhoBase, ambiente.ContentRootPath);
    }

    public Task<ImagemArmazenada> ArmazenarAsync(
        Stream conteudo,
        string nomeOriginal,
        CancellationToken cancellationToken = default) =>
        ArmazenarInternoAsync(conteudo, nomeOriginal, null, cancellationToken);

    public Task<ImagemArmazenada> ArmazenarEmPastaAsync(
        Stream conteudo,
        string nomeOriginal,
        string pasta,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(pasta))
        {
            throw new InvalidOperationException("A pasta de destino da imagem é obrigatória.");
        }

        return ArmazenarInternoAsync(conteudo, nomeOriginal, pasta, cancellationToken);
    }

    private async Task<ImagemArmazenada> ArmazenarInternoAsync(
        Stream conteudo,
        string nomeOriginal,
        string? pasta,
        CancellationToken cancellationToken)
    {
        if (conteudo is null)
        {
            throw new InvalidOperationException("Nenhuma imagem foi enviada.");
        }

        var cabecalho = new byte[BytesDeCabecalho];
        var lidosNoCabecalho = await LerCabecalhoAsync(conteudo, cabecalho, cancellationToken);

        if (lidosNoCabecalho < BytesDeCabecalho)
        {
            throw new InvalidOperationException("Arquivo de imagem inválido ou corrompido.");
        }

        var (extensao, contentType) = IdentificarFormato(cabecalho);

        if (!_opcoes.ExtensoesPermitidas.Contains(extensao, StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Formato não permitido. Envie apenas: {string.Join(", ", _opcoes.ExtensoesPermitidas)}.");
        }

        var nomeSanitizado = SanearNomeOriginal(nomeOriginal);
        // A pasta pedida tem prioridade sobre a_padrao. Passar por SanearPasta
        // mantem a mesma defesa: ".." e barra invertida sao recusados.
        var pastaEscolhida = string.IsNullOrWhiteSpace(pasta)
            ? SanearPasta(_opcoes.PastaPadrao)
            : SanearPasta(pasta);
        var caminhoRelativo = $"{pastaEscolhida}/{Guid.NewGuid():N}.{extensao}";
        var caminhoAbsoluto = ResolverCaminhoSeguro(caminhoRelativo);

        var diretorio = Path.GetDirectoryName(caminhoAbsoluto)!;
        Directory.CreateDirectory(diretorio);

        try
        {
            await using var destino = new FileStream(
                caminhoAbsoluto,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 81920,
                useAsync: true);

            await destino.WriteAsync(cabecalho.AsMemory(0, BytesDeCabecalho), cancellationToken);
            var total = (long)BytesDeCabecalho + await CopiarComLimiteAsync(conteudo, destino, _opcoes.TamanhoMaximoEmBytes, cancellationToken);

            _logger.LogInformation(
                "Imagem armazenada em {CaminhoRelativo} ({TotalEmBytes} bytes, {ContentType}).",
                caminhoRelativo,
                total,
                contentType);

            return new ImagemArmazenada(caminhoRelativo, nomeSanitizado, contentType, total);
        }
        catch
        {
            ApagarSeExistir(caminhoAbsoluto);
            throw;
        }
    }

    public Task ExcluirAsync(string caminhoRelativo, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(caminhoRelativo))
        {
            return Task.CompletedTask;
        }

        /*
         * Apagar o arquivo e melhor esforço; apagar a referencia no banco e o que a
         * pessoa pediu. Se o caminho guardado for invalido - veio de outro ambiente,
         * tem "..", ou aponta para fora da raiz - a excecao do ResolverCaminhoSeguro
         * deixava a referencia impossivel de remover: o registro ficava orfao no
         * banco para sempre, sem nenhuma tela onde pudesse ser limpo.
         *
         * O caminho segue sendo validado, so nao derruba a operacao. O arquivo
         * orfao no disco, se houver, e problema menor do que um registro que
         * ninguem consegue apagar.
         */
        string caminhoAbsoluto;
        try
        {
            caminhoAbsoluto = ResolverCaminhoSeguro(caminhoRelativo);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(
                ex,
                "Caminho invalido para remocao ({CaminhoRelativo}); a referencia sera removida e o arquivo, se existir, fica orfao.",
                caminhoRelativo);
            return Task.CompletedTask;
        }

        if (ApagarSeExistir(caminhoAbsoluto))
        {
            _logger.LogInformation("Imagem removida do disco: {CaminhoRelativo}.", caminhoRelativo);
        }
        else
        {
            _logger.LogInformation(
                "Arquivo ja nao existia no disco ({CaminhoRelativo}); apenas a referencia foi removida.",
                caminhoRelativo);
        }

        return Task.CompletedTask;
    }

    public static string ResolverRaiz(string caminhoBase, string contentRootPath)
    {
        if (string.IsNullOrWhiteSpace(caminhoBase))
        {
            throw new InvalidOperationException(
                "ArmazenamentoDeImagens:CaminhoBase não configurado — imagens de produto não podem ser gravadas.");
        }

        var combinado = Path.IsPathRooted(caminhoBase)
            ? caminhoBase
            : Path.Combine(contentRootPath, caminhoBase);

        return Path.GetFullPath(combinado);
    }

    private string ResolverCaminhoSeguro(string caminhoRelativo)
    {
        var normalizado = caminhoRelativo.Replace('\\', '/').Trim();

        if (normalizado.Contains("..", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Caminho da imagem inválido.");
        }

        var combinado = Path.GetFullPath(Path.Combine(_raiz, normalizado));
        var prefixo = _raiz.EndsWith(Path.DirectorySeparatorChar)
            ? _raiz
            : _raiz + Path.DirectorySeparatorChar;

        if (!combinado.StartsWith(prefixo, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Caminho da imagem inválido.");
        }

        return combinado;
    }

    private static async Task<int> LerCabecalhoAsync(Stream conteudo, byte[] destino, CancellationToken cancellationToken)
    {
        var total = 0;

        while (total < destino.Length)
        {
            var lidos = await conteudo.ReadAsync(destino.AsMemory(total, destino.Length - total), cancellationToken);

            if (lidos == 0)
            {
                break;
            }

            total += lidos;
        }

        return total;
    }

    private static (string Extensao, string ContentType) IdentificarFormato(byte[] cabecalho)
    {
        if (ComecaCom(cabecalho, AssinaturaPng))
        {
            return ("png", ContentTypePng);
        }

        if (ComecaCom(cabecalho, AssinaturaJpeg))
        {
            return ("jpg", ContentTypeJpeg);
        }

        throw new InvalidOperationException(
            "O conteúdo do arquivo não corresponde a uma imagem JPG ou PNG válida.");
    }

    private static bool ComecaCom(byte[] cabecalho, byte[] assinatura)
    {
        if (cabecalho.Length < assinatura.Length)
        {
            return false;
        }

        for (var i = 0; i < assinatura.Length; i++)
        {
            if (cabecalho[i] != assinatura[i])
            {
                return false;
            }
        }

        return true;
    }

    private static async Task<long> CopiarComLimiteAsync(
        Stream origem,
        Stream destino,
        long limite,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[81920];
        long total = 0;
        int lidos;

        while ((lidos = await origem.ReadAsync(buffer.AsMemory(), cancellationToken)) > 0)
        {
            total += lidos;

            if (total > limite)
            {
                throw new InvalidOperationException(
                    $"A imagem excede o limite de {limite / (1024 * 1024)} MB.");
            }

            await destino.WriteAsync(buffer.AsMemory(0, lidos), cancellationToken);
        }

        return total;
    }

    private static string SanearPasta(string pasta)
    {
        if (string.IsNullOrWhiteSpace(pasta))
        {
            return "imagens";
        }

        var validos = pasta.Where(c => char.IsLetterOrDigit(c) || c is '-' or '_' or '/').ToArray();
        var resultado = new string(validos).Trim('/');

        if (string.IsNullOrWhiteSpace(resultado) || resultado.Contains("..", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("ArmazenamentoDeImagens: pasta de destino inválida.");
        }

        return resultado;
    }

    private static string SanearNomeOriginal(string nomeOriginal)
    {
        if (string.IsNullOrWhiteSpace(nomeOriginal))
        {
            return "imagem";
        }

        var nome = Path.GetFileName(nomeOriginal);
        var validos = nome.Where(c => char.IsLetterOrDigit(c) || c is '.' or '-' or '_' or ' ').ToArray();
        var resultado = new string(validos).Trim();

        return resultado.Length > 255 ? resultado[..255] : resultado.Length == 0 ? "imagem" : resultado;
    }

    private bool ApagarSeExistir(string caminhoAbsoluto)
    {
        if (!File.Exists(caminhoAbsoluto))
        {
            return false;
        }

        try
        {
            File.Delete(caminhoAbsoluto);
            return true;
        }
        catch (IOException ex)
        {
            _logger.LogWarning(ex, "Não foi possível remover o arquivo {Caminho}.", caminhoAbsoluto);
            return false;
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Sem permissão para remover o arquivo {Caminho}.", caminhoAbsoluto);
            return false;
        }
    }
}
