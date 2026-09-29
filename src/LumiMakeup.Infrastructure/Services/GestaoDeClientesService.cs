using LumiMakeup.Application.Abstractions;
using LumiMakeup.Application.DTOs;
using LumiMakeup.Domain.Entities;
using LumiMakeup.Domain.Enums;
using LumiMakeup.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LumiMakeup.Infrastructure.Services;

public sealed class GestaoDeClientesService : IGestaoDeClientesService
{
    /// <summary>
    /// Quantos clientes a busca traz. A tela de balcão mostra esse número de uma
    /// vez; traga mais e a lista vira rolagem dentro de rolagem, com o cliente
    /// certo fora do alcance sem busca nenhuma.
    /// </summary>
    private const int LimiteDaBusca = 20;

    private readonly LumiDbContext _contexto;

    public GestaoDeClientesService(LumiDbContext contexto)
    {
        _contexto = contexto;
    }

    public async Task<IReadOnlyList<ClienteResumoDto>> BuscarAsync(
        string? termo,
        CancellationToken cancellationToken = default)
    {
        var consulta = _contexto.Usuarios
            .AsNoTracking()
            .Where(u => u.Papel == PapelUsuario.Cliente);

        if (!string.IsNullOrWhiteSpace(termo))
        {
            // A busca é por "qualquer coisa que a administradora lembre": nome,
            // e-mail, telefone ou CPF. Filtrar por nome só faria ela não achar
            // quem ela conhece pelo telefone.
            //
            // O CPF é guardado com pontos e traços, então a busca o compara
            // com os dígitos do termo, sem os separadores. É o que faz
            // "123.456.789-00" achar tanto o que tem pontuação quanto o que a
            // administradora digita direto.
            var busca = termo.Trim().ToLower();
            var digitos = new string(termo.Where(char.IsDigit).ToArray());

            consulta = consulta.Where(u =>
                EF.Functions.Like(u.Nome.ToLower(), $"%{busca}%")
                || EF.Functions.Like(u.Email, $"%{busca}%")
                || (u.Telefone != null && u.Telefone.Contains(termo.Trim()))
                || (u.Cpf != null
                    && digitos.Length >= 3
                    && u.Cpf.Replace(".", "").Replace("-", "").Contains(digitos)));
        }

        return await consulta
            .OrderByDescending(u => u.CriadoEm)
            .ThenBy(u => u.Nome)
            .Take(LimiteDaBusca)
            .Select(u => new ClienteResumoDto(
                u.Id,
                u.Nome,
                u.Email,
                u.Telefone,
                u.Cpf,
                !string.IsNullOrEmpty(u.HashSenha)))
            .ToListAsync(cancellationToken);
    }

    public async Task<ClienteResumoDto> CriarAsync(
        RequisicaoDeCliente requisicao,
        CancellationToken cancellationToken = default)
    {
        var nome = requisicao.Nome?.Trim() ?? string.Empty;
        var email = requisicao.Email?.Trim().ToLowerInvariant() ?? string.Empty;
        var telefone = requisicao.Telefone?.Trim() ?? string.Empty;
        var cpf = string.IsNullOrWhiteSpace(requisicao.Cpf) ? null : requisicao.Cpf.Trim();

        if (nome.Length == 0)
        {
            throw new InvalidOperationException("Informe o nome do cliente.");
        }

        if (!email.Contains('@'))
        {
            throw new InvalidOperationException("Informe um e-mail válido: é por ele que o cliente recebe o pedido.");
        }

        // O telefone é o que faz a venda de balcão fechar. Sem ele a venda
        // acontece, mas a administradora fica sem canal para falar com quem
        // comprou, e o aviso de WhatsApp não sai.
        if (telefone.Where(char.IsDigit).Count() < 10)
        {
            throw new InvalidOperationException("Informe um telefone válido: é por ele que a loja fala com o cliente.");
        }

        if (await _contexto.Usuarios.AnyAsync(u => u.Email == email, cancellationToken))
        {
            throw new InvalidOperationException($"Já existe um cliente com o e-mail {email}.");
        }

        if (cpf is not null
            && await _contexto.Usuarios.AnyAsync(u => u.Cpf == cpf, cancellationToken))
        {
            throw new InvalidOperationException($"Já existe um cliente com o CPF {cpf}.");
        }

        // HashSenha fica vazio de propósito: a pessoa comprou na loja, mas não
        // escolheu senha nenhuma. Criar uma aqui seria inventar credencial.
        var cliente = new Usuario
        {
            Nome = nome,
            Email = email,
            Telefone = telefone,
            Cpf = cpf,
            Papel = PapelUsuario.Cliente,
            CriadoEm = DateTime.UtcNow
        };

        _contexto.Usuarios.Add(cliente);
        await _contexto.SaveChangesAsync(cancellationToken);

        return new ClienteResumoDto(
            cliente.Id,
            cliente.Nome,
            cliente.Email,
            cliente.Telefone,
            cliente.Cpf,
            TemSenha: false);
    }
}
