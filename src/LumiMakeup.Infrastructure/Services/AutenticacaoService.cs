using LumiMakeup.Application.Abstractions;
using LumiMakeup.Application.DTOs;
using LumiMakeup.Domain.Entities;
using LumiMakeup.Domain.Enums;
using LumiMakeup.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace LumiMakeup.Infrastructure.Services;

public sealed class AutenticacaoService : IAutenticacaoService
{
    private readonly LumiDbContext _contexto;
    private readonly IPasswordHasher<Usuario> _passwordHasher;
    private readonly ITokenService _tokenService;
    private readonly IAutenticacaoGoogleService _autenticacaoGoogleService;
    private readonly IRecaptchaValidator _recaptchaValidator;
    private readonly IViaCepService _viaCepService;
    private readonly INominatimService _nominatimService;

    public AutenticacaoService(
        LumiDbContext contexto,
        IPasswordHasher<Usuario> passwordHasher,
        ITokenService tokenService,
        IAutenticacaoGoogleService autenticacaoGoogleService,
        IRecaptchaValidator recaptchaValidator,
        IViaCepService viaCepService,
        INominatimService nominatimService)
    {
        _contexto = contexto;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
        _autenticacaoGoogleService = autenticacaoGoogleService;
        _recaptchaValidator = recaptchaValidator;
        _viaCepService = viaCepService;
        _nominatimService = nominatimService;
    }

    public async Task<RespostaDeAutenticacao> CadastrarAsync(RequisicaoDeRegistro requisicao, CancellationToken cancellationToken = default)
    {
        var recaptchaValido = await _recaptchaValidator.ValidarTokenAsync(requisicao.TokenRecaptcha ?? string.Empty, cancellationToken);
        if (!recaptchaValido)
        {
            throw new InvalidOperationException("Falha na validação do reCAPTCHA.");
        }

        var email = requisicao.Email.Trim().ToLowerInvariant();

        var emailExiste = await _contexto.Usuarios.AnyAsync(u => u.Email == email, cancellationToken);
        if (emailExiste)
        {
            throw new InvalidOperationException("E-mail já cadastrado.");
        }

        if (requisicao.Senha.Length < 6)
        {
            throw new InvalidOperationException("A senha deve ter no mínimo 6 caracteres.");
        }

        var usuario = new Usuario
        {
            Nome = requisicao.Nome.Trim(),
            Email = email,
            Papel = PapelUsuario.Cliente,
            CriadoEm = DateTime.UtcNow
        };

        usuario.HashSenha = _passwordHasher.HashPassword(usuario, requisicao.Senha);

        _contexto.Usuarios.Add(usuario);
        await _contexto.SaveChangesAsync(cancellationToken);

        return ConstruirRespostaDeAutenticacao(usuario);
    }

    public async Task<RespostaDeAutenticacao> EntrarAsync(RequisicaoDeLogin requisicao, CancellationToken cancellationToken = default)
    {
        var email = requisicao.Email.Trim().ToLowerInvariant();
        var usuario = await _contexto.Usuarios.SingleOrDefaultAsync(u => u.Email == email, cancellationToken);

        if (usuario is null || string.IsNullOrEmpty(usuario.HashSenha))
        {
            throw new UnauthorizedAccessException("E-mail ou senha inválidos.");
        }

        var resultado = _passwordHasher.VerifyHashedPassword(usuario, usuario.HashSenha, requisicao.Senha);
        if (resultado == PasswordVerificationResult.Failed)
        {
            throw new UnauthorizedAccessException("E-mail ou senha inválidos.");
        }

        return ConstruirRespostaDeAutenticacao(usuario);
    }

    public async Task<RespostaDeAutenticacao> EntrarComGoogleAsync(RequisicaoDeLoginGoogle requisicao, CancellationToken cancellationToken = default)
    {
        var dados = await _autenticacaoGoogleService.ValidarTokenIdAsync(requisicao.TokenId, cancellationToken)
            ?? throw new UnauthorizedAccessException("Token do Google inválido.");

        var email = dados.Email.Trim().ToLowerInvariant();

        var usuario = await _contexto.Usuarios
            .SingleOrDefaultAsync(u => u.IdGoogle == dados.IdGoogle || u.Email == email, cancellationToken);

        if (usuario is null)
        {
            usuario = new Usuario
            {
                Nome = string.IsNullOrWhiteSpace(dados.Nome) ? email : dados.Nome,
                Email = email,
                IdGoogle = dados.IdGoogle,
                Papel = PapelUsuario.Cliente,
                CriadoEm = DateTime.UtcNow
            };

            _contexto.Usuarios.Add(usuario);
            await _contexto.SaveChangesAsync(cancellationToken);
        }
        else if (string.IsNullOrEmpty(usuario.IdGoogle))
        {
            // Vincula a conta Google ao usuário existente (mesmo e-mail).
            usuario.IdGoogle = dados.IdGoogle;
            await _contexto.SaveChangesAsync(cancellationToken);
        }

        return ConstruirRespostaDeAutenticacao(usuario);
    }

    public async Task<RespostaDeAutenticacao> RenovarAsync(string tokenRefresh, CancellationToken cancellationToken = default)
    {
        var usuarioId = _tokenService.ObterIdDeUsuarioDoTokenRefresh(tokenRefresh);
        if (usuarioId is null || !long.TryParse(usuarioId, out var id))
        {
            throw new UnauthorizedAccessException("Token de atualização inválido ou expirado.");
        }

        var usuario = await _contexto.Usuarios.FindAsync([id], cancellationToken)
            ?? throw new UnauthorizedAccessException("Usuário não encontrado.");

        return ConstruirRespostaDeAutenticacao(usuario);
    }

    public async Task<UsuarioDto> ObterUsuarioAtualAsync(long usuarioId, CancellationToken cancellationToken = default)
    {
        var usuario = await _contexto.Usuarios
            .Include(u => u.Enderecos)
            .SingleOrDefaultAsync(u => u.Id == usuarioId, cancellationToken)
            ?? throw new UnauthorizedAccessException("Usuário não encontrado.");

        return ParaUsuarioDto(usuario);
    }

    public async Task<UsuarioDto> CompletarPerfilAsync(long usuarioId, RequisicaoDeCompletarPerfil requisicao, CancellationToken cancellationToken = default)
    {
        var usuario = await _contexto.Usuarios
            .Include(u => u.Enderecos)
            .SingleOrDefaultAsync(u => u.Id == usuarioId, cancellationToken)
            ?? throw new UnauthorizedAccessException("Usuário não encontrado.");

        var cpf = new string(requisicao.Cpf.Where(char.IsDigit).ToArray());
        if (cpf.Length != 11)
        {
            throw new InvalidOperationException("CPF inválido.");
        }

        var cpfJaCadastrado = await _contexto.Usuarios.AnyAsync(u => u.Cpf == cpf && u.Id != usuarioId, cancellationToken);
        if (cpfJaCadastrado)
        {
            throw new InvalidOperationException("CPF já cadastrado.");
        }

        usuario.Nome = requisicao.Nome.Trim();
        usuario.Cpf = cpf;
        usuario.Telefone = requisicao.Telefone?.Trim();

        if (requisicao.Endereco is not null)
        {
            foreach (var enderecoPadraoAntigo in usuario.Enderecos.Where(a => a.Padrao).ToList())
            {
                usuario.Enderecos.Remove(enderecoPadraoAntigo);
            }

            var viaCep = await _viaCepService.ConsultarAsync(requisicao.Endereco.Cep, cancellationToken);
            var endereco = new Endereco
            {
                UsuarioId = usuario.Id,
                Cep = requisicao.Endereco.Cep.Trim(),
                Numero = requisicao.Endereco.Numero.Trim(),
                Complemento = requisicao.Endereco.Complemento?.Trim(),
                Padrao = true,
                Logradouro = viaCep?.Logradouro ?? string.Empty,
                Bairro = viaCep?.Bairro ?? string.Empty,
                Cidade = viaCep?.Cidade ?? string.Empty,
                Estado = viaCep?.Estado ?? string.Empty
            };

            var enderecoCompleto = $"{endereco.Logradouro}, {endereco.Numero}, {endereco.Bairro}, {endereco.Cidade} - {endereco.Estado}";
            var geo = await _nominatimService.GeocodificarAsync(enderecoCompleto, cancellationToken);
            if (geo.HasValue)
            {
                endereco.Latitude = geo.Value.Latitude;
                endereco.Longitude = geo.Value.Longitude;
            }

            usuario.Enderecos.Add(endereco);
        }

        await _contexto.SaveChangesAsync(cancellationToken);

        return ParaUsuarioDto(usuario);
    }

    private RespostaDeAutenticacao ConstruirRespostaDeAutenticacao(Usuario usuario)
    {
        var (tokenAcesso, tokenRefresh) = _tokenService.GerarTokens(usuario);
        return new RespostaDeAutenticacao(tokenAcesso, tokenRefresh, ParaUsuarioDto(usuario));
    }

    private static UsuarioDto ParaUsuarioDto(Usuario usuario)
    {
        var precisaPerfil = string.IsNullOrEmpty(usuario.Cpf);
        return new UsuarioDto(
            usuario.Id,
            usuario.Nome,
            usuario.Email,
            usuario.Cpf,
            usuario.Telefone,
            usuario.Papel,
            precisaPerfil,
            usuario.Enderecos?.Any(a => a.Padrao) ?? false);
    }
}