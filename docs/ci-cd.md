# CI/CD

## Workflows GitHub Actions

| Workflow | Trigger | Função |
| --- | --- | --- |
| `build-and-tests-ci.yml` | push / PR em `main` | Restore + build Release + test + cobertura ≥ 80%. |
| `format-ci.yml` | PR em `main` | `dotnet format` (whitespace + style + analyzers) + check de max line length 120. |
| `docker-ci.yml` | push / PR em `main`, tag `v*` | Build multi-arch (linux/amd64 + linux/arm64) e push para GHCR (apenas em push). |
| `k8s-trigger-cd.yml` | sucesso do `docker-ci` em `main` | Dispara `repository_dispatch` no repo de infra com o sha do commit. |
| `sonarqube-ci.yml` | `pull_request` ou `push` em `main` | Dispara análise do [Sonar Cloud](https://sonarcloud.io/summary/new_code?id=fiap-challenge-13soat_mecanica-hermes-api-ordem-servico) |

## Secrets esperados

- `K8S_REPO_PAT` — PAT com permissão de dispatch no repo de infra.
- `DOCKER_USERNAME` — Usuário Docker Hub |
- `DOCKER_PASSWORD` — Senha ou token Docker Hub |
- `SONAR_TOKEN` — Token gerado em SonarCloud → My Account → Security.

## Imagem Docker

Multi-stage:

1. **build:** `mcr.microsoft.com/dotnet/sdk:10.0` — restore → publish Release.
2. **runtime:** `mcr.microsoft.com/dotnet/aspnet:10.0` — copia `/app/publish`, roda como `app` (UID 1654, non-root), `chmod 0555`.

`ENTRYPOINT ["dotnet", "Mecanica.Hermes.Pagamento.Api.dll"]`. Porta `8080`.
