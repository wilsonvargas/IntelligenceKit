# IntelligenceKit ingest/query backend.
# Build context is the repository root: docker build -f docker/server.Dockerfile .

# ---- build ----------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy the product source (tests/samples are excluded via .dockerignore) and
# restore + publish the Server. Publishing it transitively builds Core,
# Server.Contracts, Server.Data and all three Server.Migrations.* provider
# assemblies (referenced by the Server project), so runtime Migrate() works for
# whichever provider is configured.
COPY src/ src/
RUN dotnet restore src/IntelligenceKit.Server/IntelligenceKit.Server.csproj
RUN dotnet publish src/IntelligenceKit.Server/IntelligenceKit.Server.csproj \
    -c Release -o /app/publish --no-restore

# ---- runtime --------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish ./

# Listen on 7099 inside the container (matches the project's http profile).
ENV ASPNETCORE_URLS=http://+:7099 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_gcServer=1

EXPOSE 7099
ENTRYPOINT ["dotnet", "IntelligenceKit.Server.dll"]
