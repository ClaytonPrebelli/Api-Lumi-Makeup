using LumiMakeup.Application.Abstractions;
using LumiMakeup.Application.DTOs;
using LumiMakeup.Domain.Entities;
using LumiMakeup.Infrastructure.Integrations;
using LumiMakeup.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LumiMakeup.Infrastructure.Services;

public sealed class GestaoDeWhatsAppService : IGestaoDeWhatsAppService
{
    private readonly LumiDbContext _contexto;
    private readonly IOptions<NotificacoesDePedidoOptions> _opcoes;

    public GestaoDeWhatsAppService(LumiDbContext contexto, IOptions<NotificacoesDePedidoOptions> opcoes)
    {
        _contexto = contexto;
        _opcoes = opcoes;
    }

    public async Task<ConfiguracaoWhatsAppDto> ObterAsync(CancellationToken cancellationToken = default)
    {
        var configuracao = await _contexto.ConfiguracoesDeWhatsApp
            .AsNoTracking()
            .OrderBy(c => c.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (configuracao is null)
        {
            return new ConfiguracaoWhatsAppDto(0, _opcoes.Value.MensagemInicialWhatsAppCliente);
        }

        return new ConfiguracaoWhatsAppDto(configuracao.Id, configuracao.MensagemInicialCliente);
    }

    public async Task<ConfiguracaoWhatsAppDto> SalvarAsync(
        RequisicaoDeConfiguracaoWhatsApp requisicao,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(requisicao.MensagemInicialCliente))
        {
            throw new InvalidOperationException("A mensagem inicial não pode estar vazia.");
        }

        if (requisicao.MensagemInicialCliente.Length > 500)
        {
            throw new InvalidOperationException("A mensagem inicial deve ter no máximo 500 caracteres.");
        }

        var existente = await _contexto.ConfiguracoesDeWhatsApp
            .OrderBy(c => c.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (existente is null)
        {
            existente = new ConfiguracaoWhatsApp
            {
                MensagemInicialCliente = requisicao.MensagemInicialCliente
            };
            _contexto.ConfiguracoesDeWhatsApp.Add(existente);
        }
        else
        {
            existente.MensagemInicialCliente = requisicao.MensagemInicialCliente;
        }

        await _contexto.SaveChangesAsync(cancellationToken);

        return new ConfiguracaoWhatsAppDto(existente.Id, existente.MensagemInicialCliente);
    }

    public async Task<PreviaMensagemWhatsAppDto> GerarPreviaAsync(CancellationToken cancellationToken = default)
    {
        var configuracao = await ObterAsync(cancellationToken);
        var mensagemInicial = configuracao.MensagemInicialCliente
            .Replace("{Nome}", "Maria")
            .Replace("{Loja}", _opcoes.Value.NomeDaLoja);

        var texto = new System.Text.StringBuilder();
        texto.AppendLine(mensagemInicial);
        texto.AppendLine();
        texto.AppendLine("**Itens do Pedido:**");
        texto.AppendLine();
        texto.AppendLine("• 1x Batom Matte - R$ 35,90");
        texto.AppendLine("• 1x Pó Compacto - R$ 45,90");
        texto.AppendLine();
        texto.AppendLine("**Subtotal:** R$ 81,80");
        texto.AppendLine("**Frete:** R$ 10,00");
        texto.AppendLine("**Total:** R$ 91,80");
        texto.AppendLine();
        texto.AppendLine("**Endereço de entrega:**");
        texto.AppendLine("Rua das Flores, 123");
        texto.AppendLine("Centro - São Paulo/SP");
        texto.AppendLine("CEP: 01310-000");
        texto.AppendLine();
        texto.AppendLine("Confirme seu pedido acima se está tudo certo por favor. É só responder esta mensagem com 'OK' ou qualquer outra coisa.");

        return new PreviaMensagemWhatsAppDto(texto.ToString().Trim());
    }
}
