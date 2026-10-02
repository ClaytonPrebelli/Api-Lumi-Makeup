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
    public void RequisicaoDeSolicitarResetDeSenha_armazena_email()
    {
        var requisicao = new RequisicaoDeSolicitarResetDeSenha("maria@exemplo.com");

        Assert.Equal("maria@exemplo.com", requisicao.Email);
    }

    [Fact]
    public void RequisicaoDeConfirmarResetDeSenha_armazena_token_e_senha()
    {
        var requisicao = new RequisicaoDeConfirmarResetDeSenha("token-de-reset", "novaSenha123");

        Assert.Equal("token-de-reset", requisicao.Token);
        Assert.Equal("novaSenha123", requisicao.NovaSenha);
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
        var dto = new ImagemProdutoDto(1, "produtos/foto.jpg", "foto.jpg", 2);

        Assert.Equal(1, dto.Id);
        Assert.Equal("produtos/foto.jpg", dto.CaminhoRelativo);
        Assert.Equal("foto.jpg", dto.NomeOriginal);
        Assert.Equal(2, dto.Ordem);
    }

    [Fact]
    public void ProdutoDto_armazena_valores()
    {
        var imagens = new List<ImagemProdutoDto> { new(1, "produtos/foto.jpg", "foto.jpg", 1) };
        var dto = new ProdutoDto(1, "Batom", "batom", "Batom vermelho", 39.9m, 29.9m, 5, true, false, 1, "Bases", imagens);

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
        var itens = new List<PedidoItemDto> { new(2, "Batom", 2, 35m, null, 70m) };
        var dto = new PedidoDto(
            1,
            7,
            "Ana",
            "123.456.789-00",
            "11999999999",
            "ana@exemplo.com",
            OrigemPedido.Online,
            StatusPedido.Pago,
            MetodoPagamento.Pix,
            "NATAL20",
            200m,
            40m,
            15m,
            175m,
            null,
            new DateTime(2026, 1, 6),
            new DateTime(2026, 1, 7),
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            itens);

        Assert.Equal(1, dto.Id);
        Assert.Equal(7, dto.UsuarioId);
        Assert.Equal("Ana", dto.NomeCliente);
        Assert.Equal("11999999999", dto.TelefoneContato);
        // Telefone e e-mail vao copiados para o pedido: o aviso sai depois que o
        // pedido existe, e o cadastro pode ter mudado no meio do caminho.
        Assert.Equal("ana@exemplo.com", dto.EmailContato);
        Assert.Equal(OrigemPedido.Online, dto.Origem);
        Assert.Equal(StatusPedido.Pago, dto.Status);
        Assert.Equal(MetodoPagamento.Pix, dto.MetodoPagamento);
        Assert.Equal("NATAL20", dto.CupomCodigo);
        Assert.Equal(200m, dto.Subtotal);
        // Desconto e subtotal sao campos separados de proposito: e o que permite
        // dizer quanto da receita veio de promocao e quanto de cupom.
        Assert.Equal(40m, dto.Desconto);
        Assert.Equal(15m, dto.CustoFrete);
        Assert.Equal(175m, dto.Total);
        Assert.Equal(new DateTime(2026, 1, 6), dto.CriadoEm);
        Assert.Equal(new DateTime(2026, 1, 7), dto.PagoEm);
        Assert.Single(dto.Itens);
    }

    [Fact]
    public void PedidoItemDto_guarda_o_preco_promocional_alem_do_de_venda()
    {
        var dto = new PedidoItemDto(2, "Batom", 2, 35m, 25m, 50m);

        // Os dois precos ficam: com so o cobrado, o relatorio nao diria quanto a
        // loja concessionou em promocao.
        Assert.Equal(2, dto.ProdutoId);
        Assert.Equal("Batom", dto.Nome);
        Assert.Equal(35m, dto.PrecoVendaUnitario);
        Assert.Equal(25m, dto.PrecoPromocionalUnitario);
        Assert.Equal(2, dto.Quantidade);
        Assert.Equal(50m, dto.Subtotal);
    }
}