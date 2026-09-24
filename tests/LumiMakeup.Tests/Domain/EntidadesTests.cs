using LumiMakeup.Domain.Entities;
using LumiMakeup.Domain.Enums;

namespace LumiMakeup.Tests.Domain;

public class EntidadesTests
{
    [Fact]
    public void Categoria_define_propriedades_e_relacao_com_produtos()
    {
        var categoria = new Categoria
        {
            Id = 1,
            Nome = "Bases",
            Slug = "bases",
            Descricao = "Bases líquidas",
            Ativo = true,
            Produtos = { new Produto() }
        };

        Assert.Equal(1, categoria.Id);
        Assert.Equal("Bases", categoria.Nome);
        Assert.Equal("bases", categoria.Slug);
        Assert.Equal("Bases líquidas", categoria.Descricao);
        Assert.True(categoria.Ativo);
        Assert.Single(categoria.Produtos);
    }

    [Fact]
    public void ConfiguracaoFrete_define_propriedades()
    {
        var configuracao = new ConfiguracaoFrete
        {
            Id = 2,
            CepOrigem = "01310100",
            LatitudeOrigem = -23.55m,
            LongitudeOrigem = -46.63m,
            PrecoPorKm = 1.5m,
            TaxaMinima = 10m
        };

        Assert.Equal(2, configuracao.Id);
        Assert.Equal("01310100", configuracao.CepOrigem);
        Assert.Equal(-23.55m, configuracao.LatitudeOrigem);
        Assert.Equal(-46.63m, configuracao.LongitudeOrigem);
        Assert.Equal(1.5m, configuracao.PrecoPorKm);
        Assert.Equal(10m, configuracao.TaxaMinima);
    }

    [Fact]
    public void Despesa_define_propriedades_e_usuario()
    {
        var despesa = new Despesa
        {
            Id = 3,
            Descricao = "Energia elétrica",
            Categoria = "Contas",
            Valor = 250m,
            DataDaDespesa = new DateTime(2026, 1, 5),
            CriadoPor = 10,
            CriadoEm = new DateTime(2026, 1, 5, 8, 0, 0, DateTimeKind.Utc),
            CriadoPorUsuario = new Usuario()
        };

        Assert.Equal(3, despesa.Id);
        Assert.Equal("Energia elétrica", despesa.Descricao);
        Assert.Equal("Contas", despesa.Categoria);
        Assert.Equal(250m, despesa.Valor);
        Assert.Equal(new DateTime(2026, 1, 5), despesa.DataDaDespesa);
        Assert.Equal(10, despesa.CriadoPor);
        Assert.Equal(new DateTime(2026, 1, 5, 8, 0, 0, DateTimeKind.Utc), despesa.CriadoEm);
        Assert.NotNull(despesa.CriadoPorUsuario);
    }

    [Fact]
    public void Despesa_define_valores_padrao()
    {
        var despesa = new Despesa();

        Assert.Empty(despesa.Descricao);
        Assert.Empty(despesa.Categoria);
        Assert.Equal(0m, despesa.Valor);
        Assert.NotEqual(default, despesa.DataDaDespesa);
        Assert.NotEqual(default, despesa.CriadoEm);
    }

    [Fact]
    public void Endereco_define_propriedades_e_usuario()
    {
        var endereco = new Endereco
        {
            Id = 4,
            UsuarioId = 5,
            Cep = "01310100",
            Logradouro = "Av. Paulista",
            Numero = "1000",
            Complemento = "Apto 12",
            Bairro = "Bela Vista",
            Cidade = "São Paulo",
            Estado = "SP",
            Latitude = -23.56m,
            Longitude = -46.64m,
            Padrao = true,
            Usuario = new Usuario()
        };

        Assert.Equal(4, endereco.Id);
        Assert.Equal(5, endereco.UsuarioId);
        Assert.Equal("01310100", endereco.Cep);
        Assert.Equal("Av. Paulista", endereco.Logradouro);
        Assert.Equal("1000", endereco.Numero);
        Assert.Equal("Apto 12", endereco.Complemento);
        Assert.Equal("Bela Vista", endereco.Bairro);
        Assert.Equal("São Paulo", endereco.Cidade);
        Assert.Equal("SP", endereco.Estado);
        Assert.Equal(-23.56m, endereco.Latitude);
        Assert.Equal(-46.64m, endereco.Longitude);
        Assert.True(endereco.Padrao);
        Assert.NotNull(endereco.Usuario);
    }

    [Fact]
    public void ImagemProduto_define_propriedades_e_produto()
    {
        var imagem = new ImagemProduto
        {
            Id = 6,
            ProdutoId = 7,
            UrlImagem = "https://cdn.example.com/foto.jpg",
            Ordem = 1,
            Produto = new Produto()
        };

        Assert.Equal(6, imagem.Id);
        Assert.Equal(7, imagem.ProdutoId);
        Assert.Equal("https://cdn.example.com/foto.jpg", imagem.UrlImagem);
        Assert.Equal(1, imagem.Ordem);
        Assert.NotNull(imagem.Produto);
    }

    [Fact]
    public void ItemPedido_define_propriedades_e_relacoes()
    {
        var item = new ItemPedido
        {
            Id = 8,
            PedidoId = 9,
            ProdutoId = 10,
            NomeProdutoRegistrado = "Batom Mate",
            PrecoCustoUnitario = 12m,
            PrecoVendaUnitario = 35m,
            Quantidade = 2,
            Subtotal = 70m,
            Pedido = new Pedido(),
            Produto = new Produto()
        };

        Assert.Equal(8, item.Id);
        Assert.Equal(9, item.PedidoId);
        Assert.Equal(10, item.ProdutoId);
        Assert.Equal("Batom Mate", item.NomeProdutoRegistrado);
        Assert.Equal(12m, item.PrecoCustoUnitario);
        Assert.Equal(35m, item.PrecoVendaUnitario);
        Assert.Equal(2, item.Quantidade);
        Assert.Equal(70m, item.Subtotal);
        Assert.NotNull(item.Pedido);
        Assert.NotNull(item.Produto);
    }

    [Fact]
    public void NotaFiscal_define_propriedades_e_pedido()
    {
        var nota = new NotaFiscal
        {
            Id = 11,
            PedidoId = 9,
            Status = StatusNotaFiscal.Emitida,
            ReferenciaFocusNfe = "ref-123",
            UrlXml = "https://cdn.example.com/nf.xml",
            UrlPdf = "https://cdn.example.com/nf.pdf",
            EmitidaEm = new DateTime(2026, 1, 6),
            Pedido = new Pedido()
        };

        Assert.Equal(11, nota.Id);
        Assert.Equal(9, nota.PedidoId);
        Assert.Equal(StatusNotaFiscal.Emitida, nota.Status);
        Assert.Equal("ref-123", nota.ReferenciaFocusNfe);
        Assert.Equal("https://cdn.example.com/nf.xml", nota.UrlXml);
        Assert.Equal("https://cdn.example.com/nf.pdf", nota.UrlPdf);
        Assert.Equal(new DateTime(2026, 1, 6), nota.EmitidaEm);
        Assert.NotNull(nota.Pedido);
    }

    [Fact]
    public void NotaFiscal_define_status_padrao_pendente()
    {
        var nota = new NotaFiscal();
        Assert.Equal(StatusNotaFiscal.Pendente, nota.Status);
    }

    [Fact]
    public void Pedido_define_propriedades_e_relacoes()
    {
        var pedido = new Pedido
        {
            Id = 12,
            UsuarioId = 5,
            EnderecoEntregaId = 4,
            Status = StatusPedido.Pago,
            StatusEntrega = StatusEntrega.Enviado,
            MetodoPagamento = MetodoPagamento.Pix,
            DistanciaKm = 3.5m,
            CustoFrete = 15m,
            Subtotal = 200m,
            Total = 215m,
            Observacoes = "Entregar após 18h",
            CriadoEm = new DateTime(2026, 1, 6, 9, 0, 0, DateTimeKind.Utc),
            PagoEm = new DateTime(2026, 1, 6, 9, 5, 0, DateTimeKind.Utc),
            EntregueEm = null,
            Usuario = new Usuario(),
            EnderecoEntrega = new Endereco()
        };

        pedido.Itens.Add(new ItemPedido());
        pedido.NotasFiscais.Add(new NotaFiscal());
        pedido.RegistrosWhatsApp.Add(new RegistroWhatsApp());

        Assert.Equal(12, pedido.Id);
        Assert.Equal(5, pedido.UsuarioId);
        Assert.Equal(4, pedido.EnderecoEntregaId);
        Assert.Equal(StatusPedido.Pago, pedido.Status);
        Assert.Equal(StatusEntrega.Enviado, pedido.StatusEntrega);
        Assert.Equal(MetodoPagamento.Pix, pedido.MetodoPagamento);
        Assert.Equal(3.5m, pedido.DistanciaKm);
        Assert.Equal(15m, pedido.CustoFrete);
        Assert.Equal(200m, pedido.Subtotal);
        Assert.Equal(215m, pedido.Total);
        Assert.Equal("Entregar após 18h", pedido.Observacoes);
        Assert.Equal(new DateTime(2026, 1, 6, 9, 0, 0, DateTimeKind.Utc), pedido.CriadoEm);
        Assert.Equal(new DateTime(2026, 1, 6, 9, 5, 0, DateTimeKind.Utc), pedido.PagoEm);
        Assert.Null(pedido.EntregueEm);
        Assert.NotNull(pedido.Usuario);
        Assert.NotNull(pedido.EnderecoEntrega);
        Assert.Single(pedido.Itens);
        Assert.Single(pedido.NotasFiscais);
        Assert.Single(pedido.RegistrosWhatsApp);
    }

    [Fact]
    public void Pedido_define_valores_padrao()
    {
        var pedido = new Pedido();

        Assert.Equal(StatusPedido.AguardandoPagamento, pedido.Status);
        Assert.Equal(StatusEntrega.NaoEnviado, pedido.StatusEntrega);
        Assert.Null(pedido.MetodoPagamento);
        Assert.Equal(0m, pedido.DistanciaKm);
        Assert.Equal(0m, pedido.CustoFrete);
        Assert.Equal(0m, pedido.Subtotal);
        Assert.Equal(0m, pedido.Total);
        Assert.NotEqual(default, pedido.CriadoEm);
    }

    [Fact]
    public void Produto_define_propriedades_e_relacoes()
    {
        var produto = new Produto
        {
            Id = 13,
            CategoriaId = 1,
            Nome = "Batom",
            Slug = "batom",
            Descricao = "Batom vermelho",
            PrecoCusto = 15m,
            PrecoVenda = 39.9m,
            QuantidadeEstoque = 8,
            Ativo = false,
            CriadoEm = new DateTime(2026, 1, 6, 10, 0, 0, DateTimeKind.Utc),
            Categoria = new Categoria()
        };

        produto.Imagens.Add(new ImagemProduto());
        produto.ItensPedido.Add(new ItemPedido());

        Assert.Equal(13, produto.Id);
        Assert.Equal(1, produto.CategoriaId);
        Assert.Equal("Batom", produto.Nome);
        Assert.Equal("batom", produto.Slug);
        Assert.Equal("Batom vermelho", produto.Descricao);
        Assert.Equal(15m, produto.PrecoCusto);
        Assert.Equal(39.9m, produto.PrecoVenda);
        Assert.Equal(8, produto.QuantidadeEstoque);
        Assert.False(produto.Ativo);
        Assert.Equal(new DateTime(2026, 1, 6, 10, 0, 0, DateTimeKind.Utc), produto.CriadoEm);
        Assert.NotNull(produto.Categoria);
        Assert.Single(produto.Imagens);
        Assert.Single(produto.ItensPedido);
    }

    [Fact]
    public void Produto_define_ativo_padrao_verdadeiro()
    {
        var produto = new Produto();
        Assert.True(produto.Ativo);
    }

    [Fact]
    public void RegistroWhatsApp_define_propriedades_e_pedido()
    {
        var registro = new RegistroWhatsApp
        {
            Id = 14,
            PedidoId = 9,
            Telefone = "5511999999999",
            Mensagem = "Pedido confirmado",
            Status = StatusRegistroWhatsApp.Enviado,
            EnviadoEm = new DateTime(2026, 1, 6, 11, 0, 0, DateTimeKind.Utc),
            Pedido = new Pedido()
        };

        Assert.Equal(14, registro.Id);
        Assert.Equal(9, registro.PedidoId);
        Assert.Equal("5511999999999", registro.Telefone);
        Assert.Equal("Pedido confirmado", registro.Mensagem);
        Assert.Equal(StatusRegistroWhatsApp.Enviado, registro.Status);
        Assert.Equal(new DateTime(2026, 1, 6, 11, 0, 0, DateTimeKind.Utc), registro.EnviadoEm);
        Assert.NotNull(registro.Pedido);
    }

    [Fact]
    public void RegistroWhatsApp_define_valores_padrao()
    {
        var registro = new RegistroWhatsApp();
        Assert.Empty(registro.Telefone);
        Assert.Empty(registro.Mensagem);
        Assert.NotEqual(default, registro.EnviadoEm);
    }

    [Fact]
    public void Usuario_define_propriedades_e_relacoes()
    {
        var usuario = new Usuario
        {
            Id = 15,
            Nome = "Maria",
            Email = "maria@exemplo.com",
            HashSenha = "hash",
            IdGoogle = "google-123",
            Cpf = "12345678901",
            Telefone = "11999999999",
            Papel = PapelUsuario.Administrador,
            CriadoEm = new DateTime(2026, 1, 6, 12, 0, 0, DateTimeKind.Utc)
        };

        usuario.Enderecos.Add(new Endereco());
        usuario.Pedidos.Add(new Pedido());
        usuario.Despesas.Add(new Despesa());

        Assert.Equal(15, usuario.Id);
        Assert.Equal("Maria", usuario.Nome);
        Assert.Equal("maria@exemplo.com", usuario.Email);
        Assert.Equal("hash", usuario.HashSenha);
        Assert.Equal("google-123", usuario.IdGoogle);
        Assert.Equal("12345678901", usuario.Cpf);
        Assert.Equal("11999999999", usuario.Telefone);
        Assert.Equal(PapelUsuario.Administrador, usuario.Papel);
        Assert.Equal(new DateTime(2026, 1, 6, 12, 0, 0, DateTimeKind.Utc), usuario.CriadoEm);
        Assert.Single(usuario.Enderecos);
        Assert.Single(usuario.Pedidos);
        Assert.Single(usuario.Despesas);
    }

    [Fact]
    public void Usuario_define_valores_padrao()
    {
        var usuario = new Usuario();

        Assert.Empty(usuario.Nome);
        Assert.Empty(usuario.Email);
        Assert.Null(usuario.HashSenha);
        Assert.Null(usuario.IdGoogle);
        Assert.Null(usuario.Cpf);
        Assert.Null(usuario.Telefone);
        Assert.Equal(PapelUsuario.Cliente, usuario.Papel);
        Assert.NotEqual(default, usuario.CriadoEm);
        Assert.Empty(usuario.Enderecos);
        Assert.Empty(usuario.Pedidos);
        Assert.Empty(usuario.Despesas);
    }
}