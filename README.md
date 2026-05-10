# Mecânica Hermes — API de Pagamentos

> Microsserviço HTTP que orquestra o ciclo de vida de pagamentos via **Mercado Pago**:
> geração de link, envio ao cliente, confirmação por **webhook + polling agendado** e
> publicação do callback de aprovação para a API de Ordem de Serviço.

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4)](https://dotnet.microsoft.com/)
[![MongoDB](https://img.shields.io/badge/MongoDB-7-47A248)](https://www.mongodb.com/)
[![RabbitMQ](https://img.shields.io/badge/RabbitMQ-3-FF6600)](https://www.rabbitmq.com/)
[![Mercado Pago](https://img.shields.io/badge/Mercado%20Pago-Preference%20API-009EE3)](https://www.mercadopago.com.br/developers/)

[![Quality Gate Status](https://sonarcloud.io/api/project_badges/measure?project=fiap-challenge-13soat_mecanica-hermes-api-pagamentos&metric=alert_status&token=87e572f763af380c4014521d5d907701425e530c)](https://sonarcloud.io/summary/new_code?id=fiap-challenge-13soat_mecanica-hermes-api-pagamentos)
[![Coverage](https://sonarcloud.io/api/project_badges/measure?project=fiap-challenge-13soat_mecanica-hermes-api-pagamentos&metric=coverage&token=87e572f763af380c4014521d5d907701425e530c)](https://sonarcloud.io/summary/new_code?id=fiap-challenge-13soat_mecanica-hermes-api-pagamentos)
[![Maintainability Rating](https://sonarcloud.io/api/project_badges/measure?project=fiap-challenge-13soat_mecanica-hermes-api-pagamentos&metric=sqale_rating&token=87e572f763af380c4014521d5d907701425e530c)](https://sonarcloud.io/summary/new_code?id=fiap-challenge-13soat_mecanica-hermes-api-pagamentos)
[![Reliability Rating](https://sonarcloud.io/api/project_badges/measure?project=fiap-challenge-13soat_mecanica-hermes-api-pagamentos&metric=reliability_rating&token=87e572f763af380c4014521d5d907701425e530c)](https://sonarcloud.io/summary/new_code?id=fiap-challenge-13soat_mecanica-hermes-api-pagamentos)
[![Security Rating](https://sonarcloud.io/api/project_badges/measure?project=fiap-challenge-13soat_mecanica-hermes-api-pagamentos&metric=security_rating&token=87e572f763af380c4014521d5d907701425e530c)](https://sonarcloud.io/summary/new_code?id=fiap-challenge-13soat_mecanica-hermes-api-pagamentos)

---

## Sumário

- [O que este serviço faz](#o-que-este-serviço-faz)
- [Stack](#stack)
- [Pré-requisitos](#pré-requisitos)
- [Início rápido](#início-rápido)
- [Endpoints](#endpoints)
- [Estrutura do repositório](#estrutura-do-repositório)
- [Documentação](#documentação)
- [Equipe](#equipe)

---

## O que este serviço faz

A API expõe um único agregado de domínio — **`Pagamento`** — e os comandos que fazem ele
transitar entre estados (`PendenteGeracao → LinkGerado → AguardandoConfirmacao → Pago`,
com transições alternativas para `Recusado` e `Expirado`).

Características importantes:

- **Endpoints de mutação são assíncronos:** o cliente recebe `202 Accepted` imediatamente; o
  trabalho é encaminhado por uma SAGA do MassTransit que serializa as operações por
  `PagamentoId` (no máximo uma operação em voo por pagamento).
- **Outbox transacional em MongoDB:** *domain events* são persistidos na mesma transação do
  agregado e despachados por um `BackgroundService`, garantindo entrega *at-least-once* mesmo
  em quedas do broker.
- **State Pattern no domínio:** cada status é uma classe própria que conhece suas próprias
  transições e permissões.
- **Confirmação dupla:** `POST /api/webhooks/mercadopago` (com validação HMAC) **+** polling
  agendado via `IMessageScheduler` do MassTransit, garantindo confirmação mesmo se o webhook
  falhar.
- **Callback à OS:** ao confirmar pagamento, publica `PagamentoAprovadoRequest { OrdemDeServicoId }`
  no bus — contrato compatível com a SAGA da OS.
- **Autenticação JWT + scopes** com bypass automático em `Development`/`Testing`.
- **OpenTelemetry** para logs, traces e métricas (exporta via OTLP/HTTP — testado contra
  New Relic).

Para detalhes de fluxo, mensageria e arquitetura, ver [`docs/`](./docs/).

## Stack

- **.NET 10** + ASP.NET Core (Minimal API) + C# 13
- **MongoDB 7** (driver oficial; sem EF Core, sem migrations relacionais)
- **MassTransit 8** (transports suportados: RabbitMQ, Amazon SQS) + **DelayedMessageScheduler** para polling
- **MongoDB** para persistência de estado da SAGA e do agregado Pagamento
- **MediatR 14** + **Autofac 9** + **FluentValidation 11**
- **Mercado Pago SDK** (Preference API) com **Polly** para resiliência
- **OpenTelemetry** (OTLP → New Relic) + **Serilog**
- **xUnit / FluentAssertions / Moq / Bogus / Testcontainers / WireMock.Net** para testes

## Pré-requisitos

| Ferramenta | Versão | Necessidade |
| --- | --- | --- |
| [.NET SDK](https://dotnet.microsoft.com/download/dotnet/10.0) | 10.0+ | obrigatória |
| [Docker](https://www.docker.com/get-started) | qualquer atual | testes de integração + serviços de dev |
| [just](https://github.com/casey/just) | qualquer | opcional — atalhos de build/test |

Para rodar a API localmente sem Docker, é preciso ter MongoDB 7 (replica set, para
suporte a transações multi-documento), RabbitMQ (com plugin
`rabbitmq_delayed_message_exchange`) e — opcionalmente — uma conta sandbox no Mercado Pago
acessíveis (ou substituir por mocks — ver [`docs/configuration.md`](./docs/configuration.md)).

## Início rápido

### 1. Subir as dependências de runtime

```bash
docker run -d --name hermes-pag-mongo -p 27017:27017 mongo:7 \
  --replSet rs0 --bind_ip_all
docker exec hermes-pag-mongo mongosh --eval "rs.initiate()"

docker run -d --name hermes-pag-rabbitmq -p 5672:5672 -p 15672:15672 \
  rabbitmq:3-management
# Ative o plugin de delayed message exchange manualmente, se necessário.
```

### 2. Rodar a API

```bash
# usando o seu launchSettings.json local (perfil "http")
dotnet run --project src/Mecanica.Hermes.Pagamento.Api

# ou via just
just run
```

> O `launchSettings.json` **não** é versionado — cada contribuidor mantém o seu próprio
> com as variáveis listadas em [`docs/configuration.md`](./docs/configuration.md).

### 3. URLs locais

| Recurso | URL |
| --- | --- |
| API | <http://localhost:5017> |
| Documentação interativa (Scalar) | <http://localhost:5017/scalar> |
| OpenAPI JSON | <http://localhost:5017/openapi/v1.json> |
| Liveness | <http://localhost:5017/healthz/live> |
| Readiness | <http://localhost:5017/healthz/ready> |
| RabbitMQ Management | <http://localhost:15672> (`guest` / `guest`) |

> Em `Development` e `Testing`, todas as requisições são autenticadas automaticamente pelo
> `DevelopmentAuthenticationMiddleware` — não é necessário token JWT.

### 4. Rodar os testes

```bash
# tudo (requer Docker — testes de integração usam Testcontainers)
dotnet test Mecanica.Hermes.Pagamento.sln

# apenas unitários (sem Docker)
just test-unit
```

Cobertura mínima exigida pelo CI: **≥ 80%** de linhas. Veja
[`docs/testing.md`](./docs/testing.md) para detalhes.

## Stack completa via Docker Compose

O `docker-compose.yml` da stack inteira do ecossistema Mecânica Hermes — Postgres,
MongoDB (com replica set), RabbitMQ (com plugin de delayed exchange), **WireMock como
mock do Mercado Pago** e as 3 APIs (`api-ordem-servico`, `api-cadastros`,
`api-pagamentos`) — **vive no repositório de testes E2E**,
[`mecanica-hermes-tests-e2e`](https://github.com/fiap-challenge-13soat/mecanica-hermes-tests-e2e).
É a forma canônica de rodar o ecossistema localmente sem precisar buildar cada API
nem expor a integração ao sandbox real do Mercado Pago:

```bash
git clone https://github.com/fiap-challenge-13soat/mecanica-hermes-tests-e2e.git ../mecanica-hermes-tests-e2e
cd ../mecanica-hermes-tests-e2e
docker compose \
  -f docker-compose/docker-compose.yaml \
  -f docker-compose/docker-compose.e2e.yaml \
  up -d --wait
```

A imagem desta API consumida pelo compose vem do Docker Hub
(`mechermes/mecanica-hermes-api-pagamentos:latest`), republicada automaticamente pelo
workflow [`Build and Publish Docker Image`](./.github/workflows/docker-ci.yml) a cada
merge na `main`. Para apontar para uma tag específica, exporte
`PAGAMENTOS_IMAGE_TAG=<sha-curto-ou-versão>` antes do `compose up`.

WireMock fica em `http://wiremock:8080` (rede docker) / `http://localhost:8090`
(host). Mappings padrão estão em `docker-compose/services/wiremock/mappings/`; a
suíte E2E pode injetar mappings dinâmicos via `POST /__admin/mappings`.

## Endpoints

Base path: `/api/pagamentos` e `/api/webhooks/mercadopago`.

| Método | Rota | Auth scope | Descrição |
| --- | --- | --- | --- |
| `POST` | `/api/pagamentos` | admin | Solicitar geração de pagamento (assíncrono → `202 Accepted`) |
| `GET` | `/api/pagamentos/{id}` | cliente, admin | Buscar pagamento por id |
| `GET` | `/api/pagamentos/{id}/status` | admin | Forçar verificação de status no MP (debug) |
| `POST` | `/api/webhooks/mercadopago` | público + HMAC | Callback do Mercado Pago |
| `GET` | `/healthz/live` & `/healthz/ready` | público | Health checks (Mongo, RabbitMQ) |

**Endpoints de mutação retornam `202 Accepted`** — não esperam o consumer concluir. Veja
[`docs/api-reference.md`](./docs/api-reference.md) para o ciclo "publish → consume → query".

## Estrutura do repositório

```text
.
├── src/
│   ├── Mecanica.Hermes.Pagamento.Api/             Minimal API, middleware, presenter
│   ├── Mecanica.Hermes.Pagamento.Application/     CQRS, MediatR, MassTransit consumers, ports
│   ├── Mecanica.Hermes.Pagamento.Domain/          Agregado Pagamento, Value Objects, Domain Events
│   └── Mecanica.Hermes.Pagamento.Infrastructure/  MongoDB, repositórios, Outbox, SAGA, Mercado Pago, OpenTelemetry
├── tests/
│   ├── Mecanica.Hermes.Pagamento.Domain.Tests/
│   ├── Mecanica.Hermes.Pagamento.Application.Tests/
│   ├── Mecanica.Hermes.Pagamento.Infrastructure.Tests/
│   ├── Mecanica.Hermes.Pagamento.Api.Tests/
│   ├── Mecanica.Hermes.Pagamento.IntegrationTests/      WebApplicationFactory + Testcontainers + WireMock
│   ├── Mecanica.Hermes.Pagamento.ArchitectureTests/     NetArchTest — regras de dependência
│   └── Mecanica.Hermes.Pagamento.TestUtils/             builders compartilhados (não roda)
├── docs/                                Documentação detalhada
├── .github/workflows/                   CI: build+tests, docker, k8s deploy trigger
├── Dockerfile                           multi-stage (.NET 10 SDK → aspnet:10)
├── Directory.Build.props                propriedades MSBuild compartilhadas
├── Directory.Packages.props             versões NuGet centralizadas (CPM)
├── coverlet.runsettings                 configuração de cobertura
└── justfile                             atalhos para build, test, run
```

## Documentação

Documentação técnica detalhada está em [`docs/`](./docs/):

- [Arquitetura](./docs/architecture.md) — camadas, padrões, dependências
- [Fluxo de Pagamento](./docs/payment-flow.md) — máquina de estados e ciclo de confirmação
- [Mensageria](./docs/messaging.md) — SAGA, Outbox, contratos, pipeline de consumers
- [Banco de dados](./docs/database.md) — coleções Mongo, índices
- [Referência da API](./docs/api-reference.md) — endpoints, auth, semântica assíncrona
- [Configuração](./docs/configuration.md) — variáveis de ambiente
- [Observabilidade](./docs/observability.md) — métricas, traces, logs
- [Testes](./docs/testing.md) — estratégia e convenções
- [CI/CD](./docs/ci-cd.md) — pipelines GitHub Actions
- [Runbook](./docs/runbook.md) — incidentes operacionais comuns
- [ADRs](./docs/adrs.md) — registros de decisão arquitetural
- [Contribuição](./docs/contributing.md) — guia para novos contribuidores

## Equipe

Desenvolvido pelo grupo do **Tech Challenge 13SOAT — FIAP**.

| Nome | RM | Discord | Email |
| --- | --- | --- | --- |
| Allef Cleiton de F. Campos | 367214 | allefcampos2179 | <allef-cleiton88@hotmail.com> |
| Leandro Borgo Battochio | 368100 | sheenbr | <lbattochio@hotmail.com> |
| Matheus Alonso Scherma | 368955 | mscherma | <matheus.scherma@gmail.com> |
| Rogério dos Santos Gelonezi | 368238 | rogergelonezi | <rogeriogelonezi@gmail.com> |

## Licença

[MIT](./LICENSE).
