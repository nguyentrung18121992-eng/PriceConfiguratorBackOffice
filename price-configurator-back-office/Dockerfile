# Build from repo root: docker compose build back-office
# Runtime Cosmos connection: ConnectionStrings__priceconfigurator in .env (not bundled in compose).
# Build token: FEED_ACCESSTOKEN in .env (Azure DevOps PAT, Packaging read on omni-backend).

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG FEED_ACCESSTOKEN
WORKDIR /src

COPY nuget.config ./
COPY price-configurator-back-office/price-configurator-back-office.csproj price-configurator-back-office/

RUN if [ -n "$FEED_ACCESSTOKEN" ]; then \
      dotnet nuget update source omni-backend \
        --username "az" \
        --password "${FEED_ACCESSTOKEN}" \
        --store-password-in-clear-text \
        --configfile nuget.config; \
    fi

RUN dotnet restore price-configurator-back-office/price-configurator-back-office.csproj --configfile nuget.config

COPY price-configurator-back-office/ price-configurator-back-office/

RUN dotnet publish price-configurator-back-office/price-configurator-back-office.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=local

EXPOSE 8080

HEALTHCHECK --interval=15s --timeout=5s --start-period=90s --retries=8 \
  CMD curl -f http://localhost:8080/api/healthcheck || exit 1

ENTRYPOINT ["dotnet", "price-configurator-back-office.dll"]
