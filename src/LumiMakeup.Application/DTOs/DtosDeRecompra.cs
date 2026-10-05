namespace LumiMakeup.Application.DTOs;

public sealed record ConfiguracaoDeRecompraDto(
    bool Ativa,
    string Assunto,
    string Mensagem,
    DateTime? AtualizadoEm);

public sealed record RequisicaoDeConfiguracaoDeRecompra(
    bool Ativa,
    string? Assunto,
    string? Mensagem);

public sealed record ResultadoDeDisparoDeRecompraDto(int Enviados);
