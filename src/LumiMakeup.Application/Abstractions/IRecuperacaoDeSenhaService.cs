using LumiMakeup.Application.DTOs;

namespace LumiMakeup.Application.Abstractions;

public interface IRecuperacaoDeSenhaService
{
    Task SolicitarAsync(RequisicaoDeSolicitarResetDeSenha requisicao, CancellationToken cancellationToken = default);
    Task<RespostaDeAutenticacao> ConfirmarAsync(RequisicaoDeConfirmarResetDeSenha requisicao, CancellationToken cancellationToken = default);
}