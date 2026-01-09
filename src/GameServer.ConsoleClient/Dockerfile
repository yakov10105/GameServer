FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY Directory.Build.props .
COPY Directory.Packages.props .
COPY GameServer.sln .

COPY src/GameServer.Domain/GameServer.Domain.csproj src/GameServer.Domain/
COPY src/GameServer.ConsoleClient/GameServer.ConsoleClient.csproj src/GameServer.ConsoleClient/

RUN dotnet restore src/GameServer.ConsoleClient/GameServer.ConsoleClient.csproj

COPY src/GameServer.Domain/ src/GameServer.Domain/
COPY src/GameServer.ConsoleClient/ src/GameServer.ConsoleClient/

WORKDIR /src/src/GameServer.ConsoleClient
RUN dotnet publish GameServer.ConsoleClient.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/runtime:8.0 AS final
WORKDIR /app

RUN addgroup --system --gid 1001 gameserver && \
    adduser --system --uid 1001 --ingroup gameserver gameserver

COPY --from=build /app/publish .

RUN chown -R gameserver:gameserver /app

USER gameserver

ENV GAMESERVER_URI=ws://gameserver-api:8080/ws
ENV GAMESERVER_LOG_LEVEL=Information

ENTRYPOINT ["dotnet", "GameServer.ConsoleClient.dll"]

