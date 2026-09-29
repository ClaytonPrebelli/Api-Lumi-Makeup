using System.Globalization;
using System.Text;
using LumiMakeup.Application.Abstractions;
using LumiMakeup.Application.DTOs;
using LumiMakeup.Domain.Entities;
using LumiMakeup.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LumiMakeup.Infrastructure.Services;

public sealed class GestaoDeCuponsService : IGestaoDeCuponsService
{
    /// <summary>
    /// Mensagem única para as três formas de o código não existir. Cupom inexistente,
    /// desativado e esgotado são a mesma coisa para quem está comprando, e dizer qual
    /// deles foi só entregaria informação de negócio de graça — além de permitir
    /// adivinhar um código desligado.
    /// </summary>
    private const string MensagemDeCupomInvalido = "Cupom inválido.";

    private const string MensagemDeCupomExpirado = "Cupom expirado.";

    private readonly LumiDbContext _contexto;

    public GestaoDeCuponsService(LumiDbContext contexto)
    {
        _contexto = contexto;
    }

    public async Task<IReadOnlyList<CupomDto>> ObterTodosAsync(CancellationToken cancellationToken = default)
    {
        return await _contexto.Cupons
            .AsNoTracking()
            .OrderByDescending(c => c.Ativo)
            .ThenBy(c => c.Codigo)
            .Select(c => new CupomDto(
                c.Id,
                c.Codigo,
                c.Percentual,
                c.QuantidadeDisponivel,
                c.ValorMinimo,
                c.ValidadeAte,
                c.Ativo,
                c.CriadoEm))
            .ToListAsync(cancellationToken);
    }

    public async Task<CupomDto> CriarAsync(RequisicaoDeCupom requisicao, CancellationToken cancellationToken = default)
    {
        var codigo = NormalizarCodigo(requisicao.Codigo);
        var valores = Validar(requisicao, codigo);

        if (await _contexto.Cupons.AnyAsync(c => c.Codigo == codigo, cancellationToken))
        {
            throw new InvalidOperationException($"Já existe um cupom com o código {codigo}.");
        }

        var cupom = new Cupom
        {
            Codigo = codigo,
            Percentual = valores.Percentual,
            QuantidadeDisponivel = valores.Quantidade,
            ValorMinimo = valores.ValorMinimo,
            ValidadeAte = requisicao.ValidadeAte,
            Ativo = requisicao.Ativo,
            CriadoEm = DateTime.UtcNow
        };

        _contexto.Cupons.Add(cupom);
        await _contexto.SaveChangesAsync(cancellationToken);

        return ParaDto(cupom);
    }

    public async Task<CupomDto> AtualizarAsync(
        long id,
        RequisicaoDeCupom requisicao,
        CancellationToken cancellationToken = default)
    {
        var cupom = await _contexto.Cupons.FirstOrDefaultAsync(c => c.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("Cupom não encontrado.");

        var codigo = NormalizarCodigo(requisicao.Codigo);
        var valores = Validar(requisicao, codigo);

        if (await _contexto.Cupons.AnyAsync(c => c.Codigo == codigo && c.Id != id, cancellationToken))
        {
            throw new InvalidOperationException($"Já existe um cupom com o código {codigo}.");
        }

        cupom.Codigo = codigo;
        cupom.Percentual = valores.Percentual;
        cupom.QuantidadeDisponivel = valores.Quantidade;
        cupom.ValorMinimo = valores.ValorMinimo;
        cupom.ValidadeAte = requisicao.ValidadeAte;
        cupom.Ativo = requisicao.Ativo;

        await _contexto.SaveChangesAsync(cancellationToken);

        return ParaDto(cupom);
    }

    public async Task<CupomDto> DefinirAtivoAsync(long id, bool ativo, CancellationToken cancellationToken = default)
    {
        var cupom = await _contexto.Cupons.FirstOrDefaultAsync(c => c.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("Cupom não encontrado.");

        cupom.Ativo = ativo;

        await _contexto.SaveChangesAsync(cancellationToken);

        return ParaDto(cupom);
    }

    public async Task<CupomDto> SomarQuantidadeAsync(
        long id,
        int quantidade,
        CancellationToken cancellationToken = default)
    {
        if (quantidade <= 0)
        {
            throw new InvalidOperationException("Informe quantas unidades somar.");
        }

        var cupom = await _contexto.Cupons.FirstOrDefaultAsync(c => c.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("Cupom não encontrado.");

        cupom.QuantidadeDisponivel += quantidade;

        await _contexto.SaveChangesAsync(cancellationToken);

        return ParaDto(cupom);
    }

    public async Task ExcluirAsync(long id, CancellationToken cancellationToken = default)
    {
        var cupom = await _contexto.Cupons.FirstOrDefaultAsync(c => c.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("Cupom não encontrado.");

        _contexto.Cupons.Remove(cupom);
        await _contexto.SaveChangesAsync(cancellationToken);
    }

    public async Task<AplicacaoDeCupom> CalcularAsync(
        string codigo,
        decimal subtotalDosProdutos,
        DateTime em,
        CancellationToken cancellationToken = default)
    {
        var codigoNormalizado = NormalizarCodigo(codigo);

        var cupom = await _contexto.Cupons
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Codigo == codigoNormalizado, cancellationToken);

        if (cupom is null || !cupom.Ativo || cupom.QuantidadeDisponivel <= 0)
        {
            throw new InvalidOperationException(MensagemDeCupomInvalido);
        }

        if (cupom.ValidadeAte is { } validade && validade < em)
        {
            throw new InvalidOperationException(MensagemDeCupomExpirado);
        }

        if (subtotalDosProdutos < cupom.ValorMinimo)
        {
            throw new InvalidOperationException(
                $"Este cupom vale para compras acima de {cupom.ValorMinimo.ToString("C", CultureInfo.GetCultureInfo("pt-BR"))}.");
        }

        // O desconto incide sobre o subtotal dos produtos, nunca sobre o frete.
        // Cupom sobre frete subsidiaria o transporte em vez da mercadoria. Por isso
        // o parametro se chama subtotalDosProdutos e nao total.
        var desconto = Math.Round(
            subtotalDosProdutos * cupom.Percentual / 100m,
            2,
            MidpointRounding.AwayFromZero);

        return new AplicacaoDeCupom(cupom.Id, cupom.Codigo, cupom.Percentual, desconto);
    }

    /// <summary>
    /// Consome uma unidade do cupom.
    ///
    /// A quantidade é token de concorrência (ver <c>CupomConfiguration</c>), então o
    /// UPDATE do EF leva o valor lido no WHERE. Se outra requisição alterou a
    /// quantidade no meio, o UPDATE afeta zero linhas e o EF lança
    /// <see cref="DbUpdateConcurrencyException"/>, que aqui vira a mesma mensagem de
    /// cupom inválido.
    ///
    /// Sem isso, dois clientes validando o mesmo código com uma única unidade
    /// sobrando levariam os dois: a validação não reserva, e a escrita posterior
    /// leria o mesmo número.
    /// </summary>
    public async Task ConsumirAsync(long cupomId, CancellationToken cancellationToken = default)
    {
        var cupom = await _contexto.Cupons.FirstOrDefaultAsync(c => c.Id == cupomId, cancellationToken)
            ?? throw new KeyNotFoundException("Cupom não encontrado.");

        if (cupom.QuantidadeDisponivel <= 0)
        {
            throw new InvalidOperationException(MensagemDeCupomInvalido);
        }

        cupom.QuantidadeDisponivel--;

        try
        {
            await _contexto.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // A outra requisição levou a última unidade. A mensagem é a de sempre,
            // porque para o cliente o código deixou de valer — e o motivo exato é
            // informação de negócio.
            throw new InvalidOperationException(MensagemDeCupomInvalido);
        }
    }

    public async Task DevolverAsync(long cupomId, CancellationToken cancellationToken = default)
    {
        var cupom = await _contexto.Cupons.FirstOrDefaultAsync(c => c.Id == cupomId, cancellationToken)
            ?? throw new KeyNotFoundException("Cupom não encontrado.");

        cupom.QuantidadeDisponivel++;
        await _contexto.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Deixa o código como o cliente digita: maiúsculo, sem acento e sem espaço.
    ///
    /// Sem isso, "NATAL20", "natal20" e " natal20 " seriam três códigos, e quem
    /// digitasse minúsculo receberia "cupom inválido" de um cupom válido — a falha
    /// mais irritante de um campo de texto, porque parece o sistema estar errado.
    /// </summary>
    public static string NormalizarCodigo(string codigo)
    {
        if (string.IsNullOrWhiteSpace(codigo))
        {
            return string.Empty;
        }

        var semAcento = new StringBuilder(codigo.Length);
        var normalizado = codigo.Trim().Normalize(NormalizationForm.FormD);

        foreach (var caractere in normalizado)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(caractere) != UnicodeCategory.NonSpacingMark)
            {
                semAcento.Append(caractere);
            }
        }

        // O filtro precisa virar string explicitamente: encadear .Where(...).ToString()
        // devolveria o nome do tipo enumeravel, e todo codigo gravado ficaria
        // inencontravel na hora de validar.
        var filtrado = semAcento
            .ToString()
            .Normalize(NormalizationForm.FormC)
            .ToUpperInvariant()
            .Where(c => char.IsLetterOrDigit(c) || c is '-' or '_');

        return new string(filtrado.ToArray());
    }

    private static (decimal Percentual, int Quantidade, decimal ValorMinimo) Validar(
        RequisicaoDeCupom requisicao,
        string codigoNormalizado)
    {
        if (string.IsNullOrWhiteSpace(codigoNormalizado))
        {
            throw new InvalidOperationException(
                "O código do cupom precisa ter letras ou números. Acento e espaço não contam.");
        }

        if (requisicao.Percentual <= 0 || requisicao.Percentual > 100)
        {
            throw new InvalidOperationException("O desconto precisa estar entre 0 e 100%.");
        }

        if (requisicao.QuantidadeDisponivel < 0)
        {
            throw new InvalidOperationException("A quantidade não pode ser negativa.");
        }

        if (requisicao.ValorMinimo < 0)
        {
            throw new InvalidOperationException("O valor mínimo não pode ser negativo.");
        }

        return (requisicao.Percentual, requisicao.QuantidadeDisponivel, requisicao.ValorMinimo);
    }

    private static CupomDto ParaDto(Cupom cupom) => new(
        cupom.Id,
        cupom.Codigo,
        cupom.Percentual,
        cupom.QuantidadeDisponivel,
        cupom.ValorMinimo,
        cupom.ValidadeAte,
        cupom.Ativo,
        cupom.CriadoEm);
}
