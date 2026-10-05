using LumiMakeup.Application.Abstractions;
using LumiMakeup.Application.DTOs;
using LumiMakeup.Domain.Entities;
using LumiMakeup.Domain.Enums;
using LumiMakeup.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LumiMakeup.Infrastructure.Services;

public sealed class RecompraService : IRecompraService
{
    private static readonly TimeSpan ValidadeDaReserva = TimeSpan.FromMinutes(10);
    private readonly LumiDbContext _contexto;
    private readonly IEmailSenderComConfirmacao _emailSender;
    private readonly TimeProvider _relogio;
    private readonly ILogger<RecompraService> _logger;

    public RecompraService(
        LumiDbContext contexto,
        IEmailSenderComConfirmacao emailSender,
        TimeProvider relogio,
        ILogger<RecompraService> logger)
    {
        _contexto = contexto;
        _emailSender = emailSender;
        _relogio = relogio;
        _logger = logger;
    }

    public async Task<ConfiguracaoDeRecompraDto> ObterConfiguracaoAsync(
        CancellationToken cancellationToken = default)
    {
        var configuracao = await _contexto.ConfiguracoesDeRecompra
            .AsNoTracking()
            .OrderBy(c => c.Id)
            .FirstOrDefaultAsync(cancellationToken);

        return configuracao is null
            ? new ConfiguracaoDeRecompraDto(false, string.Empty, string.Empty, null)
            : Mapear(configuracao);
    }

    public async Task<ConfiguracaoDeRecompraDto> SalvarConfiguracaoAsync(
        RequisicaoDeConfiguracaoDeRecompra requisicao,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(requisicao.Assunto))
        {
            throw new InvalidOperationException("Informe o assunto do e-mail.");
        }

        if (requisicao.Assunto.Trim().Length > 200)
        {
            throw new InvalidOperationException("O assunto deve ter no máximo 200 caracteres.");
        }

        if (string.IsNullOrWhiteSpace(requisicao.Mensagem))
        {
            throw new InvalidOperationException("Informe a mensagem do e-mail.");
        }

        if (requisicao.Mensagem.Length > 65_535)
        {
            throw new InvalidOperationException("A mensagem deve ter no máximo 65535 caracteres.");
        }

        var configuracao = await _contexto.ConfiguracoesDeRecompra
            .OrderBy(c => c.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (configuracao is null)
        {
            configuracao = new ConfiguracaoDeRecompra();
            _contexto.ConfiguracoesDeRecompra.Add(configuracao);
        }

        configuracao.Ativa = requisicao.Ativa;
        configuracao.Assunto = requisicao.Assunto.Trim();
        configuracao.Mensagem = requisicao.Mensagem;
        configuracao.AtualizadoEm = _relogio.GetUtcNow().UtcDateTime;

        await _contexto.SaveChangesAsync(cancellationToken);
        return Mapear(configuracao);
    }

    public async Task<ResultadoDeDisparoDeRecompraDto> DispararAsync(
        CancellationToken cancellationToken = default)
    {
        var configuracao = await _contexto.ConfiguracoesDeRecompra
            .AsNoTracking()
            .OrderBy(c => c.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (configuracao is null || !configuracao.Ativa)
        {
            return new ResultadoDeDisparoDeRecompraDto(0);
        }

        var agora = _relogio.GetUtcNow().UtcDateTime;
        var inicioDaJanela = agora.AddDays(-40);
        var fimDaJanela = agora.AddDays(-30);

        var pedidosElegiveis = await _contexto.Pedidos
            .AsNoTracking()
            .Where(p =>
                p.Status == StatusPedido.Pago &&
                p.PagoEm.HasValue &&
                p.PagoEm.Value >= inicioDaJanela &&
                p.PagoEm.Value <= fimDaJanela &&
                p.EmailContato != null &&
                p.EmailContato != string.Empty)
            .Where(p => !_contexto.Pedidos.Any(posterior =>
                posterior.UsuarioId == p.UsuarioId &&
                (posterior.CriadoEm > p.CriadoEm ||
                 (posterior.CriadoEm == p.CriadoEm && posterior.Id > p.Id))))
            .OrderBy(p => p.Id)
            .ToListAsync(cancellationToken);
        pedidosElegiveis = pedidosElegiveis
            .Where(p => !string.IsNullOrWhiteSpace(p.EmailContato))
            .ToList();

        var enviados = 0;
        foreach (var pedido in pedidosElegiveis)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var reserva = await TentarReservarAsync(pedido.Id, agora, cancellationToken);
            if (reserva is null)
            {
                continue;
            }

            bool confirmado;
            try
            {
                confirmado = await _emailSender.EnviarComConfirmacaoAsync(
                    pedido.EmailContato!.Trim(),
                    configuracao.Assunto,
                    configuracao.Mensagem,
                    cancellationToken);
            }
            catch (OperationCanceledException)
            {
                await LiberarReservaAsync(reserva);
                throw;
            }
            catch (Exception excecao)
            {
                _logger.LogError(excecao, "Falha no disparo manual de recompra para o pedido {PedidoId}", pedido.Id);
                await LiberarReservaAsync(reserva);
                continue;
            }

            if (!confirmado)
            {
                await LiberarReservaAsync(reserva);
                continue;
            }

            reserva.EnviadoEm = _relogio.GetUtcNow().UtcDateTime;
            await _contexto.SaveChangesAsync(cancellationToken);
            enviados++;
        }

        return new ResultadoDeDisparoDeRecompraDto(enviados);
    }

    private async Task<EnvioDeRecompra?> TentarReservarAsync(
        long pedidoId,
        DateTime agora,
        CancellationToken cancellationToken)
    {
        var existente = await _contexto.EnviosDeRecompra
            .SingleOrDefaultAsync(e => e.PedidoId == pedidoId, cancellationToken);

        if (existente is not null)
        {
            if (existente.EnviadoEm is not null || existente.ReservadoEm > agora - ValidadeDaReserva)
            {
                return null;
            }

            existente.ReservadoEm = agora;
            existente.Versao++;
            try
            {
                await _contexto.SaveChangesAsync(cancellationToken);
                return existente;
            }
            catch (DbUpdateConcurrencyException)
            {
                _contexto.Entry(existente).State = EntityState.Detached;
                return null;
            }
        }

        var novaReserva = new EnvioDeRecompra
        {
            PedidoId = pedidoId,
            ReservadoEm = agora,
            Versao = 1
        };
        _contexto.EnviosDeRecompra.Add(novaReserva);

        try
        {
            await _contexto.SaveChangesAsync(cancellationToken);
            return novaReserva;
        }
        catch (DbUpdateException)
        {
            _contexto.Entry(novaReserva).State = EntityState.Detached;
            return null;
        }
    }

    private async Task LiberarReservaAsync(EnvioDeRecompra reserva)
    {
        _contexto.EnviosDeRecompra.Remove(reserva);
        await _contexto.SaveChangesAsync(CancellationToken.None);
    }

    private static ConfiguracaoDeRecompraDto Mapear(ConfiguracaoDeRecompra configuracao) =>
        new(configuracao.Ativa, configuracao.Assunto, configuracao.Mensagem, configuracao.AtualizadoEm);
}
