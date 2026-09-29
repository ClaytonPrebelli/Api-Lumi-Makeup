using LumiMakeup.Application.Abstractions;
using LumiMakeup.Application.DTOs;
using LumiMakeup.Domain.Entities;
using LumiMakeup.Domain.Enums;
using LumiMakeup.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LumiMakeup.Infrastructure.Services;

public sealed class GestaoDeEnderecosService : IGestaoDeEnderecosService
{
    private readonly LumiDbContext _contexto;
    private readonly IViaCepService _viaCep;

    public GestaoDeEnderecosService(LumiDbContext contexto, IViaCepService viaCep)
    {
        _contexto = contexto;
        _viaCep = viaCep;
    }

    public async Task<IReadOnlyList<EnderecoDto>> ListarDoUsuarioAsync(
        long usuarioId,
        CancellationToken cancellationToken = default)
    {
        return await _contexto.Enderecos
            .AsNoTracking()
            .Where(e => e.UsuarioId == usuarioId)
            .OrderByDescending(e => e.Padrao)
            .ThenBy(e => e.Id)
            .Select(e => new EnderecoDto(
                e.Id,
                e.Cep,
                e.Logradouro,
                e.Numero,
                e.Complemento,
                e.Bairro,
                e.Cidade,
                e.Estado,
                e.Padrao))
            .ToListAsync(cancellationToken);
    }

    public async Task<EnderecoDto> CriarAsync(
        long usuarioId,
        RequisicaoDeEnderecoDoPedido requisicao,
        CancellationToken cancellationToken = default)
    {
        if (!await _contexto.Usuarios.AnyAsync(u => u.Id == usuarioId, cancellationToken))
        {
            throw new KeyNotFoundException("Cliente não encontrado.");
        }

        var preenchido = await PreencherPeloCepAsync(requisicao, cancellationToken);
        var primeiro = !await _contexto.Enderecos.AnyAsync(e => e.UsuarioId == usuarioId, cancellationToken);

        var endereco = new Endereco
        {
            UsuarioId = usuarioId,
            Cep = preenchido.Cep,
            Logradouro = preenchido.Logradouro,
            Numero = preenchido.Numero,
            Complemento = preenchido.Complemento,
            Bairro = preenchido.Bairro,
            Cidade = preenchido.Cidade,
            Estado = preenchido.Estado,
            // O primeiro endereço vira padrão sozinho. Deixar a pessoa com uma
            // agenda vazia de "padrão" faria o checkout ter que perguntar qual
            // usar em uma lista de um item só.
            Padrao = primeiro
        };

        _contexto.Enderecos.Add(endereco);
        await _contexto.SaveChangesAsync(cancellationToken);

        return ParaDto(endereco);
    }

    public async Task<EnderecoDto> AtualizarNoPedidoAsync(
        long usuarioId,
        long pedidoId,
        RequisicaoDeEnderecoDoPedido requisicao,
        CancellationToken cancellationToken = default)
    {
        var pedido = await _contexto.Pedidos
            .FirstOrDefaultAsync(p => p.Id == pedidoId && p.UsuarioId == usuarioId, cancellationToken)
            ?? throw new KeyNotFoundException("Pedido não encontrado.");

        if (pedido.StatusEntrega is not StatusEntrega.NaoEnviado)
        {
            throw new InvalidOperationException(
                "Este pedido já saiu para entrega e o endereço não pode mais ser alterado.");
        }

        if (pedido.Status is StatusPedido.Cancelado)
        {
            throw new InvalidOperationException("Este pedido está cancelado.");
        }

        var preenchido = await PreencherPeloCepAsync(requisicao, cancellationToken);

        pedido.EnderecoCep = preenchido.Cep;
        pedido.EnderecoLogradouro = preenchido.Logradouro;
        pedido.EnderecoNumero = preenchido.Numero;
        pedido.EnderecoComplemento = preenchido.Complemento;
        pedido.EnderecoBairro = preenchido.Bairro;
        pedido.EnderecoCidade = preenchido.Cidade;
        pedido.EnderecoEstado = preenchido.Estado;

        // A agenda recebe a mesma correção, senão a próxima compra traria o
        // endereço antigo e a pessoa corrigiria de novo.
        var naAgenda = await _contexto.Enderecos
            .FirstOrDefaultAsync(e => e.UsuarioId == usuarioId && e.Cep == preenchido.Cep, cancellationToken);

        if (naAgenda is not null)
        {
            naAgenda.Logradouro = preenchido.Logradouro;
            naAgenda.Numero = preenchido.Numero;
            naAgenda.Complemento = preenchido.Complemento;
            naAgenda.Bairro = preenchido.Bairro;
            naAgenda.Cidade = preenchido.Cidade;
            naAgenda.Estado = preenchido.Estado;
        }

        await _contexto.SaveChangesAsync(cancellationToken);

        return new EnderecoDto(
            0,
            preenchido.Cep,
            preenchido.Logradouro,
            preenchido.Numero,
            preenchido.Complemento,
            preenchido.Bairro,
            preenchido.Cidade,
            preenchido.Estado,
            Padrao: false);
    }

    public async Task ExcluirAsync(long usuarioId, long enderecoId, CancellationToken cancellationToken = default)
    {
        var endereco = await _contexto.Enderecos
            .FirstOrDefaultAsync(e => e.Id == enderecoId && e.UsuarioId == usuarioId, cancellationToken)
            ?? throw new KeyNotFoundException("Endereço não encontrado.");

        _contexto.Enderecos.Remove(endereco);
        await _contexto.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Complementa o que a pessoa digitou com o que o CEP sabe.
    ///
    /// Logradouro, bairro, cidade e estado vêm do CEP e não são pedidos: exigir
    /// que a pessoa digite bairro e cidade só cria chance de digitar diferente do
    /// que está no registro, e o erro de entrega aparece depois.
    /// </summary>
    private async Task<RequisicaoDeEnderecoDoPedido> PreencherPeloCepAsync(
        RequisicaoDeEnderecoDoPedido requisicao,
        CancellationToken cancellationToken)
    {
        var cep = NormalizarCep(requisicao.Cep);
        var numero = requisicao.Numero?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(cep) || cep.Length != 8)
        {
            throw new InvalidOperationException("Informe um CEP válido.");
        }

        if (string.IsNullOrWhiteSpace(numero))
        {
            throw new InvalidOperationException("Informe o número.");
        }

        var consultado = await _viaCep.ConsultarAsync(cep, cancellationToken);

        if (consultado is null)
        {
            throw new InvalidOperationException("Não encontramos esse CEP. Confira os números.");
        }

        return requisicao with
        {
            Cep = cep,
            Numero = numero,
            Logradouro = string.IsNullOrWhiteSpace(requisicao.Logradouro)
                ? consultado.Logradouro
                : requisicao.Logradouro.Trim(),
            Bairro = consultado.Bairro,
            Cidade = consultado.Cidade,
            Estado = consultado.Estado,
            Complemento = string.IsNullOrWhiteSpace(requisicao.Complemento) ? null : requisicao.Complemento.Trim()
        };
    }

    /// <summary>CEP só com dígitos, com os oito números que o ViaCEP espera.</summary>
    public static string NormalizarCep(string? cep) => new((cep ?? string.Empty).Where(char.IsDigit).ToArray());

    private static EnderecoDto ParaDto(Endereco endereco) => new(
        endereco.Id,
        endereco.Cep,
        endereco.Logradouro,
        endereco.Numero,
        endereco.Complemento,
        endereco.Bairro,
        endereco.Cidade,
        endereco.Estado,
        endereco.Padrao);
}
