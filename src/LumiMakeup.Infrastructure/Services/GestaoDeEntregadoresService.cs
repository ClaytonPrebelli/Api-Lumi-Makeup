using LumiMakeup.Application.Abstractions;
using LumiMakeup.Application.DTOs;
using LumiMakeup.Domain.Entities;
using LumiMakeup.Domain.Enums;
using LumiMakeup.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace LumiMakeup.Infrastructure.Services;

public sealed class GestaoDeEntregadoresService : IGestaoDeEntregadoresService
{
    private readonly LumiDbContext _contexto;
    private readonly IPasswordHasher<Usuario> _passwordHasher;

    public GestaoDeEntregadoresService(
        LumiDbContext contexto,
        IPasswordHasher<Usuario> passwordHasher)
    {
        _contexto = contexto;
        _passwordHasher = passwordHasher;
    }

    public async Task<IReadOnlyList<EntregadorDto>> ListarAsync(CancellationToken cancellationToken = default)
    {
        return await _contexto.Usuarios
            .AsNoTracking()
            .Where(u => u.Papel == PapelUsuario.Entregador)
            .OrderBy(u => u.Nome)
            .Select(u => new EntregadorDto(u.Id, u.Nome, u.Login, u.Ativo, u.CriadoEm))
            .ToListAsync(cancellationToken);
    }

    public async Task<EntregadorDto> CriarAsync(
        RequisicaoDeEntregador requisicao,
        CancellationToken cancellationToken = default)
    {
        var nome = requisicao.Nome?.Trim() ?? string.Empty;
        var login = requisicao.Login?.Trim().ToLowerInvariant() ?? string.Empty;

        if (nome.Length == 0)
        {
            throw new InvalidOperationException("Informe o nome do entregador.");
        }

        if (login.Length == 0)
        {
            throw new InvalidOperationException("Informe o usuário de login do entregador.");
        }

        if (requisicao.Senha is null || requisicao.Senha.Length < 6)
        {
            throw new InvalidOperationException("A senha deve ter no mínimo 6 caracteres.");
        }

        if (await _contexto.Usuarios.AnyAsync(u => u.Login == login, cancellationToken))
        {
            throw new InvalidOperationException($"Já existe um entregador com o usuário {login}.");
        }

        var entregador = new Usuario
        {
            Nome = nome,
            Login = login,
            Email = null,
            Papel = PapelUsuario.Entregador,
            Ativo = true,
            CriadoEm = DateTime.UtcNow
        };

        entregador.HashSenha = _passwordHasher.HashPassword(entregador, requisicao.Senha);

        _contexto.Usuarios.Add(entregador);
        await _contexto.SaveChangesAsync(cancellationToken);

        return ParaDto(entregador);
    }

    public async Task<EntregadorDto> AtualizarAsync(
        long id,
        RequisicaoDeAtualizacaoDeEntregador requisicao,
        CancellationToken cancellationToken = default)
    {
        var entregador = await _contexto.Usuarios
            .FirstOrDefaultAsync(u => u.Id == id && u.Papel == PapelUsuario.Entregador, cancellationToken)
            ?? throw new KeyNotFoundException("Entregador não encontrado.");

        var nome = requisicao.Nome?.Trim() ?? string.Empty;
        var login = requisicao.Login?.Trim().ToLowerInvariant() ?? string.Empty;

        if (nome.Length == 0)
        {
            throw new InvalidOperationException("Informe o nome do entregador.");
        }

        if (login.Length == 0)
        {
            throw new InvalidOperationException("Informe o usuário de login do entregador.");
        }

        if (await _contexto.Usuarios.AnyAsync(u => u.Id != id && u.Login == login, cancellationToken))
        {
            throw new InvalidOperationException($"Já existe um entregador com o usuário {login}.");
        }

        entregador.Nome = nome;
        entregador.Login = login;
        entregador.Ativo = requisicao.Ativo;

        // Senha em branco mantém a atual: trocar sem querer deixaria o
        // entregador trancado para fora do portal.
        if (!string.IsNullOrEmpty(requisicao.Senha))
        {
            if (requisicao.Senha.Length < 6)
            {
                throw new InvalidOperationException("A senha deve ter no mínimo 6 caracteres.");
            }

            entregador.HashSenha = _passwordHasher.HashPassword(entregador, requisicao.Senha);
        }

        await _contexto.SaveChangesAsync(cancellationToken);

        return ParaDto(entregador);
    }

    private static EntregadorDto ParaDto(Usuario entregador) => new(
        entregador.Id,
        entregador.Nome,
        entregador.Login,
        entregador.Ativo,
        entregador.CriadoEm);
}
