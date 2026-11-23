# syntax=docker/dockerfile:1
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080 8081 8443
USER $APP_UID

FROM node:24-bookworm-slim AS frontend
WORKDIR /web
COPY disputedesk-web/package.json disputedesk-web/package-lock.json ./
RUN npm ci
COPY disputedesk-web/ ./
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY DisputeDesk.Api/DisputeDesk.Api.csproj DisputeDesk.Api/
COPY DisputeDesk.Application/DisputeDesk.Application.csproj DisputeDesk.Application/
COPY DisputeDesk.Domain/DisputeDesk.Domain.csproj DisputeDesk.Domain/
COPY DisputeDesk.Infrastructure/DisputeDesk.Infrastructure.csproj DisputeDesk.Infrastructure/
RUN dotnet restore DisputeDesk.Api/DisputeDesk.Api.csproj
COPY DisputeDesk.Api/ DisputeDesk.Api/
COPY DisputeDesk.Application/ DisputeDesk.Application/
COPY DisputeDesk.Domain/ DisputeDesk.Domain/
COPY DisputeDesk.Infrastructure/ DisputeDesk.Infrastructure/
RUN dotnet publish DisputeDesk.Api/DisputeDesk.Api.csproj -c Release --no-restore -o /app /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS setup
WORKDIR /app
COPY --from=build /app/ ./
ENTRYPOINT ["dotnet", "DisputeDesk.Api.dll"]

FROM base AS final
USER root
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*
USER $APP_UID
ENV DISPUTEDESK_HEALTH_URL=http://127.0.0.1:8080/health/ready
HEALTHCHECK --interval=30s --timeout=5s --start-period=15s --retries=3 \
    CMD test "$(curl --insecure --silent --show-error --max-time 4 --output /dev/null --write-out '%{http_code}' "$DISPUTEDESK_HEALTH_URL")" = 200
COPY --from=build /app/ ./
COPY --from=frontend /web/dist/disputedesk-web/browser/ ./wwwroot/
ENTRYPOINT ["dotnet", "DisputeDesk.Api.dll"]
