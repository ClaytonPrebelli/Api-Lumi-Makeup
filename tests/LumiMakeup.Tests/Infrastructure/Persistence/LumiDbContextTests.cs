using LumiMakeup.Domain.Entities;
using LumiMakeup.Infrastructure.Persistence.Migrations;
using LumiMakeup.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;

namespace LumiMakeup.Tests.Infrastructure.Persistence;

public class LumiDbContextTests
{
    [Fact]
    public void Expoe_todos_os_dbsets()
    {
        using var contexto = Testes.CriarContextoInMemory();

        Assert.NotNull(contexto.Usuarios);
        Assert.NotNull(contexto.Enderecos);
        Assert.NotNull(contexto.Categorias);
        Assert.NotNull(contexto.Produtos);
        Assert.NotNull(contexto.ImagensProduto);
        Assert.NotNull(contexto.ConfiguracoesDeFrete);
        Assert.NotNull(contexto.ConfiguracoesDeRecompra);
        Assert.NotNull(contexto.EnviosDeRecompra);
        Assert.NotNull(contexto.Pedidos);
        Assert.NotNull(contexto.ItensPedido);
        Assert.NotNull(contexto.Despesas);
        Assert.NotNull(contexto.NotasFiscais);
        Assert.NotNull(contexto.RegistrosWhatsApp);
    }

    [Fact]
    public void Modelo_reconhece_todas_as_entidades()
    {
        using var contexto = Testes.CriarContextoInMemory();

        var modelo = contexto.Model;

        foreach (var tipo in new[]
        {
            typeof(Usuario), typeof(Endereco), typeof(Categoria), typeof(Produto),
            typeof(ImagemProduto), typeof(ConfiguracaoFrete), typeof(Pedido),
            typeof(ItemPedido), typeof(Despesa), typeof(NotaFiscal), typeof(RegistroWhatsApp),
            typeof(ConfiguracaoDeRecompra), typeof(EnvioDeRecompra)
        })
        {
            Assert.NotNull(modelo.FindEntityType(tipo));
        }
    }

    [Fact]
    public void Migration_de_recompra_esta_registrada_sem_aplicar_schema()
    {
        using var contexto = Testes.CriarContextoInMemory();

        var migration = Assert.IsType<MigrationAttribute>(
            Attribute.GetCustomAttribute(typeof(RecompraManual), typeof(MigrationAttribute)));

        Assert.Equal("20261004230000_RecompraManual", migration.Id);
    }

    [Fact]
    public void Cria_o_banco_e_permite_inserir_e_consultar()
    {
        using var contexto = Testes.CriarContextoInMemory();

        contexto.Database.EnsureCreated();
        contexto.Usuarios.Add(new Usuario { Nome = "Maria", Email = "maria@exemplo.com" });
        contexto.SaveChanges();

        var criado = contexto.Usuarios.Single();

        Assert.Equal("maria@exemplo.com", criado.Email);
    }
}