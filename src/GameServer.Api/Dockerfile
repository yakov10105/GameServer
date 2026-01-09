FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY Directory.Build.props .
COPY Directory.Packages.props .
COPY GameServer.slnx .

COPY src/GameServer.Domain/GameServer.Domain.csproj src/GameServer.Domain/
COPY src/GameServer.Application/GameServer.Application.csproj src/GameServer.Application/
COPY src/GameServer.Infrastructure/GameServer.Infrastructure.csproj src/GameServer.Infrastructure/
COPY src/GameServer.Api/GameServer.Api.csproj src/GameServer.Api/

RUN dotnet restore src/GameServer.Api/GameServer.Api.csproj

COPY src/ src/

WORKDIR /src/src/GameServer.Api
RUN dotnet publish GameServer.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*

RUN addgroup --system --gid 1001 gameserver && \
    adduser --system --uid 1001 --ingroup gameserver gameserver

COPY --from=build /app/publish .

RUN chown -R gameserver:gameserver /app

USER gameserver

EXPOSE 8080

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

HEALTHCHECK --interval=10s --timeout=5s --start-period=15s --retries=5 \
    CMD curl -f http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "GameServer.Api.dll"]

