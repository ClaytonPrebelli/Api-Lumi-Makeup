using LumiMakeup.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace LumiMakeup.Infrastructure.Persistence;

public class LumiDbContext : DbContext
{
    public LumiDbContext(DbContextOptions<LumiDbContext> options) : base(options)
    {
    }

    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<Endereco> Enderecos => Set<Endereco>();
    public DbSet<Categoria> Categorias => Set<Categoria>();
    public DbSet<Produto> Produtos => Set<Produto>();
    public DbSet<ImagemProduto> ImagensProduto => Set<ImagemProduto>();
    public DbSet<Banner> Banners => Set<Banner>();
    public DbSet<ConfiguracaoFrete> ConfiguracoesDeFrete => Set<ConfiguracaoFrete>();
    public DbSet<ConfiguracaoWhatsApp> ConfiguracoesDeWhatsApp => Set<ConfiguracaoWhatsApp>();
    public DbSet<Pedido> Pedidos => Set<Pedido>();
    public DbSet<ItemPedido> ItensPedido => Set<ItemPedido>();
    public DbSet<Cupom> Cupons => Set<Cupom>();
    public DbSet<Despesa> Despesas => Set<Despesa>();
    public DbSet<NotaFiscal> NotasFiscais => Set<NotaFiscal>();
    public DbSet<RegistroWhatsApp> RegistrosWhatsApp => Set<RegistroWhatsApp>();
    public DbSet<SessaoWhatsApp> SessoesWhatsApp => Set<SessaoWhatsApp>();
    public DbSet<MovimentoEstoque> MovimentosEstoque => Set<MovimentoEstoque>();
    public DbSet<VarianteProduto> VariantesProduto => Set<VarianteProduto>();
    public DbSet<RecuperacaoDeSenha> RecuperacoesDeSenha => Set<RecuperacaoDeSenha>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(LumiDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}