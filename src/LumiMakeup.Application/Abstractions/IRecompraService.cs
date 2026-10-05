using LumiMakeup.Application.DTOs;

namespace LumiMakeup.Application.Abstractions;

public interface IRecompraService
{
    Task<ConfiguracaoDeRecompraDto> ObterConfiguracaoAsync(CancellationToken cancellationToken = default);

    Task<ConfiguracaoDeRecompraDto> SalvarConfiguracaoAsync(
        RequisicaoDeConfiguracaoDeRecompra requisicao,
        CancellationToken cancellationToken = default);

    Task<ResultadoDeDisparoDeRecompraDto> DispararAsync(CancellationToken cancellationToken = default);
}
