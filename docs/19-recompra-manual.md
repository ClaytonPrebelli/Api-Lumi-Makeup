# 19 — E-mail manual de recompra

**Status:** ✅ implementado

---

## Fluxo administrativo

O painel usa somente os endpoints protegidos pela política `SomenteAdministrador`:

| Verbo | Rota | Resultado |
|---|---|---|
| GET | `/api/admin/recompra` | Configuração (`ativa`, `assunto`, `mensagem`, `atualizadoEm`) |
| POST | `/api/admin/recompra` | Salva a configuração; `atualizadoEm` é gerado pelo servidor |
| POST | `/api/admin/recompra/disparar` | Dispara manualmente e retorna `{ "enviados": n }` |

Não há Hangfire, job, timer nem envio automático. Só a chamada administrativa
`POST /disparar` inicia uma execução. Configuração ausente ou desativada retorna
`enviados: 0`.

## Elegibilidade e idempotência

Um destinatário precisa ter um pedido pago (`Status = Pago`) cuja data `PagoEm`
esteja entre 30 e 40 dias antes do disparo, inclusive, um e-mail salvo no pedido e
nenhum pedido posterior do mesmo cliente. A janela usa o instante UTC do servidor.

`envios_recompra` usa `PedidoId` como chave única. A linha é reservada antes do
envio para impedir que dois disparos simultâneos enviem o mesmo pedido; pedidos
confirmados ficam marcados com `EnviadoEm`. Uma falha libera a reserva e não entra
na contagem, para que a administração possa tentar novamente. Reserva abandonada
por uma interrupção pode ser retomada após dez minutos.

O contador inclui apenas envios que `IEmailSenderComConfirmacao` confirma. SMTP
bem-sucedido confirma; o stub de desenvolvimento retorna falso porque não envia
de verdade. `IEmailSender` mantém seu comportamento anterior para o fluxo de
recuperação de senha.

## Persistência

A configuração fica em `configuracao_recompra`; o controle por pedido fica em
`envios_recompra`. As tabelas são criadas pela migration
`20261004230000_RecompraManual` como InnoDB e não têm foreign key: o identificador
do pedido é persistido como chave de idempotência, sem dependência de engine em
tabelas legadas.
