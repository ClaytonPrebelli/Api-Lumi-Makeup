namespace LumiMakeup.Domain.Entities;

/// <summary>
/// A sessao do WhatsApp guardada no banco, para o Node nao depender de disco.
///
/// Existe por causa de um limite do Render no plano gratuito: nao ha disco
/// persistente. Se a sessao vivesse so em arquivo, cada sono do servico
/// apagaria o pareamento e a loja teria que escanear QR de novo - e o
/// WhatsApp bloqueia QR repetido.
///
/// Por isso o Node nao acessa o banco: ele pede esta sessao a API e devolve o
/// que mudou. O banco continua fechado para a internet, e o Render nunca
/// recebe credencial de MySQL.
///
/// O conteudo e JSON opaco do Baileys. Nao ha campo para consultar, so para
/// guardar e devolver inteiro.
/// </summary>
public class SessaoWhatsApp
{
    public long Id { get; set; }

    /// <summary>
    /// Blob de credenciais do Baileys. Sao poucas KB: identidade assinada,
    /// chave de ruido, par efemero, registro do dispositivo.
    /// </summary>
    public string Credenciais { get; set; } = "{}";

    /// <summary>
    /// Chaves de sinal em JSON: pre-keys, chaves de sessao e app-state-sync.
    ///
    /// Vai num unico blob, e nao numa linha por chave, porque o Node devolve
    /// o conjunto alterado inteiro e ler linha a linha custaria mais caro do
    /// que guardar o pacote. Sao algumas centenas de entradas, poucos MB no
    /// maximo, e o trafego e de uma loja com dezenas de mensagens por dia.
    /// </summary>
    public string Chaves { get; set; } = "{}";

    /// <summary>
    /// Sobe a cada gravacao.
    ///
    /// E o que impede que dois contêineres gravem por cima um do outro. Num
    /// redeploy o contêiner antigo ainda drena enquanto o novo sobe; sem
    /// versao, o velho acabaria sobrescrevendo a sessao recem-construida.
    /// </summary>
    public int Versao { get; set; }

    public DateTime AtualizadoEm { get; set; } = DateTime.UtcNow;
}
