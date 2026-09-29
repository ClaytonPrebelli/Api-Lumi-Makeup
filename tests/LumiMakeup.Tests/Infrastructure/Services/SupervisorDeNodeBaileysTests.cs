using LumiMakeup.Infrastructure.Services;

namespace LumiMakeup.Tests.Infrastructure.Services;

/// <summary>
/// Testes do comando que sobe o Node.
///
/// Os caminhos sao montados com <see cref="Path"/>, e nao escritos a mao, porque
/// esta suite roda no CI em Linux e na sua maquina em Windows. Um caminho com
/// letra de drive escrito a mao passa em Windows e quebra no Linux, onde a
/// barra invertida nao e separador e a letra de drive nao e raiz.
/// </summary>
public class SupervisorDeNodeBaileysTests
{
    private static string Pasta(string nome) => Path.Combine(Extrator, "www", nome);

    private static string Extrator => Path.Combine(Path.GetTempPath(), "lumi-teste-node");

    [Fact]
    public void monta_o_comando_para_o_node_portatil()
    {
        var pasta = Pasta("whats");
        var executavel = Path.Combine(pasta, "node.exe");
        var entrada = Path.Combine(pasta, "src", "index.js");

        var informacoes = SupervisorDeNodeBaileys.MontarInformacoesDoProcesso(
            executavel,
            entrada,
            pasta,
            3001,
            "segredo");

        // O executavel vem da configuracao, e nao do PATH: em producao o Node
        // e uma copia portatil na pasta irma, e nao ha Node instalado.
        Assert.Equal(executavel, informacoes.FileName);
        Assert.Equal(pasta, informacoes.WorkingDirectory);
        Assert.Equal([entrada], informacoes.ArgumentList);

        // Sem janela e sem shell: o processo roda sob o pool do IIS, e nao pode
        // abrir janela nem passar pelo shell do Windows.
        Assert.False(informacoes.UseShellExecute);
        Assert.True(informacoes.CreateNoWindow);
    }

    [Fact]
    public void passa_o_segredo_por_variavel_de_ambiente_e_nao_por_argumento()
    {
        var pasta = Pasta("whats");

        var informacoes = SupervisorDeNodeBaileys.MontarInformacoesDoProcesso(
            Path.Combine(pasta, "node.exe"),
            Path.Combine(pasta, "src", "index.js"),
            pasta,
            3001,
            "segredo-super-secreto");

        // Argumento de linha de comando aparece na lista de processos do
        // Windows, visivel para qualquer usuario da maquina. Ambiente nao.
        Assert.DoesNotContain("segredo-super-secreto", informacoes.ArgumentList);
        Assert.Equal("segredo-super-secreto", informacoes.Environment["BAILEYS_SEGREDO_COMPARTILHADO"]);

        Assert.Equal("3001", informacoes.Environment["PORT"]);
        Assert.Equal("production", informacoes.Environment["NODE_ENV"]);
        Assert.Equal(Path.Combine(pasta, "dados"), informacoes.Environment["PASTA_DE_DADOS"]);
    }

    [Fact]
    public void passa_o_env_file_antes_do_script_quando_o_arquivo_existe()
    {
        var pasta = Pasta("whats");
        var entrada = Path.Combine(pasta, "src", "index.js");

        var informacoes = SupervisorDeNodeBaileys.MontarInformacoesDoProcesso(
            Path.Combine(pasta, "node.exe"),
            entrada,
            pasta,
            3001,
            "segredo",
            usarArquivoEnv: true);

        // A ordem importa: o Node so aplica o --env-file quando ele vem antes do
        // script. Depois do script, ele trata como argumento, o .env nao e lido,
        // e o processo sobe sem segredo e morre na largada.
        Assert.Equal(["--env-file=.env", entrada], informacoes.ArgumentList);
    }

    [Fact]
    public void nao_passa_o_env_file_quando_ele_nao_foi_pedido()
    {
        var pasta = Pasta("whats");
        var entrada = Path.Combine(pasta, "src", "index.js");

        var informacoes = SupervisorDeNodeBaileys.MontarInformacoesDoProcesso(
            Path.Combine(pasta, "node.exe"),
            entrada,
            pasta,
            3001,
            "segredo");

        // Em desenvolvimento o .env nao existe e o Node roda na mao. Sem a flag,
        // o processo sobe so com o ambiente, e nao falha por causa de um
        // arquivo que ninguem pediu.
        Assert.Equal([entrada], informacoes.ArgumentList);
    }

    [Fact]
    public void a_pasta_de_dados_fica_dentro_da_pasta_do_node()
    {
        var pasta = Pasta("whats");

        var informacoes = SupervisorDeNodeBaileys.MontarInformacoesDoProcesso(
            Path.Combine(pasta, "node.exe"),
            Path.Combine(pasta, "src", "index.js"),
            pasta,
            3001,
            "segredo");

        // A sessao do Baileys tem de sobreviver ao recycle do pool do IIS. Se
        // fosse /tmp ou a pasta temporaria do usuario, cada reinicio exigiria
        // escanear QR de novo.
        var pastaDeDados = informacoes.Environment["PASTA_DE_DADOS"];

        // OrdinalIgnoreCase explicito: sem isso a comparacao herda a regra da
        // cultura e o teste passa em Windows e falha no CI em Linux.
        Assert.StartsWith(pasta, pastaDeDados!, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("dados", Path.GetFileName(pastaDeDados!));
    }

    [Fact]
    public void caminho_com_letra_de_drive_nao_e_confundido_com_raiz_da_publicacao()
    {
        // Este teste e a razao de os caminhos aqui serem montados com Path, e
        // nao escritos a mao.
        //
        // No CI, em Linux, um caminho com letra de drive nao conta como
        // absoluto: no POSIX so a barra abre caminho absoluto. O codigo tratava
        // a pasta do Node como relativa e a combinava com a raiz da
        // publicacao, devolvendo a pasta do runner em vez da pedida. O teste
        // passava no Windows e quebrava no deploy.
        //
        // Aqui a comparacao e feita com Path, que e a mesma regra do codigo
        // de producao: o mesmo caminho absoluto passa intacto nos dois sistemas.
        var raizDaApi = Pasta("api.lumimakeup.com.br");
        var caminhoDoNode = Path.Combine(Extrator, "www", "whats.lumimakeup.com.br");

        Assert.True(Path.IsPathRooted(caminhoDoNode));
        Assert.Equal(
            caminhoDoNode,
            SupervisorDeNodeBaileys.ResolverPastaDoNode(caminhoDoNode, raizDaApi));
    }

    [Fact]
    public void resolve_a_pasta_irma_a_partir_da_raiz_da_publicacao()
    {
        var raiz = Pasta("api.lumimakeup.com.br");

        var pasta = SupervisorDeNodeBaileys.ResolverPastaDoNode("../whats.lumimakeup.com.br", raiz);

        // A publicacao e plana e o Node e pasta irma. E o mesmo formato de
        // caminho relativo que o ArmazenamentoDeImagens ja usa ("../imagens").
        Assert.Equal(Path.Combine(Extrator, "www", "whats.lumimakeup.com.br"), pasta);
    }

    [Fact]
    public void aceita_caminho_absoluto_sem_misturar_com_a_raiz_da_publicacao()
    {
        // Caminho absoluto montado no formato do sistema operacional. Raiz
        // diferente da da API e a prova de que os dois nao se misturam: se o
        // metodo juntasse os dois, o resultado comecaria pela pasta da API.
        var absoluto = Path.Combine(Extrator, "outro-servidor", "whats");
        var raizDaApi = Pasta("api.lumimakeup.com.br");

        var pasta = SupervisorDeNodeBaileys.ResolverPastaDoNode(absoluto, raizDaApi);

        Assert.Equal(absoluto, pasta);
        Assert.DoesNotContain("api.lumimakeup.com.br", pasta);
    }

    [Fact]
    public void devolve_vazio_quando_a_pasta_do_node_nao_esta_configurada()
    {
        // Vazio em vez de excecao: quem chama comeca a API mesmo sem WhatsApp,
        // e registra a configuracao faltando.
        var pasta = SupervisorDeNodeBaileys.ResolverPastaDoNode("", Pasta("api.lumimakeup.com.br"));

        Assert.Equal(string.Empty, pasta);
    }
}
