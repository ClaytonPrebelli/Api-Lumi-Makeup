using System.Reflection;
using LumiMakeup.Api.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;

namespace LumiMakeup.Tests.Api;

/// <summary>
/// Trava a restrição de verbos do servidor de produção.
///
/// O IIS de produção só encaminha GET, POST, HEAD, OPTIONS e TRACE. Um PUT ou
/// DELETE é recusado **antes de chegar na API**, com 405 e sem nenhum header de
/// CORS - o que o navegador reporta como "bloqueado pela política de CORS",
/// uma mensagem que aponta para o lado errado e esconde a causa.
///
/// Sem este teste, reintroduzir um DELETE parece apenas uma melhoria de estilo
/// REST, e a quebra só aparece em produção, no painel, como erro de CORS.
/// </summary>
public sealed class VerboHttpDasRotasTests
{
    private static readonly string[] VerbosBloqueados = { "Delete", "Put", "Patch" };

    public static TheoryData<Type> ControllersDoPainel => new()
    {
        typeof(AdminProdutosController),
        typeof(AdminCategoriasController),
        typeof(AdminBannersController),
        typeof(AdminCuponsController),
        typeof(AdminPedidosController)
    };

    [Theory]
    [MemberData(nameof(ControllersDoPainel))]
    public void Controller_do_painel_nao_declara_verbs_bloqueados(Type controller)
    {
        var infragores = controller
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .SelectMany(metodo => metodo
                .GetCustomAttributes<HttpMethodAttribute>()
                .SelectMany(attr => attr.HttpMethods)
                .Select(verbo => (Metodo: metodo.Name, Verbo: verbo.ToUpperInvariant())))
            .Where(x => VerbosBloqueados.Contains(x.Verbo))
            .ToList();

        Assert.True(
            infragores.Count == 0,
            $"'{controller.Name}' declara verbo bloqueado pelo servidor: "
            + string.Join(", ", infragores.Select(x => $"{x.Verbo} {x.Metodo}")));
    }

    [Fact]
    public void Toda_operacao_que_altera_ou_apaga_e_post()
    {
        // Atualizar, excluir e reordenar precisam existir como POST com o verbo no
        // fim da URL. Este teste existe para o próximo que for mexer aqui ler antes
        // de "corrigir" para DELETE.
        var esperados = new (Type Controller, string Metodo, string Rota)[]
        {
            (typeof(AdminProdutosController), "Atualizar", "api/admin/produtos/{id:long}/atualizar"),
            (typeof(AdminProdutosController), "Excluir", "api/admin/produtos/{id:long}/excluir"),
            (typeof(AdminProdutosController), "ExcluirImagem", "api/admin/produtos/{id:long}/imagens/{imagemId:long}/excluir"),
            (typeof(AdminProdutosController), "ReordenarImagens", "api/admin/produtos/{id:long}/imagens/ordem"),
            (typeof(AdminCategoriasController), "Atualizar", "api/admin/categorias/{id:long}/atualizar"),
            (typeof(AdminCategoriasController), "Excluir", "api/admin/categorias/{id:long}/excluir"),
            (typeof(AdminBannersController), "Atualizar", "api/admin/banners/{id:long}/atualizar"),
            (typeof(AdminBannersController), "DefinirAtivo", "api/admin/banners/{id:long}/ativo"),
            (typeof(AdminBannersController), "Excluir", "api/admin/banners/{id:long}/excluir"),
            (typeof(AdminBannersController), "Reordenar", "api/admin/banners/ordem"),
            (typeof(AdminCuponsController), "Atualizar", "api/admin/cupons/{id:long}/atualizar"),
            (typeof(AdminCuponsController), "DefinirAtivo", "api/admin/cupons/{id:long}/ativo"),
            (typeof(AdminCuponsController), "SomarQuantidade", "api/admin/cupons/{id:long}/quantidade"),
            (typeof(AdminCuponsController), "Excluir", "api/admin/cupons/{id:long}/excluir"),
            (typeof(AdminPedidosController), "CriarVendaDeBalcao", "api/admin/pedidos/balcao"),
            (typeof(AdminPedidosController), "RegistrarPagamento", "api/admin/pedidos/{id:long}/pagamento"),
            (typeof(AdminPedidosController), "Cancelar", "api/admin/pedidos/{id:long}/cancelar")
        };

        foreach (var (tipoController, nomeMetodo, template) in esperados)
        {
            var metodo = tipoController.GetMethod(nomeMetodo);

            Assert.NotNull(metodo);

            // O Template do atributo traz so a parte relativa; a rota completa e o
            // prefixo do controller mais ele. Compor os dois valida a URL inteira.
            var prefixo = tipoController
                .GetCustomAttributes<RouteAttribute>()
                .Single()
                .Template;

            var atributo = metodo!
                .GetCustomAttributes<HttpMethodAttribute>()
                .Single();

            Assert.Equal("POST", atributo.HttpMethods.Single());
            Assert.Equal(template, $"{prefixo}/{atributo.Template}");
        }
    }
}
