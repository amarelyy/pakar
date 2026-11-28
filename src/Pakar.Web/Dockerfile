# Build from the repository root:  docker build -f src/Pakar.Web/Dockerfile -t pakar-web .
# Static Blazor WebAssembly files served by Caddy, which also proxies the API on the same origin.
FROM mcr.microsoft.com/dotnet/sdk:10.0-noble AS build
WORKDIR /src
COPY Directory.Build.props global.json ./
COPY src/ src/
# Compression is done by Caddy: appsettings.json is rewritten at start, so precompressed copies would be stale.
RUN dotnet publish src/Pakar.Web -c Release -o /out -p:CompressionEnabled=false

FROM caddy:2.11-alpine
COPY --from=build /out/wwwroot /srv
COPY infra/vm/Caddyfile /etc/caddy/Caddyfile
COPY infra/vm/web-start.sh /web-start.sh
RUN sed -i 's/\r$//' /web-start.sh && chmod +x /web-start.sh
EXPOSE 80 443
ENTRYPOINT ["/web-start.sh"]
