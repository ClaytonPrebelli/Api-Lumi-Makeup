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

    [Fact]
    public async Task status_devolve_o_numero_quando_o_node_esta_pareado()
    {
        var corpo = """
            {
              "pareado": true,
              "numero": "5511999999999",
              "nome": "Lumi Makeup",
              "inicioEm": "2026-09-29T16:00:00.000Z",
              "ultimoEnvioEm": "2026-09-29T17:30:00.000Z"
            }
            """;

        var servico = CriarServico(new Testes.ManipuladorHttpSimulado(
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(corpo) }));

        var status = await servico.ObterStatusAsync(CancellationToken.None);

        Assert.True(status.ServicoNoAr);
        Assert.True(status.Pareado);
        Assert.Equal("5511999999999", status.Numero);
        Assert.Equal("Lumi Makeup", status.Nome);
        Assert.NotNull(status.ConectadoDesde);
    }

    [Fact]
    public async Task status_diz_que_o_servico_esta_parado_quando_o_node_nao_responde()
    {
        var servico = CriarServico(new ManipuladorQueFalha());

        var status = await servico.ObterStatusAsync(CancellationToken.None);

        // Node parado nao pode virar excecao: a tela do painel precisa mostrar
        // "servico desligado" e nao quebrar.
        Assert.False(status.ServicoNoAr);
        Assert.False(status.Pareado);
        Assert.Null(status.Numero);
    }

    [Fact]
    public async Task status_aceita_data_invalida_sem_derrubar_a_tela()
    {
        // O Node manda data quebrada, a tela de status continua funcionando.
        // A data de "conectado desde" nao vale um 500.
        var corpo = """{"pareado": true, "numero": "5511999999999", "inicioEm": "data-quebrada"}""";

        var servico = CriarServico(new Testes.ManipuladorHttpSimulado(
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(corpo) }));

        var status = await servico.ObterStatusAsync(CancellationToken.None);

        Assert.True(status.Pareado);
        Assert.Null(status.ConectadoDesde);
    }

    [Fact]
    public async Task status_aceita_corpo_que_nao_e_json_sem_derrubar_a_tela()
    {
        var servico = CriarServico(new Testes.ManipuladorHttpSimulado(
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("<html>erro do proxy</html>") }));

        var status = await servico.ObterStatusAsync(CancellationToken.None);

        Assert.True(status.ServicoNoAr);
        Assert.False(status.Pareado);
    }

    [Fact]
    public async Task pareamento_devolve_o_qr_em_png_base64()
    {
        var corpo = """{"pareado": false, "qr": "data:image/png;base64,iVBORw0KGgo="}""";

        var servico = CriarServico(new Testes.ManipuladorHttpSimulado(
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(corpo) }));

        var qr = await servico.ObterQrDePareamentoAsync(CancellationToken.None);

        Assert.Equal("data:image/png;base64,iVBORw0KGgo=", qr);
    }

    [Fact]
    public async Task pareamento_devolve_nulo_quando_o_node_diz_que_nao_ha_qr()
    {
        // 409 e o Node avisando "ou ja esta pareado, ou o numero foi bloqueado".
        var servico = CriarServico(new Testes.ManipuladorHttpSimulado(
            new HttpResponseMessage(HttpStatusCode.Conflict)
            {
                Content = new StringContent("{\"mensagem\":\"Nenhum QR disponivel\"}")
            }));

        var qr = await servico.ObterQrDePareamentoAsync(CancellationToken.None);

        Assert.Null(qr);
    }

    [Fact]
    public async Task pareamento_devolve_nulo_quando_o_node_esta_fora_do_ar()
    {
        var servico = CriarServico(new ManipuladorQueFalha());

        var qr = await servico.ObterQrDePareamentoAsync(CancellationToken.None);

        Assert.Null(qr);
    }

    [Fact]
    public async Task as_rotas_de_leitura_tambem_exigem_o_segredo()
    {
        HttpRequestMessage? recebida = null;

        var manipulador = new Testes.ManipuladorHttpSimulado(requisicao =>
        {
            recebida = requisicao;

            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}") };
        });

        var servico = CriarServico(manipulador, segredo: "");

        await servico.ObterStatusAsync(CancellationToken.None);
        await servico.ObterQrDePareamentoAsync(CancellationToken.None);

        // Sem segredo configurado, nenhuma leitura sai. E o que impede que o QR
        // do numero da loja saia do servidor.
        Assert.Null(recebida);
    }
}
