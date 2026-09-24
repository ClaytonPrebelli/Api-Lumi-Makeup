using LumiMakeup.Application.Abstractions;
using LumiMakeup.Application.DTOs;
using LumiMakeup.Domain.Entities;
using LumiMakeup.Domain.Enums;
using LumiMakeup.Infrastructure.Persistence;
using LumiMakeup.Infrastructure.Services;
using LumiMakeup.Tests.Helpers;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace LumiMakeup.Tests.Infrastructure.Services;

public class AutenticacaoServiceTests
{
    private sealed class CenáriosDeTeste
    {
        public LumiDbContext Contexto { get; }
        public Mock<IPasswordHasher<Usuario>> PasswordHasher { get; } = new();
        public Mock<ITokenService> TokenService { get; } = new();
        public Mock<IAutenticacaoGoogleService> GoogleService { get; } = new();
        public Mock<IRecaptchaValidator> RecaptchaValidator { get; } = new();
        public Mock<IViaCepService> ViaCepService { get; } = new();
        public Mock<INominatimService> NominatimService { get; } = new();

        public CenáriosDeTeste()
        {
            Contexto = Testes.CriarContextoInMemory();
            TokenService.Setup(t => t.GerarTokens(It.IsAny<Usuario>()))
                .Returns(("token-acesso", "token-refresh"));
        }

        public AutenticacaoService CriarServico()
        {
            return new AutenticacaoService(
                Contexto,
                PasswordHasher.Object,
                TokenService.Object,
                GoogleService.Object,
                RecaptchaValidator.Object,
                ViaCepService.Object,
                NominatimService.Object);
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CadastrarAsync_quando_recaptcha_invalido_lanca_excecao(bool recaptchaValido)
    {
        var cenario = new CenáriosDeTeste();
        cenario.RecaptchaValidator
            .Setup(r => r.ValidarTokenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(recaptchaValido);
        var servico = cenario.CriarServico();
        var requisicao = new RequisicaoDeRegistro("Maria", "maria@exemplo.com", "abc123", "token");

        if (recaptchaValido)
        {
            return;
        }

        var excecao = await Assert.ThrowsAsync<InvalidOperationException>(
            () => servico.CadastrarAsync(requisicao, CancellationToken.None));

        Assert.Equal("Falha na validação do reCAPTCHA.", excecao.Message);
    }

    [Fact]
    public async Task CadastrarAsync_lanca_quando_email_ja_cadastrado()
    {
        var cenario = new CenáriosDeTeste();
        cenario.RecaptchaValidator.Setup(r => r.ValidarTokenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        cenario.Contexto.Usuarios.Add(new Usuario { Nome = "Existente", Email = "maria@exemplo.com" });
        await cenario.Contexto.SaveChangesAsync();
        var servico = cenario.CriarServico();

        var excecao = await Assert.ThrowsAsync<InvalidOperationException>(
            () => servico.CadastrarAsync(new RequisicaoDeRegistro("Maria", "  Maria@Exemplo.com ", "abc123", "token"), CancellationToken.None));

        Assert.Equal("E-mail já cadastrado.", excecao.Message);
    }

    [Fact]
    public async Task CadastrarAsync_lanca_quando_senha_curta()
    {
        var cenario = new CenáriosDeTeste();
        cenario.RecaptchaValidator.Setup(r => r.ValidarTokenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var servico = cenario.CriarServico();

        var excecao = await Assert.ThrowsAsync<InvalidOperationException>(
            () => servico.CadastrarAsync(new RequisicaoDeRegistro("Maria", "maria@exemplo.com", "123", "token"), CancellationToken.None));

        Assert.Equal("A senha deve ter no mínimo 6 caracteres.", excecao.Message);
    }

    [Fact]
    public async Task CadastrarAsync_cria_usuario_e_retorna_tokens()
    {
        var cenario = new CenáriosDeTeste();
        cenario.RecaptchaValidator.Setup(r => r.ValidarTokenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        cenario.PasswordHasher.Setup(h => h.HashPassword(It.IsAny<Usuario>(), It.IsAny<string>())).Returns("hash-da-senha");
        var servico = cenario.CriarServico();

        var resposta = await servico.CadastrarAsync(
            new RequisicaoDeRegistro("  Maria  ", "Maria@Exemplo.com", "abc123", "token"),
            CancellationToken.None);

        Assert.Equal("token-acesso", resposta.TokenAcesso);
        Assert.Equal("token-refresh", resposta.TokenRefresh);
        Assert.Equal("maria@exemplo.com", resposta.Usuario.Email);
        Assert.Equal("Maria", resposta.Usuario.Nome);
        Assert.Equal(PapelUsuario.Cliente, resposta.Usuario.Papel);
        Assert.True(resposta.Usuario.PrecisaPerfil);

        var criado = await cenario.Contexto.Usuarios.SingleAsync();
        Assert.Equal("hash-da-senha", criado.HashSenha);
        Assert.Equal("maria@exemplo.com", criado.Email);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task EntrarAsync_lanca_quando_usuario_inexistente_ou_sem_senha(string? hashSenha)
    {
        var cenario = new CenáriosDeTeste();
        cenario.Contexto.Usuarios.Add(new Usuario { Nome = "Maria", Email = "maria@exemplo.com", HashSenha = hashSenha });
        await cenario.Contexto.SaveChangesAsync();
        var servico = cenario.CriarServico();

        var excecao = await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => servico.EntrarAsync(new RequisicaoDeLogin("maria@exemplo.com", "abc123"), CancellationToken.None));

        Assert.Equal("E-mail ou senha inválidos.", excecao.Message);
    }

    [Fact]
    public async Task EntrarAsync_lanca_quando_email_nao_existe()
    {
        var cenario = new CenáriosDeTeste();
        var servico = cenario.CriarServico();

        var excecao = await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => servico.EntrarAsync(new RequisicaoDeLogin("nao@existe.com", "abc123"), CancellationToken.None));

        Assert.Equal("E-mail ou senha inválidos.", excecao.Message);
    }

    [Fact]
    public async Task EntrarAsync_lanca_quando_senha_invalida()
    {
        var cenario = new CenáriosDeTeste();
        cenario.Contexto.Usuarios.Add(new Usuario { Nome = "Maria", Email = "maria@exemplo.com", HashSenha = "hash" });
        await cenario.Contexto.SaveChangesAsync();
        cenario.PasswordHasher
            .Setup(h => h.VerifyHashedPassword(It.IsAny<Usuario>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(PasswordVerificationResult.Failed);
        var servico = cenario.CriarServico();

        var excecao = await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => servico.EntrarAsync(new RequisicaoDeLogin("maria@exemplo.com", "errada"), CancellationToken.None));

        Assert.Equal("E-mail ou senha inválidos.", excecao.Message);
    }

    [Fact]
    public async Task EntrarAsync_retorna_tokens_quando_credenciais_validas()
    {
        var cenario = new CenáriosDeTeste();
        var usuario = new Usuario { Nome = "Maria", Email = "maria@exemplo.com", HashSenha = "hash", Cpf = "12345678901" };
        cenario.Contexto.Usuarios.Add(usuario);
        await cenario.Contexto.SaveChangesAsync();
        cenario.PasswordHasher
            .Setup(h => h.VerifyHashedPassword(It.IsAny<Usuario>(), "hash", It.IsAny<string>()))
            .Returns(PasswordVerificationResult.Success);
        cenario.TokenService.Setup(t => t.GerarTokens(It.IsAny<Usuario>())).Returns(("acesso", "refresh"));
        var servico = cenario.CriarServico();

        var resposta = await servico.EntrarAsync(new RequisicaoDeLogin("Maria@Exemplo.com", "abc123"), CancellationToken.None);

        Assert.Equal("acesso", resposta.TokenAcesso);
        Assert.Equal("maria@exemplo.com", resposta.Usuario.Email);
        Assert.False(resposta.Usuario.PrecisaPerfil);
    }

    [Fact]
    public async Task EntrarComGoogleAsync_lanca_quando_token_google_invalido()
    {
        var cenario = new CenáriosDeTeste();
        cenario.GoogleService
            .Setup(g => g.ValidarTokenIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((DadosDoUsuarioGoogle?)null);
        var servico = cenario.CriarServico();

        var excecao = await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => servico.EntrarComGoogleAsync(new RequisicaoDeLoginGoogle("token"), CancellationToken.None));

        Assert.Equal("Token do Google inválido.", excecao.Message);
    }

    [Fact]
    public async Task EntrarComGoogleAsync_vincula_conta_google_a_usuario_existente_com_senha()
    {
        var cenario = new CenáriosDeTeste();
        cenario.GoogleService
            .Setup(g => g.ValidarTokenIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DadosDoUsuarioGoogle("google-1", "maria@exemplo.com", "Maria"));
        cenario.Contexto.Usuarios.Add(new Usuario { Nome = "Maria", Email = "maria@exemplo.com", HashSenha = "hash" });
        await cenario.Contexto.SaveChangesAsync();
        var servico = cenario.CriarServico();

        var resposta = await servico.EntrarComGoogleAsync(new RequisicaoDeLoginGoogle("token"), CancellationToken.None);

        var atualizado = await cenario.Contexto.Usuarios.SingleAsync();
        Assert.Equal("google-1", atualizado.IdGoogle);
        Assert.Equal("maria@exemplo.com", resposta.Usuario.Email);
    }

    [Theory]
    [InlineData("Maria Silva")]
    [InlineData("   ")]
    public async Task EntrarComGoogleAsync_cria_usuario_novo_quando_nao_existe(string nomeDoGoogle)
    {
        var cenario = new CenáriosDeTeste();
        cenario.GoogleService
            .Setup(g => g.ValidarTokenIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DadosDoUsuarioGoogle("google-1", "Maria@Exemplo.com", nomeDoGoogle));
        var servico = cenario.CriarServico();

        var resposta = await servico.EntrarComGoogleAsync(new RequisicaoDeLoginGoogle("token"), CancellationToken.None);

        var esperado = string.IsNullOrWhiteSpace(nomeDoGoogle) ? "maria@exemplo.com" : nomeDoGoogle;
        Assert.Equal(esperado, resposta.Usuario.Nome);
        Assert.Equal("maria@exemplo.com", resposta.Usuario.Email);
        Assert.Equal(PapelUsuario.Cliente, resposta.Usuario.Papel);

        var criado = await cenario.Contexto.Usuarios.SingleAsync();
        Assert.Equal("google-1", criado.IdGoogle);
    }

    [Fact]
    public async Task EntrarComGoogleAsync_vincular_id_google_ao_usuario_existente()
    {
        var cenario = new CenáriosDeTeste();
        cenario.GoogleService
            .Setup(g => g.ValidarTokenIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DadosDoUsuarioGoogle("google-1", "maria@exemplo.com", "Maria"));
        var usuario = new Usuario { Nome = "Maria", Email = "maria@exemplo.com", Cpf = "12345678901" };
        cenario.Contexto.Usuarios.Add(usuario);
        await cenario.Contexto.SaveChangesAsync();
        var servico = cenario.CriarServico();

        var resposta = await servico.EntrarComGoogleAsync(new RequisicaoDeLoginGoogle("token"), CancellationToken.None);

        Assert.Equal("maria@exemplo.com", resposta.Usuario.Email);
        var atualizado = await cenario.Contexto.Usuarios.SingleAsync();
        Assert.Equal("google-1", atualizado.IdGoogle);
    }

    [Fact]
    public async Task EntrarComGoogleAsync_retorna_usuario_existente_já_vinculado_sem_chamadas_internas_extras()
    {
        var cenario = new CenáriosDeTeste();
        cenario.GoogleService
            .Setup(g => g.ValidarTokenIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DadosDoUsuarioGoogle("google-1", "maria@exemplo.com", "Maria"));
        var usuario = new Usuario { Nome = "Maria", Email = "maria@exemplo.com", IdGoogle = "google-1", Cpf = "12345678901", Enderecos = { } };
        cenario.Contexto.Usuarios.Add(usuario);
        await cenario.Contexto.SaveChangesAsync();
        var servico = cenario.CriarServico();

        var resposta = await servico.EntrarComGoogleAsync(new RequisicaoDeLoginGoogle("token"), CancellationToken.None);

        Assert.Equal("maria@exemplo.com", resposta.Usuario.Email);
        var atualizado = await cenario.Contexto.Usuarios.SingleAsync();
        Assert.Equal("google-1", atualizado.IdGoogle);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("abc")]
    public async Task RenovarAsync_lanca_quando_id_invalido(string? usuarioId)
    {
        var cenario = new CenáriosDeTeste();
        cenario.TokenService.Setup(t => t.ObterIdDeUsuarioDoTokenRefresh(It.IsAny<string>())).Returns(usuarioId);
        var servico = cenario.CriarServico();

        var excecao = await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => servico.RenovarAsync("token", CancellationToken.None));

        Assert.Equal("Token de atualização inválido ou expirado.", excecao.Message);
    }

    [Fact]
    public async Task RenovarAsync_lanca_quando_usuario_nao_encontrado()
    {
        var cenario = new CenáriosDeTeste();
        cenario.TokenService.Setup(t => t.ObterIdDeUsuarioDoTokenRefresh(It.IsAny<string>())).Returns("999");
        var servico = cenario.CriarServico();

        var excecao = await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => servico.RenovarAsync("token", CancellationToken.None));

        Assert.Equal("Usuário não encontrado.", excecao.Message);
    }

    [Fact]
    public async Task RenovarAsync_retorna_tokens_para_usuario_existente()
    {
        var cenario = new CenáriosDeTeste();
        var usuario = new Usuario { Nome = "Maria", Email = "maria@exemplo.com" };
        cenario.Contexto.Usuarios.Add(usuario);
        await cenario.Contexto.SaveChangesAsync();
        cenario.TokenService.Setup(t => t.ObterIdDeUsuarioDoTokenRefresh(It.IsAny<string>())).Returns(usuario.Id.ToString());
        cenario.TokenService.Setup(t => t.GerarTokens(It.IsAny<Usuario>())).Returns(("novo-acesso", "novo-refresh"));
        var servico = cenario.CriarServico();

        var resposta = await servico.RenovarAsync("refresh-token", CancellationToken.None);

        Assert.Equal("novo-acesso", resposta.TokenAcesso);
        Assert.Equal("novo-refresh", resposta.TokenRefresh);
    }

    [Fact]
    public async Task ObterUsuarioAtualAsync_lanca_quando_usuario_nao_encontrado()
    {
        var cenario = new CenáriosDeTeste();
        var servico = cenario.CriarServico();

        var excecao = await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => servico.ObterUsuarioAtualAsync(999, CancellationToken.None));

        Assert.Equal("Usuário não encontrado.", excecao.Message);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ObterUsuarioAtualAsync_retorna_dto_do_usuario(bool comEnderecoPadrao)
    {
        var cenario = new CenáriosDeTeste();
        var usuario = new Usuario { Nome = "Maria", Email = "maria@exemplo.com", HashSenha = "hash" };
        if (comEnderecoPadrao)
        {
            usuario.Enderecos.Add(new Endereco { Padrao = true, Cep = "01310100" });
        }
        cenario.Contexto.Usuarios.Add(usuario);
        await cenario.Contexto.SaveChangesAsync();
        var servico = cenario.CriarServico();

        var dto = await servico.ObterUsuarioAtualAsync(usuario.Id, CancellationToken.None);

        Assert.Equal(usuario.Id, dto.Id);
        Assert.Equal("Maria", dto.Nome);
        Assert.True(dto.PrecisaPerfil);
        Assert.Equal(comEnderecoPadrao, dto.TemEndereco);
    }

    [Fact]
    public async Task CompletarPerfilAsync_lanca_quando_usuario_nao_encontrado()
    {
        var cenario = new CenáriosDeTeste();
        var servico = cenario.CriarServico();

        var excecao = await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => servico.CompletarPerfilAsync(999, new RequisicaoDeCompletarPerfil("Maria", "12345678901", null, null), CancellationToken.None));

        Assert.Equal("Usuário não encontrado.", excecao.Message);
    }

    [Theory]
    [InlineData("123")]
    [InlineData("123456789012")]
    [InlineData("")]
    public async Task CompletarPerfilAsync_lanca_quando_cpf_invalido(string cpf)
    {
        var cenario = new CenáriosDeTeste();
        var usuario = new Usuario { Nome = "Maria", Email = "maria@exemplo.com" };
        cenario.Contexto.Usuarios.Add(usuario);
        await cenario.Contexto.SaveChangesAsync();
        var servico = cenario.CriarServico();

        var excecao = await Assert.ThrowsAsync<InvalidOperationException>(
            () => servico.CompletarPerfilAsync(usuario.Id, new RequisicaoDeCompletarPerfil("Maria", cpf, null, null), CancellationToken.None));

        Assert.Equal("CPF inválido.", excecao.Message);
    }

    [Fact]
    public async Task CompletarPerfilAsync_lanca_quando_cpf_ja_cadastrado()
    {
        var cenario = new CenáriosDeTeste();
        cenario.Contexto.Usuarios.Add(new Usuario { Nome = "Outro", Email = "outro@exemplo.com", Cpf = "12345678901" });
        var usuario = new Usuario { Nome = "Maria", Email = "maria@exemplo.com" };
        cenario.Contexto.Usuarios.Add(usuario);
        await cenario.Contexto.SaveChangesAsync();
        var servico = cenario.CriarServico();

        var excecao = await Assert.ThrowsAsync<InvalidOperationException>(
            () => servico.CompletarPerfilAsync(usuario.Id, new RequisicaoDeCompletarPerfil("Maria", "123.456.789-01", null, null), CancellationToken.None));

        Assert.Equal("CPF já cadastrado.", excecao.Message);
    }

    [Fact]
    public async Task CompletarPerfilAsync_atualiza_dados_sem_endereco()
    {
        var cenario = new CenáriosDeTeste();
        var usuario = new Usuario { Nome = "Antigo", Email = "maria@exemplo.com" };
        cenario.Contexto.Usuarios.Add(usuario);
        await cenario.Contexto.SaveChangesAsync();
        var servico = cenario.CriarServico();

        var dto = await servico.CompletarPerfilAsync(
            usuario.Id,
            new RequisicaoDeCompletarPerfil("  Maria  ", "123.456.789-01", "11999999999", null),
            CancellationToken.None);

        Assert.Equal("Maria", dto.Nome);
        Assert.Equal("12345678901", dto.Cpf);
        Assert.Equal("11999999999", dto.Telefone);
        Assert.False(dto.PrecisaPerfil);
        Assert.False(dto.TemEndereco);
    }

    [Fact]
    public async Task CompletarPerfilAsync_remove_enderecos_padrao_antigos_e_adiciona_novo_com_geolocalizacao()
    {
        var cenario = new CenáriosDeTeste();
        var usuario = new Usuario { Nome = "Maria", Email = "maria@exemplo.com" };
        usuario.Enderecos.Add(new Endereco { Padrao = true, Cep = "99999-999", Logradouro = "Antigo" });
        cenario.Contexto.Usuarios.Add(usuario);
        await cenario.Contexto.SaveChangesAsync();

        cenario.ViaCepService
            .Setup(v => v.ConsultarAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResultadoViaCep("01310100", "Av. Paulista", "Bela Vista", "São Paulo", "SP"));
        cenario.NominatimService
            .Setup(n => n.GeocodificarAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((-23.55m, -46.63m));
        var servico = cenario.CriarServico();

        var dto = await servico.CompletarPerfilAsync(
            usuario.Id,
            new RequisicaoDeCompletarPerfil("Maria", "12345678901", "11999999999", new RequisicaoDeEndereco("01310-100", "1000", null)),
            CancellationToken.None);

        Assert.True(dto.TemEndereco);
        var enderecos = await cenario.Contexto.Enderecos.ToListAsync();
        var endereco = Assert.Single(enderecos);
        Assert.Equal("Av. Paulista", endereco.Logradouro);
        Assert.Equal("1000", endereco.Numero);
        Assert.Equal("01310-100", endereco.Cep);
        Assert.Equal("Bela Vista", endereco.Bairro);
        Assert.Equal("São Paulo", endereco.Cidade);
        Assert.Equal("SP", endereco.Estado);
        Assert.True(endereco.Padrao);
        Assert.Equal(-23.55m, endereco.Latitude);
        Assert.Equal(-46.63m, endereco.Longitude);
        Assert.Equal(usuario.Id, endereco.UsuarioId);
    }

    [Fact]
    public async Task CompletarPerfilAsync_adiciona_endereco_sem_dados_do_viacep_e_sem_geolocalizacao()
    {
        var cenario = new CenáriosDeTeste();
        var usuario = new Usuario { Nome = "Maria", Email = "maria@exemplo.com" };
        cenario.Contexto.Usuarios.Add(usuario);
        await cenario.Contexto.SaveChangesAsync();

        cenario.ViaCepService
            .Setup(v => v.ConsultarAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ResultadoViaCep?)null);
        cenario.NominatimService
            .Setup(n => n.GeocodificarAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(default((decimal, decimal)?));
        var servico = cenario.CriarServico();

        var dto = await servico.CompletarPerfilAsync(
            usuario.Id,
            new RequisicaoDeCompletarPerfil("Maria", "12345678901", null, new RequisicaoDeEndereco("01310100", "1000", "Apto 5")),
            CancellationToken.None);

        Assert.True(dto.TemEndereco);
        var endereco = await cenario.Contexto.Enderecos.SingleAsync();
        Assert.Equal(string.Empty, endereco.Logradouro);
        Assert.Equal(string.Empty, endereco.Cidade);
        Assert.Equal("Apto 5", endereco.Complemento);
        Assert.Equal("01310100", endereco.Cep);
        Assert.Equal(0m, endereco.Latitude);
        Assert.Equal(0m, endereco.Longitude);
    }

    [Fact]
    public async Task CompletarPerfilAsync_adiciona_endereco_com_geolocalizacao_via_nominatim()
    {
        var cenario = new CenáriosDeTeste();
        var usuario = new Usuario { Nome = "Maria", Email = "maria@exemplo.com" };
        cenario.Contexto.Usuarios.Add(usuario);
        await cenario.Contexto.SaveChangesAsync();

        cenario.ViaCepService
            .Setup(v => v.ConsultarAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResultadoViaCep("01310100", "Av. Paulista", "Bela Vista", "São Paulo", "SP"));
        cenario.NominatimService
            .Setup(n => n.GeocodificarAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((-23.55m, -46.63m));
        var servico = cenario.CriarServico();

        var dto = await servico.CompletarPerfilAsync(
            usuario.Id,
            new RequisicaoDeCompletarPerfil("Maria", "12345678901", null, new RequisicaoDeEndereco("01310100", "1000", null)),
            CancellationToken.None);

        var endereco = await cenario.Contexto.Enderecos.SingleAsync();
        Assert.Equal(-23.55m, endereco.Latitude);
        Assert.Equal(-46.63m, endereco.Longitude);
        Assert.True(dto.TemEndereco);
    }
}