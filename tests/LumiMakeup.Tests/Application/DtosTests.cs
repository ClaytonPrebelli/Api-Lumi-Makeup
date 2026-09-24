using LumiMakeup.Application.DTOs;
using LumiMakeup.Domain.Enums;

namespace LumiMakeup.Tests.Application;

public class DtosTests
{
    [Fact]
    public void RequisicaoDeRegistro_armazena_valores()
    {
        var requisicao = new RequisicaoDeRegistro("Maria", "maria@exemplo.com", "abc123", "token-rc");

        Assert.Equal("Maria", requisicao.Nome);
        Assert.Equal("maria@exemplo.com", requisicao.Email);
        Assert.Equal("abc123", requisicao.Senha);
        Assert.Equal("token-rc", requisicao.TokenRecaptcha);
    }

    [Fact]
    public void RequisicaoDeRegistro_aceita_token_recaptcha_nulo()
    {
        var requisicao = new RequisicaoDeRegistro("Maria", "maria@exemplo.com", "abc123", null);

        Assert.Null(requisicao.TokenRecaptcha);
    }

    [Fact]
    public void RequisicaoDeLogin_armazena_valores()
    {
        var requisicao = new RequisicaoDeLogin("maria@exemplo.com", "abc123");

        Assert.Equal("maria@exemplo.com", requisicao.Email);
        Assert.Equal("abc123", requisicao.Senha);
    }

    [Fact]
    public void RequisicaoDeLoginGoogle_armazena_token()
    {
        var requisicao = new RequisicaoDeLoginGoogle("token-do-google");

        Assert.Equal("token-do-google", requisicao.TokenId);
    }

    [Fact]
    public void RequisicaoDeRenovacao_armazena_token()
    {
        var requisicao = new RequisicaoDeRenovacao("refresh-token");

        Assert.Equal("refresh-token", requisicao.TokenRefresh);
    }

    [Fact]
    public void RequisicaoDeCompletarPerfil_armazena_valores_com_endereco()
    {
        var endereco = new RequisicaoDeEndereco("01310100", "1000", "Apto 12");
        var requisicao = new RequisicaoDeCompletarPerfil("Maria", "123.456.789-01", "11999999999", endereco);

        Assert.Equal("Maria", requisicao.Nome);
        Assert.Equal("123.456.789-01", requisicao.Cpf);
        Assert.Equal("11999999999", requisicao.Telefone);
        Assert.NotNull(requisicao.Endereco);
        Assert.Equal("01310100", requisicao.Endereco!.Cep);
        Assert.Equal("1000", requisicao.Endereco.Numero);
        Assert.Equal("Apto 12", requisicao.Endereco.Complemento);
    }

    [Fact]
    public void RequisicaoDeCompletarPerfil_aceita_endereco_e_telefone_nulos()
    {
        var requisicao = new RequisicaoDeCompletarPerfil("Maria", "12345678901", null, null);

        Assert.Equal("12345678901", requisicao.Cpf);
        Assert.Null(requisicao.Telefone);
        Assert.Null(requisicao.Endereco);
    }

    [Fact]
    public void UsuarioDto_armazena_valores()
    {
        var dto = new UsuarioDto(1, "Maria", "maria@exemplo.com", "12345678901", "11999999999", PapelUsuario.Cliente, false, true);

        Assert.Equal(1, dto.Id);
        Assert.Equal("Maria", dto.Nome);
        Assert.Equal("maria@exemplo.com", dto.Email);
        Assert.Equal("12345678901", dto.Cpf);
        Assert.Equal("11999999999", dto.Telefone);
        Assert.Equal(PapelUsuario.Cliente, dto.Papel);
        Assert.False(dto.PrecisaPerfil);
        Assert.True(dto.TemEndereco);
    }

    [Fact]
    public void RespostaDeAutenticacao_armazena_valores()
    {
        var usuario = new UsuarioDto(1, "Maria", "maria@exemplo.com", null, null, PapelUsuario.Administrador, true, false);
        var resposta = new RespostaDeAutenticacao("acesso", "refresh", usuario);

        Assert.Equal("acesso", resposta.TokenAcesso);
        Assert.Equal("refresh", resposta.TokenRefresh);
        Assert.Same(usuario, resposta.Usuario);
    }

    [Fact]
    public void CategoriaDto_armazena_valores()
    {
        var dto = new CategoriaDto(1, "Bases", "bases", "Bases líquidas", true);

        Assert.Equal(1, dto.Id);
        Assert.Equal("Bases", dto.Nome);
        Assert.Equal("bases", dto.Slug);
        Assert.Equal("Bases líquidas", dto.Descricao);
        Assert.True(dto.Ativo);
    }

    [Fact]
    public void ImagemProdutoDto_armazena_valores()
    {
        var dto = new ImagemProdutoDto(1, "https://cdn.example.com/foto.jpg", 2);

        Assert.Equal(1, dto.Id);
        Assert.Equal("https://cdn.example.com/foto.jpg", dto.UrlImagem);
        Assert.Equal(2, dto.Ordem);
    }

    [Fact]
    public void ProdutoDto_armazena_valores()
    {
        var imagens = new List<ImagemProdutoDto> { new(1, "https://cdn.example.com/foto.jpg", 1) };
        var dto = new ProdutoDto(1, "Batom", "batom", "Batom vermelho", 39.9m, 5, true, 1, "Bases", imagens);

        Assert.Equal(1, dto.Id);
        Assert.Equal("Batom", dto.Nome);
        Assert.Equal("batom", dto.Slug);
        Assert.Equal("Batom vermelho", dto.Descricao);
        Assert.Equal(39.9m, dto.PrecoVenda);
        Assert.Equal(5, dto.QuantidadeEstoque);
        Assert.True(dto.Ativo);
        Assert.Equal(1, dto.CategoriaId);
        Assert.Equal("Bases", dto.NomeCategoria);
        Assert.Single(dto.Imagens);
    }

    [Fact]
    public void PedidoDto_armazena_valores()
    {
        var itens = new List<ItemPedidoDto> { new(1, 2, "Batom", 35m, 2, 70m) };
        var dto = new PedidoDto(1, StatusPedido.Pago, StatusEntrega.Enviado, MetodoPagamento.Pix, 200m, 15m, 215m, new DateTime(2026, 1, 6), itens);

        Assert.Equal(1, dto.Id);
        Assert.Equal(StatusPedido.Pago, dto.Status);
        Assert.Equal(StatusEntrega.Enviado, dto.StatusEntrega);
        Assert.Equal(MetodoPagamento.Pix, dto.MetodoPagamento);
        Assert.Equal(200m, dto.Subtotal);
        Assert.Equal(15m, dto.CustoFrete);
        Assert.Equal(215m, dto.Total);
        Assert.Equal(new DateTime(2026, 1, 6), dto.CriadoEm);
        Assert.Single(dto.Itens);
    }

    [Fact]
    public void ItemPedidoDto_armazena_valores()
    {
        var dto = new ItemPedidoDto(1, 2, "Batom", 35m, 2, 70m);

        Assert.Equal(1, dto.Id);
        Assert.Equal(2, dto.ProdutoId);
        Assert.Equal("Batom", dto.NomeProduto);
        Assert.Equal(35m, dto.PrecoVendaUnitario);
        Assert.Equal(2, dto.Quantidade);
        Assert.Equal(70m, dto.Subtotal);
    }
}