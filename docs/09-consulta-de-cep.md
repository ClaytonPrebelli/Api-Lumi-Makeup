# 09 — Consulta de CEP

**Status:** ✅ concluído

---

## Objetivo

Preencher automaticamente logradouro, bairro, cidade e estado a partir do CEP digitado,
para que o cliente não precise digitar o endereço completo à mão.

---

## Endpoint

```http
GET /api/cep/{cep}
```

| Situação | Resposta |
|---|---|
| CEP encontrado | `200` com os dados |
| CEP não encontrado | `404` |
| Falha no ViaCEP | `404` |

Exemplo de resposta:

```json
{
  "cep": "01310-100",
  "logradouro": "Avenida Paulista",
  "bairro": "Bela Vista",
  "cidade": "São Paulo",
  "estado": "SP"
}
```

A rota é pública, sem `[Authorize]` — é usada no cadastro e na conclusão de perfil,
antes de o usuário ter token.

---

## Implementação

`ViaCepService`, registrado como `HttpClient` tipado:

```csharp
services.AddHttpClient<IViaCepService, ViaCepService>(client =>
{
    client.BaseAddress = new Uri("https://viacep.com.br/");
});
```

`BaseAddress` fica no registro, não no serviço — é o padrão do `HttpClient` tipado
do .NET, que cuida de pooling, timeout e ciclo de vida do handler.

### Normalização do CEP

```csharp
var cepLimpo = cep.Replace("-", string.Empty).Trim();
```

`01310-100` vira `01310100`. O traço é removido em vez de validado porque o campo já
passa pela máscara do frontend, e o ViaCEP só aceita os oito dígitos.

### Consulta

```csharp
var response = await _httpClient.GetFromJsonAsync<RespostaViaCep>($"ws/{cepLimpo}/json/", cancellationToken);
if (response is null || response.Erro) return null;
```

O ViaCEP sinaliza CEP inexistente de duas formas, e as duas são tratadas: corpo
vazio (`null`) ou o campo `erro` preenchido. Checar só um dos dois deixaria passar
CEP inválido.

### Tratamento de falha

```csharp
catch (Exception ex)
{
    _logger.LogError(ex, "Falha ao consultar ViaCEP para CEP {Cep}", cepLimpo);
    return null;
}
```

Timeout, DNS e resposta malformada caem todos no mesmo `catch`, viram `null` e
resultam em `404`. O frontend trata o endereço como não preenchido e o cliente
digita o restante.

O `catch (Exception)` é aqui uma decisão consciente: consulta de CEP é
conveniência, nunca requisito. Uma falha do terceiro-party não pode derrubar a
conclusão de cadastro — degrada o formulário, não a sessão.

O log é feito com `ILogger` e mensagem estruturada, sem interpolação de string, o
que permite filtrar por `Cep`.

---

## Mapeamento de nomes

O ViaCEP usa nomes próprios, diferentes do nosso domínio:

| ViaCEP | `ResultadoViaCep` |
|---|---|
| `localidade` | `cidade` |
| `uf` | `estado` |
| `logradouro` | `logradouro` |
| `bairro` | `bairro` |

A classe interna `RespostaViaCep` é `private sealed` — faz parte do detalhe de
integração e não vaza para a API. O que sai do serviço é o record `ResultadoViaCep`,
com os nomes do nosso domínio.

---

## Onde é usado

| Contexto | Uso |
|---|---|
| Conclusão de perfil | Preenche e salva o endereço padrão do usuário |
| Checkout (a implementar) | Preenche o endereço de entrega do pedido |

Na conclusão de perfil, o ViaCEP é consultado com o CEP informado; o resultado preenche
logradouro, bairro, cidade e estado. Se a consulta falhar, o endereço é salvo com
esses campos vazios e a geocodificação posterior (Nominatim) simplesmente não é
aplicada.
