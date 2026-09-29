using LumiMakeup.Infrastructure.Integrations;
using LumiMakeup.Tests.Helpers;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Net;
using System.Text.Json;

namespace LumiMakeup.Tests.Infrastructure.Integrations;

public class BaileysWhatsAppServiceTests
{
    private static BaileysWhatsAppService CriarServico(
        HttpMessageHandler manipulador,
        string segredo = "segredo-de-teste",
        string urlBase = "https://localhost:3001/")
    {
        var http = Testes.CriarHttpClient(manipulador, urlBase);
        var opcoes = Options.Create(new OpcoesDeBaileys
        {
            Habilitado = true,
            SegredoCompartilhado = segredo,
            UrlBase = urlBase
        });

        return new BaileysWhatsAppService(http, opcoes, NullLogger<BaileysWhatsAppService>.Instance);
    }

    [Fact]
    public async Task envia_para_o_endpoint_de_envio_com_o_segredo_no_cabecalho()
    {
        HttpRequestMessage? recebida = null;
        string? corpo = null;

        var manipulador = new Testes.ManipuladorHttpSimulado(
            requisicao =>
            {
                recebida = requisicao;
                corpo = requisicao.Content!.ReadAsStringAsync().GetAwaiter().GetResult();

                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"enviada\":true}")
                };
            });

        var servico = CriarServico(manipulador);

        var enviada = await servico.EnviarMensagemAsync("5511999999999", "Ola", CancellationToken.None);

        Assert.True(enviada);
        Assert.Equal(HttpMethod.Post, recebida!.Method);
        Assert.Equal("/enviar", recebida.RequestUri!.AbsolutePath);
        Assert.Equal("segredo-de-teste", recebida.Headers.GetValues("x-segredo").Single());

        // O Node so aceita digitos com codigo do pais, entao o corpo tem que
        // chegar com o telefone e a mensagem, e nada alem.
        using var json = JsonDocument.Parse(corpo!);
        Assert.Equal("5511999999999", json.RootElement.GetProperty("telefone").GetString());
        Assert.Equal("Ola", json.RootElement.GetProperty("mensagem").GetString());
    }

    [Fact]
    public async Task devolve_false_sem_lancar_quando_o_numero_ainda_nao_esta_pareado()
    {
        var manipulador = new Testes.ManipuladorHttpSimulado(
            new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
            {
                Content = new StringContent("{\"mensagem\":\"nao pareado\"}")
            });

        var servico = CriarServico(manipulador);

        var enviada = await servico.EnviarMensagemAsync("5511999999999", "Ola", CancellationToken.None);

        // 503 e o Node no ar sem sessao. Nao e excecao: o NotificadorDePedido
        // registra e o pedido segue.
        Assert.False(enviada);
    }

    [Fact]
    public async Task devolve_false_sem_lancar_quando_o_node_esta_fora_do_ar()
    {
        var manipulador = new ManipuladorQueFalha();
        var servico = CriarServico(manipulador);

        var enviada = await servico.EnviarMensagemAsync("5511999999999", "Ola", CancellationToken.None);

        // Node morto e sintoma de producao, nao erro de programacao: quem
        // chama precisa conseguir registrar a falha e seguir.
        Assert.False(enviada);
    }

    [Fact]
    public async Task devolve_false_sem_lancar_quando_o_node_responde_erro()
    {
        var manipulador = new Testes.ManipuladorHttpSimulado(
            new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                Content = new StringContent("erro interno")
            });

        var servico = CriarServico(manipulador);

        var enviada = await servico.EnviarMensagemAsync("5511999999999", "Ola", CancellationToken.None);

        Assert.False(enviada);
    }

    [Fact]
    public async Task nao_chama_o_node_quando_o_segredo_compartilhado_nao_esta_configurado()
    {
        var chamado = false;
        var manipulador = new Testes.ManipuladorHttpSimulado(_ =>
        {
            chamado = true;
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        var servico = CriarServico(manipulador, segredo: "   ");

        var enviada = await servico.EnviarMensagemAsync("5511999999999", "Ola", CancellationToken.None);

        Assert.False(enviada);
        Assert.False(chamado);
    }

    private sealed class ManipuladorQueFalha : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            throw new HttpRequestException("Conexao recusada em 127.0.0.1:3001");
    }
}
