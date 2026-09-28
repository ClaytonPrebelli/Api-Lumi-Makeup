namespace LumiMakeup.Application.Abstractions;

/// <summary>
/// Reescreve a descrição de um produto a partir de um modelo de linguagem.
/// </summary>
/// <remarks>
/// A chave da API nunca sai do backend. A interface existe para que o provedor
/// possa ser trocado sem tocar em quem chama.
/// </remarks>
public interface IMelhoradorDeTextoService
{
    Task<ResultadoDeMelhoriaDeTexto> MelhorarAsync(
        string nome,
        string descricao,
        CancellationToken cancellationToken = default);
}

public sealed record ResultadoDeMelhoriaDeTexto(string DescricaoMelhorada, string ModeloUsado);

public static class LimitesDeMelhoriaDeTexto
{
    public const int MaximoDeCaracteresDoNome = 150;
    public const int MaximoDeCaracteresDaDescricao = 4_000;
    public const int MinimoDeCaracteresDaDescricao = 10;
}
