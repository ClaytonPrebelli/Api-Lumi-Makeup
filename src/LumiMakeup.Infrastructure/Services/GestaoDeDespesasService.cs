using LumiMakeup.Application.Abstractions;
using LumiMakeup.Application.DTOs;
using LumiMakeup.Domain.Entities;
using LumiMakeup.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LumiMakeup.Infrastructure.Services;

public sealed class GestaoDeDespesasService : IGestaoDeDespesasService
{
    private readonly LumiDbContext _contexto;

    public GestaoDeDespesasService(LumiDbContext contexto)
    {
        _contexto = contexto;
    }

    public async Task<IReadOnlyList<DespesaDto>> ListarAsync(
        DateTime? inicio = null,
        DateTime? fim = null,
        string? categoria = null,
        CancellationToken cancellationToken = default)
    {
        var consulta = _contexto.Despesas.AsNoTracking();

        if (inicio.HasValue)
        {
            consulta = consulta.Where(d => d.DataDaDespesa >= inicio.Value);
        }

        if (fim.HasValue)
        {
            var fimDoDia = fim.Value.Date.AddDays(1).AddTicks(-1);
            consulta = consulta.Where(d => d.DataDaDespesa <= fimDoDia);
        }

        if (!string.IsNullOrWhiteSpace(categoria))
        {
            consulta = consulta.Where(d => d.Categoria == categoria);
        }

        return await consulta
            .OrderByDescending(d => d.DataDaDespesa)
            .ThenByDescending(d => d.Id)
            .Select(d => new DespesaDto(
                d.Id,
                d.Descricao,
                d.Categoria,
                d.Valor,
                d.DataDaDespesa,
                d.CriadoPor,
                d.CriadoPorUsuario.Nome,
                d.CriadoEm))
            .ToListAsync(cancellationToken);
    }

    public async Task<DespesaDto> CriarAsync(
        RequisicaoDeDespesa requisicao,
        long usuarioId,
        CancellationToken cancellationToken = default)
    {
        if (!await _contexto.Usuarios.AnyAsync(u => u.Id == usuarioId, cancellationToken))
        {
            throw new KeyNotFoundException("Usuário não encontrado.");
        }

        Validar(requisicao);

        var despesa = new Despesa
        {
            Descricao = requisicao.Descricao.Trim(),
            Categoria = requisicao.Categoria.Trim(),
            Valor = requisicao.Valor,
            DataDaDespesa = requisicao.DataDaDespesa,
            CriadoPor = usuarioId
        };

        _contexto.Despesas.Add(despesa);
        await _contexto.SaveChangesAsync(cancellationToken);

        var usuario = await _contexto.Usuarios
            .AsNoTracking()
            .FirstAsync(u => u.Id == usuarioId, cancellationToken);

        return new DespesaDto(
            despesa.Id,
            despesa.Descricao,
            despesa.Categoria,
            despesa.Valor,
            despesa.DataDaDespesa,
            despesa.CriadoPor,
            usuario.Nome,
            despesa.CriadoEm);
    }

    public async Task<DespesaDto> AtualizarAsync(
        long id,
        RequisicaoDeAtualizacaoDeDespesa requisicao,
        CancellationToken cancellationToken = default)
    {
        Validar(requisicao);

        var despesa = await _contexto.Despesas
            .Include(d => d.CriadoPorUsuario)
            .FirstOrDefaultAsync(d => d.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("Despesa não encontrada.");

        despesa.Descricao = requisicao.Descricao.Trim();
        despesa.Categoria = requisicao.Categoria.Trim();
        despesa.Valor = requisicao.Valor;
        despesa.DataDaDespesa = requisicao.DataDaDespesa;

        await _contexto.SaveChangesAsync(cancellationToken);

        return new DespesaDto(
            despesa.Id,
            despesa.Descricao,
            despesa.Categoria,
            despesa.Valor,
            despesa.DataDaDespesa,
            despesa.CriadoPor,
            despesa.CriadoPorUsuario.Nome,
            despesa.CriadoEm);
    }

    public async Task ExcluirAsync(long id, CancellationToken cancellationToken = default)
    {
        var despesa = await _contexto.Despesas
            .FirstOrDefaultAsync(d => d.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("Despesa não encontrada.");

        _contexto.Despesas.Remove(despesa);
        await _contexto.SaveChangesAsync(cancellationToken);
    }

    private static void Validar(RequisicaoDeDespesa requisicao)
    {
        if (string.IsNullOrWhiteSpace(requisicao.Descricao))
        {
            throw new InvalidOperationException("A descrição é obrigatória.");
        }

        if (string.IsNullOrWhiteSpace(requisicao.Categoria))
        {
            throw new InvalidOperationException("A categoria é obrigatória.");
        }

        if (requisicao.Valor <= 0)
        {
            throw new InvalidOperationException("O valor precisa ser maior que zero.");
        }
    }

    private static void Validar(RequisicaoDeAtualizacaoDeDespesa requisicao)
    {
        if (string.IsNullOrWhiteSpace(requisicao.Descricao))
        {
            throw new InvalidOperationException("A descrição é obrigatória.");
        }

        if (string.IsNullOrWhiteSpace(requisicao.Categoria))
        {
            throw new InvalidOperationException("A categoria é obrigatória.");
        }

        if (requisicao.Valor <= 0)
        {
            throw new InvalidOperationException("O valor precisa ser maior que zero.");
        }
    }
}