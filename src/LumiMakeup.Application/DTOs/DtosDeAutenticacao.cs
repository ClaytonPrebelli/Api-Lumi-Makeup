using LumiMakeup.Domain.Enums;

namespace LumiMakeup.Application.DTOs;

public sealed record RequisicaoDeRegistro(string Nome, string Email, string Senha, string? TokenRecaptcha);

public sealed record RequisicaoDeLogin(string Email, string Senha);

public sealed record RequisicaoDeLoginGoogle(string TokenId);

public sealed record RequisicaoDeRenovacao(string TokenRefresh);

public sealed record RequisicaoDeSolicitarResetDeSenha(string Email);

public sealed record RequisicaoDeConfirmarResetDeSenha(string Token, string NovaSenha);

public sealed record RequisicaoDeCompletarPerfil(
    string Nome,
    string Cpf,
    string? Telefone,
    RequisicaoDeEndereco? Endereco);

public sealed record RequisicaoDeEndereco(
    string Cep,
    string Numero,
    string? Complemento);

public sealed record UsuarioDto(
    long Id,
    string Nome,
    string Email,
    string? Cpf,
    string? Telefone,
    PapelUsuario Papel,
    bool PrecisaPerfil,
    bool TemEndereco);

public sealed record RespostaDeAutenticacao(
    string TokenAcesso,
    string TokenRefresh,
    UsuarioDto Usuario);