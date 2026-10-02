using LumiMakeup.Application.Abstractions;
using LumiMakeup.Application.DTOs;
using LumiMakeup.Domain.Entities;
using LumiMakeup.Infrastructure.Integrations;
using LumiMakeup.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LumiMakeup.Infrastructure.Services;

/// <summary>
/// Configuração do texto que abre a mensagem de WhatsApp do cliente.
///
/// A frase fica no banco, e não em <c>appsettings</c>, porque quem escreve é a
/// administradora pelo painel, no meio da operação, e um arquivo de
/// configuração não se edita sem republicar.
/// </summary>
public sealed class GestaoDeWhatsAppService : IGestaoDeWhatsAppService
{
    private const int TamanhoMaximoDaMensagem = 500;

    private readonly LumiDbContext _contexto;
    private readonly NotificacoesDePedidoOptions _opcoes;

    public GestaoDeWhatsAppService(
        LumiDbContext contexto,
        IOptions<NotificacoesDePedidoOptions> opcoes)
    {
        _contexto = contexto;
        _opcoes = opcoes.Value;
    }

    public async Task<ConfiguracaoWhatsAppDto> ObterAsync(CancellationToken cancellationToken = default)
    {
        var configuracao = await _contexto.ConfiguracoesDeWhatsApp
            .AsNoTracking()
            .OrderBy(c => c.Id)
            .FirstOrDefaultAsync(cancellationToken);

        // Loja sem linha na tabela ainda é o estado inicial normal, e a frase
        // padrão é a única que funciona sem ninguém ter digitado nada.
        if (configuracao is null)
        {
            return new ConfiguracaoWhatsAppDto(0, _opcoes.MensagemInicialWhatsAppCliente);
        }

        return new ConfiguracaoWhatsAppDto(configuracao.Id, configuracao.MensagemInicialCliente);
    }

    public async Task<ConfiguracaoWhatsAppDto> SalvarAsync(
        RequisicaoDeConfiguracaoWhatsApp requisicao,
        CancellationToken cancellationToken = default)
    {
        var mensagem = (requisicao.MensagemInicialCliente ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(mensagem))
        {
            throw new InvalidOperationException("Escreva a frase que abre a mensagem do cliente.");
        }

        if (mensagem.Length > TamanhoMaximoDaMensagem)
        {
            throw new InvalidOperationException(
                $"A frase pode ter no máximo {TamanhoMaximoDaMensagem} caracteres.");
        }

        var existente = await _contexto.ConfiguracoesDeWhatsApp
            .OrderBy(c => c.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (existente is null)
        {
            existente = new ConfiguracaoWhatsApp();
            _contexto.ConfiguracoesDeWhatsApp.Add(existente);
        }

        existente.MensagemInicialCliente = mensagem;

        await _contexto.SaveChangesAsync(cancellationToken);

        return new ConfiguracaoWhatsAppDto(existente.Id, existente.MensagemInicialCliente);
    }

    public async Task<PreviaMensagemWhatsAppDto> GerarPreviaAsync(
        string? mensagemInicial,
        CancellationToken cancellationToken = default)
    {
        // A tela manda a frase que esta no campo, e nao a que esta gravada:
        // previa da frase salva so ajuda depois de salvar, que e tarde demais
        // para quem esta decidindo o que escrever. Sem frase, mostra a gravada.
        var frase = string.IsNullOrWhiteSpace(mensagemInicial)
            ? (await ObterAsync(cancellationToken)).MensagemInicialCliente
            : mensagemInicial;

        return new PreviaMensagemWhatsAppDto(
            MontadorDeMensagemDePedido.Montar(
                frase,
                MontadorDeMensagemDePedido.PedidoDeExemplo(),
                _opcoes.NomeDaLoja));
    }
}