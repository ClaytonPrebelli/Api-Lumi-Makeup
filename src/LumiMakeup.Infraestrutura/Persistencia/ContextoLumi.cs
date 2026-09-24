using LumiMakeup.Dominio.Entidades;
using Microsoft.EntityFrameworkCore;

namespace LumiMakeup.Infraestrutura.Persistencia;

public class ContextoLumi : DbContext
{
    public ContextoLumi(DbContextOptions<ContextoLumi> options) : base(options)
    {
    }

    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<Endereco> Enderecos => Set<Endereco>();
    public DbSet<Categoria> Categorias => Set<Categoria>();
    public DbSet<Produto> Produtos => Set<Produto>();
    public DbSet<ImagemProduto> ImagensProduto => Set<ImagemProduto>();
    public DbSet<ConfiguracaoFrete> ConfiguracoesDeFrete => Set<ConfiguracaoFrete>();
    public DbSet<Pedido> Pedidos => Set<Pedido>();
    public DbSet<ItemPedido> ItensPedido => Set<ItemPedido>();
    public DbSet<Despesa> Despesas => Set<Despesa>();
    public DbSet<NotaFiscal> NotasFiscais => Set<NotaFiscal>();
    public DbSet<RegistroWhatsApp> RegistrosWhatsApp => Set<RegistroWhatsApp>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ContextoLumi).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}