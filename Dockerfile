FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY Directory.Build.props .
COPY Directory.Packages.props .
COPY GameServer.sln .

COPY src/GameServer.Domain/GameServer.Domain.csproj src/GameServer.Domain/
COPY src/GameServer.Application/GameServer.Application.csproj src/GameServer.Application/
COPY src/GameServer.Infrastructure/GameServer.Infrastructure.csproj src/GameServer.Infrastructure/
COPY src/GameServer.Api/GameServer.Api.csproj src/GameServer.Api/

RUN dotnet restore src/GameServer.Api/GameServer.Api.csproj

COPY src/ src/

WORKDIR /src/src/GameServer.Api
RUN dotnet build GameServer.Api.csproj -c Release -o /app/build --no-restore

FROM build AS publish
RUN dotnet publish GameServer.Api.csproj -c Release -o /app/publish --no-restore --no-build

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

RUN groupadd -r gameserver && useradd -r -g gameserver gameserver
USER gameserver

EXPOSE 8080
EXPOSE 8443

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

COPY --from=publish /app/publish .

ENTRYPOINT ["dotnet", "GameServer.Api.dll"]

