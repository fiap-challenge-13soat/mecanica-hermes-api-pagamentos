# syntax=docker/dockerfile:1

# ─── Build stage ──────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# GitHub Packages credentials — usadas pelo nuget.config para baixar
# Mecanica.Hermes.Shared.* da org fiap-challenge-13soat.
# GITHUB_USERNAME chega como build-arg (não é segredo).
# GITHUB_TOKEN chega via BuildKit secret (id=github_token) para não
# ser gravado em nenhuma camada da imagem nem no cache do buildx.
ARG GITHUB_USERNAME

# Copy solution-level config first — these change rarely so the restore layer
# stays cached across code-only changes.
COPY Mecanica.Hermes.Pagamento.sln           .
COPY Directory.Build.props                   .
COPY Directory.Packages.props                .
COPY nuget.config                            .

# Copy only the *.csproj files needed for the runtime image (no test projects).
# Docker caches the restore layer as long as these files don't change.
COPY src/Mecanica.Hermes.Pagamento.Domain/Mecanica.Hermes.Pagamento.Domain.csproj                 src/Mecanica.Hermes.Pagamento.Domain/
COPY src/Mecanica.Hermes.Pagamento.Application/Mecanica.Hermes.Pagamento.Application.csproj       src/Mecanica.Hermes.Pagamento.Application/
COPY src/Mecanica.Hermes.Pagamento.Infrastructure/Mecanica.Hermes.Pagamento.Infrastructure.csproj src/Mecanica.Hermes.Pagamento.Infrastructure/
COPY src/Mecanica.Hermes.Pagamento.Api/Mecanica.Hermes.Pagamento.Api.csproj                       src/Mecanica.Hermes.Pagamento.Api/

# --mount=type=secret expõe o token apenas durante este RUN — não persiste em camadas.
RUN --mount=type=secret,id=github_token \
    GITHUB_USERNAME=${GITHUB_USERNAME} \
    GITHUB_TOKEN=$(cat /run/secrets/github_token) \
    dotnet restore src/Mecanica.Hermes.Pagamento.Api/Mecanica.Hermes.Pagamento.Api.csproj

# Copy the full source and publish a framework-dependent release output.
COPY src/ src/
RUN dotnet publish src/Mecanica.Hermes.Pagamento.Api/Mecanica.Hermes.Pagamento.Api.csproj \
      --configuration Release \
      --no-restore \
      --output /app/publish

# ─── Runtime stage ────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

RUN apt-get update \
    && apt-get install -y --no-install-recommends libgssapi-krb5-2 \
    && rm -rf /var/lib/apt/lists/*

# Usuário não-root — a imagem aspnet:10.0 já traz o usuário `app` (UID 1654)
# pré-criado, atendendo least-privilege e políticas Kubernetes runAsNonRoot:true.
# --chmod=0555 (r-x): artefatos publicados só precisam de leitura/execução em
# runtime; remover o bit de escrita endurece o filesystem da imagem.
COPY --from=build --chown=app:app --chmod=0555 /app/publish .

USER app

# The aspnet:10.0 base image already sets ASPNETCORE_HTTP_PORTS=8080, but
# we make it explicit here so the intent is visible in the image config.
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "Mecanica.Hermes.Pagamento.Api.dll"]
