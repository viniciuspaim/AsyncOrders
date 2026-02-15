# AsyncOrders

Sistema distribuído de processamento assíncrono de pedidos construído com .NET 10 utilizando Clean Architecture, Outbox Pattern, Inbox Pattern, RabbitMQ, SQL Server e testes de integração com containers reais.

---

## 🚀 Visão Geral

O AsyncOrders simula um sistema de processamento distribuído com garantias reais de confiabilidade e consistência eventual.

Este projeto demonstra:

- Clean Architecture
- Outbox Pattern (entrega garantida de mensagens)
- Inbox Pattern (processamento idempotente)
- Retry com filas de atraso (delay queues)
- Dead Letter Queue (DLQ)
- Persistência em SQL Server
- Mensageria com RabbitMQ
- Testes de integração com Testcontainers

Não é um CRUD simples. É um backend orientado a produção.

---

## 🏗 Arquitetura

API
└── Cria Order
└── Grava Order + Outbox na mesma transação

OutboxDispatcher
└── Publica mensagens pendentes no RabbitMQ
└── Marca como processadas

Worker
└── Consome evento
└── Verifica Inbox (idempotência)
└── Processa pedido
└── Retry com backoff via delay queues
└── DLQ após número máximo de tentativas

---

## 🧠 Padrões Implementados

### ✔ Clean Architecture

Separação clara de responsabilidades:

- Domain
- Application
- Infrastructure
- API
- Worker
- Tests

Inversão de dependência aplicada corretamente.

---

### ✔ Outbox Pattern

Garante consistência entre banco e mensageria:

- Order e OutboxMessage são salvos na mesma transação.
- Dispatcher publica mensagens de forma assíncrona.
- Mensagem só é marcada como processada após publicação bem-sucedida.

---

### ✔ Inbox Pattern

Garante processamento idempotente:

- Controle por CorrelationId.
- Evita reprocessamento.
- Seguro contra retries e duplicação de mensagens.

---

### ✔ Retry e DLQ

- Retry com delay progressivo (5s / 15s / 30s / 60s)
- Controle de tentativas máximas
- Dead Letter Queue para mensagens inválidas ou falhas definitivas

---

## 🧪 Estratégia de Testes

### Testes Unitários
- Validação do OutboxWriter
- Comportamento de domínio

### Testes de Integração
- SQL Server real em container
- RabbitMQ real em container
- Validação completa do fluxo Outbox → Rabbit → Worker
- Dispatcher executando de fato

Todos os testes de integração utilizam Testcontainers.

Executar testes:

```bash
dotnet test

🐳 Execução com Docker

Serviços utilizados:
SQL Server 2022
RabbitMQ (com management)

Subir containers:
docker compose up -d

Depois executar:
dotnet run --project AsyncOrders.Api
dotnet run --project AsyncOrders.Worker

📦 Stack Tecnológica

.NET 10
ASP.NET Core
Entity Framework Core
SQL Server
RabbitMQ
Testcontainers
xUnit
FluentAssertions

📊 Características de Produção

Consistência eventual
Idempotência
Retry resiliente
DLQ explícita
Transação atômica (Order + Outbox)
Serviços em background
Testes de integração reais

Este projeto foi desenvolvido para demonstrar:
Arquitetura distribuída
Confiabilidade em sistemas assíncronos
Tratamento de falhas
Boas práticas de backend
Testabilidade de infraestrutura
