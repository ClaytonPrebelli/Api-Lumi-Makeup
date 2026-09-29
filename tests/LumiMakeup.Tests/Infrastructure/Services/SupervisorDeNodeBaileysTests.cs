using LumiMakeup.Infrastructure.Services;
using LumiMakeup.Tests.Helpers;
using Microsoft.Extensions.Options;

namespace LumiMakeup.Tests.Infrastructure.Services;

public class SupervisorDeNodeBaileysTests
{
    [Fact]
    public void monta_o_comando_para_o_node_portatil()
    {
        var informacoes = SupervisorDeNodeBaileys.MontarInformacoesDoProcesso(
            @"C:\www\whats\node.exe",
            @"C:\www\whats\src\index.js",
            @"C:\www\whats",
            3001,
            "segredo");

        // O executavel vem da configuracao, e nao do PATH: em producao o Node
        // e uma copia portatil na pasta irma, e nao ha Node instalado.
        Assert.Equal(@"C:\www\whats\node.exe", informacoes.FileName);
        Assert.Equal(@"C:\www\whats", informacoes.WorkingDirectory);
        Assert.Equal([@"C:\www\whats\src\index.js"], informacoes.ArgumentList);

        // Sem janela e sem shell: o processo roda sob o pool do IIS, e nao pode
        // abrir janela nem passar pelo shell do Windows.
        Assert.False(informacoes.UseShellExecute);
        Assert.True(informacoes.CreateNoWindow);
    }

    [Fact]
    public void passa_o_segredo_por_variavel_de_ambiente_e_nao_por_argumento()
    {
        var informacoes = SupervisorDeNodeBaileys.MontarInformacoesDoProcesso(
            @"C:\www\whats\node.exe",
            @"C:\www\whats\src\index.js",
            @"C:\www\whats",
            3001,
            "segredo-super-secreto");

        // Argumento de linha de comando aparece na lista de processos do
        // Windows, visivel para qualquer usuario da maquina. Ambiente nao.
        Assert.DoesNotContain("segredo-super-secreto", informacoes.ArgumentList);
        Assert.Equal("segredo-super-secreto", informacoes.Environment["BAILEYS_SEGREDO_COMPARTILHADO"]);

        Assert.Equal("3001", informacoes.Environment["PORT"]);
        Assert.Equal("production", informacoes.Environment["NODE_ENV"]);
        Assert.Equal(@"C:\www\whats\dados", informacoes.Environment["PASTA_DE_DADOS"]);
    }

    [Fact]
    public void a_pasta_de_dados_fica_dentro_da_pasta_do_node()
    {
        var informacoes = SupervisorDeNodeBaileys.MontarInformacoesDoProcesso(
            @"C:\www\whats\node.exe",
            @"C:\www\whats\src\index.js",
            @"C:\www\whats",
            3001,
            "segredo");

        // A sessao do Baileys tem de sobreviver ao recycle do pool do IIS. Se
        // fosse /tmp ou a pasta temporaria do usuario, cada reinicio exigiria
        // escanear QR de novo.
        Assert.StartsWith(@"C:\www\whats", informacoes.Environment["PASTA_DE_DADOS"]!);
    }

    [Fact]
    public void resolve_a_pasta_irma_a_partir_da_raiz_da_publicacao()
    {
        var raiz = @"C:\inetpub\wwwroot\api.lumimakeup.com.br";

        var pasta = SupervisorDeNodeBaileys.ResolverPastaDoNode("../whats.lumimakeup.com.br", raiz);

        // A publicacao e plana e o Node e pasta irma. E o mesmo formato de
        // caminho relativo que o ArmazenamentoDeImagens ja usa ("../imagens").
        Assert.Equal(@"C:\inetpub\wwwroot\whats.lumimakeup.com.br", pasta);
    }

    [Fact]
    public void aceita_caminho_absoluto_sem_misturar_com_a_raiz_da_publicacao()
    {
        var pasta = SupervisorDeNodeBaileys.ResolverPastaDoNode(
            @"D:\servicos\whats",
            @"C:\inetpub\wwwroot\api.lumimakeup.com.br");

        Assert.Equal(@"D:\servicos\whats", pasta);
    }

    [Fact]
    public void devolve_vazio_quando_a_pasta_do_node_nao_esta_configurada()
    {
        // Vazio em vez de excecao: quem chama comeca a API mesmo sem WhatsApp,
        // e registra a configuracao faltando.
        var pasta = SupervisorDeNodeBaileys.ResolverPastaDoNode("", @"C:\inetpub\wwwroot\api");

        Assert.Equal(string.Empty, pasta);
    }
}
