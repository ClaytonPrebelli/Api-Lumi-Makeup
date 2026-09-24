using LumiMakeup.Application.DTOs;

namespace LumiMakeup.Application.Abstractions;

public interface IAutenticacaoService
{
    Task<RespostaDeAutenticacao> CadastrarAsync(RequisicaoDeRegistro requisicao, CancellationToken cancellationToken = default);
    Task<RespostaDeAutenticacao> EntrarAsync(RequisicaoDeLogin requisicao, CancellationToken cancellationToken = default);
    Task<RespostaDeAutenticacao> EntrarComGoogleAsync(RequisicaoDeLoginGoogle requisicao, CancellationToken cancellationToken = default);
    Task<RespostaDeAutenticacao> RenovarAsync(string tokenRefresh, CancellationToken cancellationToken = default);
    Task<UsuarioDto> ObterUsuarioAtualAsync(long usuarioId, CancellationToken cancellationToken = default);
    Task<UsuarioDto> CompletarPerfilAsync(long usuarioId, RequisicaoDeCompletarPerfil requisicao, CancellationToken cancellationToken = default);
}