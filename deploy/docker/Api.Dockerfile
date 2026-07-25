FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source

COPY global.json Directory.Build.props Directory.Packages.props Yottaverse.MachineOps.slnx ./
COPY src/Yottaverse.MachineOps.Core/*.csproj src/Yottaverse.MachineOps.Core/
COPY src/Yottaverse.MachineOps.Application/*.csproj src/Yottaverse.MachineOps.Application/
COPY src/Yottaverse.MachineOps.Contracts/*.csproj src/Yottaverse.MachineOps.Contracts/
COPY src/Yottaverse.MachineOps.Infrastructure/*.csproj src/Yottaverse.MachineOps.Infrastructure/
COPY src/Yottaverse.MachineOps.Api/*.csproj src/Yottaverse.MachineOps.Api/
RUN dotnet restore src/Yottaverse.MachineOps.Api/Yottaverse.MachineOps.Api.csproj

COPY src/ src/
RUN dotnet publish src/Yottaverse.MachineOps.Api/Yottaverse.MachineOps.Api.csproj \
    --configuration Release \
    --no-restore \
    --output /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled
WORKDIR /app
COPY --from=build /app .
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "Yottaverse.MachineOps.Api.dll"]
