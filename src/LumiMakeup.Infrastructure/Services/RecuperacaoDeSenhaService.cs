using System.Net;
using System.Security.Cryptography;
using System.Text;
using LumiMakeup.Application.Abstractions;
using LumiMakeup.Application.DTOs;
using LumiMakeup.Domain.Entities;
using LumiMakeup.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LumiMakeup.Infrastructure.Services;

public sealed class FrontendOptions
{
    public string UrlBase { get; set; } = string.Empty;
    public string RotaDeRedefinicaoDeSenha { get; set; } = "/redefinir-senha";
    public int MinutosDeExpiracaoDoTokenDeReset { get; set; } = 30;
}

public sealed class RecuperacaoDeSenhaService : IRecuperacaoDeSenhaService
{
    private const string AssuntoDeRedefinicaoDeSenha = "Redefinição de senha – Lumi Makeup";
    private const string AssuntoDeDefinicaoDeSenha = "Definição de senha – Lumi Makeup";

    private readonly LumiDbContext _contexto;
    private readonly IPasswordHasher<Usuario> _passwordHasher;
    private readonly ITokenService _tokenService;
    private readonly IEmailSender _emailSender;
    private readonly IOptions<FrontendOptions> _opcoesDoFrontend;

    public RecuperacaoDeSenhaService(
        LumiDbContext contexto,
        IPasswordHasher<Usuario> passwordHasher,
        ITokenService tokenService,
        IEmailSender emailSender,
        IOptions<FrontendOptions> opcoesDoFrontend)
    {
        _contexto = contexto;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
        _emailSender = emailSender;
        _opcoesDoFrontend = opcoesDoFrontend;
    }

    public async Task SolicitarAsync(RequisicaoDeSolicitarResetDeSenha requisicao, CancellationToken cancellationToken = default)
    {
        var email = requisicao.Email.Trim().ToLowerInvariant();
        var usuario = await _contexto.Usuarios
            .SingleOrDefaultAsync(u => u.Email == email, cancellationToken);

        if (usuario is null)
        {
            return;
        }

        var temSenha = !string.IsNullOrEmpty(usuario.HashSenha);
        var token = GerarToken();
        var agora = DateTime.UtcNow;
        var minutosDeExpiracao = _opcoesDoFrontend.Value.MinutosDeExpiracaoDoTokenDeReset;

        var recuperacoesAnteriores = await _contexto.RecuperacoesDeSenha
            .Where(r => r.UsuarioId == usuario.Id)
            .ToListAsync(cancellationToken);
        _contexto.RecuperacoesDeSenha.RemoveRange(recuperacoesAnteriores);

        _contexto.RecuperacoesDeSenha.Add(new RecuperacaoDeSenha
        {
            UsuarioId = usuario.Id,
            HashToken = CalcularHashDoToken(token),
            CriadoEm = agora,
            ExpiracaoEm = agora.AddMinutes(minutosDeExpiracao)
        });

        await _contexto.SaveChangesAsync(cancellationToken);

        var linkDeRedefinicao = $"{_opcoesDoFrontend.Value.UrlBase}{_opcoesDoFrontend.Value.RotaDeRedefinicaoDeSenha}?token={token}";
        var corpoHtml = ConstruirCorpoDoEmail(usuario.Nome, linkDeRedefinicao, minutosDeExpiracao, temSenha);
        var assunto = temSenha ? AssuntoDeRedefinicaoDeSenha : AssuntoDeDefinicaoDeSenha;

        await _emailSender.EnviarAsync(usuario.Email, assunto, corpoHtml, cancellationToken);
    }

    public async Task<RespostaDeAutenticacao> ConfirmarAsync(RequisicaoDeConfirmarResetDeSenha requisicao, CancellationToken cancellationToken = default)
    {
        if (requisicao.NovaSenha.Length < 6)
        {
            throw new InvalidOperationException("A senha deve ter no mínimo 6 caracteres.");
        }

        var recuperacao = await _contexto.RecuperacoesDeSenha
            .Include(r => r.Usuario)
            .SingleOrDefaultAsync(r => r.HashToken == CalcularHashDoToken(requisicao.Token), cancellationToken);

        if (recuperacao is null || recuperacao.UtilizadoEm.HasValue || recuperacao.ExpiracaoEm < DateTime.UtcNow)
        {
            throw new UnauthorizedAccessException("Token de recuperação inválido ou expirado.");
        }

        var usuario = recuperacao.Usuario;
        usuario.HashSenha = _passwordHasher.HashPassword(usuario, requisicao.NovaSenha);

        var outrasRecuperacoes = await _contexto.RecuperacoesDeSenha
            .Where(r => r.UsuarioId == usuario.Id && r.Id != recuperacao.Id)
            .ToListAsync(cancellationToken);
        _contexto.RecuperacoesDeSenha.RemoveRange(outrasRecuperacoes);

        recuperacao.UtilizadoEm = DateTime.UtcNow;
        await _contexto.SaveChangesAsync(cancellationToken);

        return ConstruirRespostaDeAutenticacao(usuario);
    }

    private RespostaDeAutenticacao ConstruirRespostaDeAutenticacao(Usuario usuario)
    {
        var (tokenAcesso, tokenRefresh) = _tokenService.GerarTokens(usuario);
        var precisaPerfil = string.IsNullOrEmpty(usuario.Cpf);
        var usuarioDto = new UsuarioDto(
            usuario.Id,
            usuario.Nome,
            usuario.Email,
            usuario.Cpf,
            usuario.Telefone,
            usuario.Papel,
            precisaPerfil,
            false);

        return new RespostaDeAutenticacao(tokenAcesso, tokenRefresh, usuarioDto);
    }

    private static string GerarToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static string CalcularHashDoToken(string token)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private static string ConstruirCorpoDoEmail(string nome, string linkDeRedefinicao, int minutosDeExpiracao, bool temSenha)
    {
        var nomeSeguro = WebUtility.HtmlEncode(nome);
        var titulo = temSenha ? "Redefinição de senha" : "Definição de senha";
        var botao = temSenha ? "Redefinir minha senha" : "Definir minha senha";
        var introducao = temSenha
            ? $"Olá, <strong>{nomeSeguro}</strong>! Recebemos uma solicitação para redefinir a senha da sua conta na <strong>Lumi Makeup</strong>. Se foi você, basta confirmar pelo botão abaixo."
            : $"Olá, <strong>{nomeSeguro}</strong>! Você criou sua conta com o Google e ainda não tem senha. Para acessar também com senha, defina uma pelo botão abaixo.";
        var observacaoDeSeguranca = temSenha
            ? "Se você não solicitou a redefinição, ignore este e-mail — a sua senha continua segura."
            : "Se você não pediu essa definição de senha, ignore este e-mail — a sua conta continua segura.";

        return $"""
                <!DOCTYPE html>
                <html lang="pt-BR">
                <body style="margin:0;padding:0;background-color:#f3e4da;">
                  <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="background-color:#f3e4da;padding:32px 12px;">
                    <tr>
                      <td align="center">
                        <table role="presentation" width="600" cellpadding="0" cellspacing="0" style="width:100%;max-width:600px;background-color:#ffffff;border-radius:10px;box-shadow:0 4px 14px rgba(74,51,44,0.08);overflow:hidden;">
                          <tr>
                            <td style="height:6px;background-color:#b98b73;"></td>
                          </tr>
                          <tr>
                            <td style="padding:40px 44px 32px 44px;text-align:center;">
                              <div style="font-family:Georgia,'Playfair Display',serif;font-size:28px;letter-spacing:3px;color:#8b5e52;font-weight:600;">LUMI&nbsp;MAKEUP</div>
                              <div style="font-family:'Brush Script MT','Segoe Print',cursive;font-size:20px;color:#b98b73;margin-top:2px;">Seu brilho começa aqui</div>
                              <div style="color:#b98b73;font-size:14px;margin:18px 0 22px 0;">&#10084;&nbsp;&nbsp;&#10084;&nbsp;&nbsp;&#10084;</div>
                              <h1 style="font-family:Georgia,'Playfair Display',serif;font-size:26px;color:#4a332c;margin:0 0 10px 0;font-weight:600;">{titulo}</h1>
                              <p style="font-family:Arial,'Inter',sans-serif;font-size:15px;line-height:1.6;color:#4a332c;margin:0 0 26px 0;">
                                {introducao}
                              </p>
                              <a href="{linkDeRedefinicao}" style="background-color:#b98b73;color:#ffffff;padding:15px 34px;border-radius:10px;font-family:Arial,'Inter',sans-serif;font-size:16px;font-weight:700;text-decoration:none;display:inline-block;line-height:1.4;">{botao}</a>
                              <p style="font-family:Arial,'Inter',sans-serif;font-size:13px;line-height:1.6;color:#8a7268;margin:22px 0 0 0;">
                                Se o botão não funcionar, copie e cole este link no navegador:<br />
                                <a href="{linkDeRedefinicao}" style="color:#8b5e52;word-break:break-all;">{linkDeRedefinicao}</a>
                              </p>
                              <p style="font-family:Arial,'Inter',sans-serif;font-size:13px;line-height:1.6;color:#8a7268;margin:16px 0 0 0;">
                                Este link é válido por <strong>{minutosDeExpiracao} minutos</strong> e pode ser usado uma única vez.
                                {observacaoDeSeguranca}
                              </p>
                            </td>
                          </tr>
                          <tr>
                            <td style="padding:26px 44px;border-top:1px solid #e7d6ca;text-align:center;">
                              <div style="font-family:Georgia,'Playfair Display',serif;font-size:16px;color:#8b5e52;">Equipe Lumi Makeup</div>
                              <div style="font-family:Arial,'Inter',sans-serif;font-size:12px;color:#8a7268;margin-top:6px;">nao-responda@lumimakeup.com.br &bull; mensagem automática, não responda.</div>
                            </td>
                          </tr>
                        </table>
                      </td>
                    </tr>
                  </table>
                </body>
                </html>
                """;
    }
}