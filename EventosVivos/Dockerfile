# ─── Stage 1: Build Angular SPA ──────────────────────────────────────────────
# Use Debian-based Node (not Alpine) — Angular's build toolchain (esbuild,
# lightningcss, rollup) ships glibc binaries; Alpine uses musl and may fail.
FROM node:22-slim AS frontend-build
WORKDIR /app

COPY frontend/package*.json ./
RUN npm ci --legacy-peer-deps

COPY frontend/ ./
RUN npm run build

# ─── Stage 2: Build and publish .NET API ──────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS backend-build
WORKDIR /src

COPY src/ ./src/
RUN dotnet publish src/EventosVivos.API/EventosVivos.API.csproj \
      --configuration Release \
      --output /publish \
      --no-self-contained \
      /p:UseAppHost=false

# ─── Stage 3: Final runtime image ─────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

COPY --from=backend-build /publish .
COPY --from=frontend-build /app/dist/frontend/browser ./wwwroot

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

# ConnectionStrings__DefaultConnection must be injected at runtime via the
# orchestration layer (Azure App Service env vars, docker-compose .env, etc.)
# Development uses SQLite; Staging/Production use Azure PostgreSQL.

ENTRYPOINT ["dotnet", "EventosVivos.API.dll"]
