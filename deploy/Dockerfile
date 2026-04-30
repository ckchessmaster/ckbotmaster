# syntax=docker/dockerfile:1.7
ARG DOTNET_VERSION=10.0

FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION} AS build
WORKDIR /src

# Copy solution + project files first for layer-cached restore.
COPY Directory.Build.props ./
COPY CkBotMaster.slnx ./
COPY src/CkBotMaster.AuditBot/CkBotMaster.AuditBot.csproj src/CkBotMaster.AuditBot/
COPY src/CkBotMaster.ServiceDefaults/CkBotMaster.ServiceDefaults.csproj src/CkBotMaster.ServiceDefaults/

RUN dotnet restore src/CkBotMaster.AuditBot/CkBotMaster.AuditBot.csproj

# Copy source.
COPY src/CkBotMaster.AuditBot/ src/CkBotMaster.AuditBot/
COPY src/CkBotMaster.ServiceDefaults/ src/CkBotMaster.ServiceDefaults/

RUN dotnet publish src/CkBotMaster.AuditBot/CkBotMaster.AuditBot.csproj \
    -c Release \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_VERSION} AS runtime
WORKDIR /app

# Run as non-root.
RUN groupadd --system --gid 1000 app \
 && useradd --system --uid 1000 --gid app --no-create-home --shell /usr/sbin/nologin app

COPY --from=build /app/publish ./

ENV DOTNET_RUNNING_IN_CONTAINER=true \
    DOTNET_USE_POLLING_FILE_WATCHER=false \
    ASPNETCORE_HTTP_PORTS=8080

USER app
EXPOSE 8080
ENTRYPOINT ["dotnet", "CkBotMaster.AuditBot.dll"]
