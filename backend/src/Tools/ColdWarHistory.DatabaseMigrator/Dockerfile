FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish src/Tools/ColdWarHistory.DatabaseMigrator/ColdWarHistory.DatabaseMigrator.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/runtime:9.0
WORKDIR /app
COPY --from=build /app .
COPY . /workspace
WORKDIR /workspace
ENTRYPOINT ["dotnet", "/app/ColdWarHistory.DatabaseMigrator.dll"]
