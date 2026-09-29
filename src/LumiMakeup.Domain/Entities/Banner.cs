namespace LumiMakeup.Domain.Entities;

/// <summary>
/// Um slide do carrossel do hero da home.
///
/// Cada banner carrega duas imagens: a de desktop e a de celular. São arquivos
/// diferentes porque a proporção muda muito - um banner de 1600x600 cortado para
/// uma tela de 375px perde o assunto ou deixa a arte esticada. Guardar as duas
/// versões deixa o navegador escolher pela tag picture, sem JavaScript.
///
/// A ordem define a sequência do carrossel, e <see cref="Ativo"/> tira o slide do
/// ar sem precisar apagar nada: um banner sazonal volta a aparecer em segundos.
/// </summary>
public class Banner
{
    public long Id { get; set; }

    /// <summary>Caminho relativo da imagem usada em telas largas.</summary>
    public string CaminhoRelativoDesktop { get; set; } = string.Empty;

    /// <summary>Caminho relativo da imagem usada em telas estreitas.</summary>
    public string CaminhoRelativoMobile { get; set; } = string.Empty;

    public string NomeOriginalDesktop { get; set; } = string.Empty;
    public string NomeOriginalMobile { get; set; } = string.Empty;

    /// <summary>Texto alternativo da imagem de desktop.</summary>
    public string? TextoAlternativo { get; set; }

    public int Ordem { get; set; }
    public bool Ativo { get; set; } = true;
    public DateTime CriadoEm { get; set; }
}
