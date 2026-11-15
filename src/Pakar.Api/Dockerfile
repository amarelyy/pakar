# Build from the repository root:  docker build -f src/Pakar.Api/Dockerfile -t pakar-api .
# Only the projects the API depends on are copied, so a change in Pakar.Web or Pakar.AiInference does not invalidate the cache.
FROM mcr.microsoft.com/dotnet/sdk:10.0-noble AS build
WORKDIR /src
COPY Directory.Build.props global.json ./
COPY src/Pakar.Shared/ src/Pakar.Shared/
COPY src/Pakar.Domain/ src/Pakar.Domain/
COPY src/Pakar.Application/ src/Pakar.Application/
COPY src/Pakar.Infrastructure/ src/Pakar.Infrastructure/
COPY src/Pakar.Migrations.Postgres/ src/Pakar.Migrations.Postgres/
COPY src/Pakar.Api/ src/Pakar.Api/
RUN dotnet publish src/Pakar.Api -c Release -o /out

FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble
WORKDIR /app
COPY --from=build /out ./
# Container Apps and the Bicep in infra/ expect port 8080. Secrets and the database URL come from environment variables
# (Jwt__Key, Ingest__ApiKey, DATABASE_URL, ...); see README.md. Schema changes are applied by the migration bundle, not at startup.
ENV ASPNETCORE_HTTP_PORTS=8080 \
    ASPNETCORE_FORWARDEDHEADERS_ENABLED=true \
    Database__ApplyMigrationsOnStartup=false \
    Logging__LogLevel__Microsoft.EntityFrameworkCore=Warning
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "Pakar.Api.dll"]
