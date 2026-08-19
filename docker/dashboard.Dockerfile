# IntelligenceKit Blazor WebAssembly dashboard, served as static files by nginx.
# Build context is the repository root: docker build -f docker/dashboard.Dockerfile .

# ---- build ----------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# The wasm-tools workload is only *recommended* (size optimization); a plain
# publish produces a fully working app, so we skip it to keep the build lean.
COPY src/ src/
RUN dotnet restore src/IntelligenceKit.Dashboard/IntelligenceKit.Dashboard.csproj
RUN dotnet publish src/IntelligenceKit.Dashboard/IntelligenceKit.Dashboard.csproj \
    -c Release -o /app/publish --no-restore

# ---- runtime --------------------------------------------------------------
FROM nginx:1.27-alpine AS runtime

# SPA routing + WASM MIME/caching rules.
COPY docker/nginx.conf /etc/nginx/conf.d/default.conf

# Static site (the published Blazor output lives under wwwroot).
COPY --from=build /app/publish/wwwroot /usr/share/nginx/html

# Rewrites wwwroot/appsettings.json from $API_BASE_URL before nginx starts.
# The official nginx image runs every /docker-entrypoint.d/*.sh at container
# start, so no custom ENTRYPOINT is needed.
COPY docker/dashboard-entrypoint.sh /docker-entrypoint.d/40-ik-apibaseurl.sh
RUN chmod +x /docker-entrypoint.d/40-ik-apibaseurl.sh

EXPOSE 80
