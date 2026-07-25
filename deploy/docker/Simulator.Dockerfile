FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source

COPY global.json Directory.Build.props Directory.Packages.props Yottaverse.MachineOps.slnx ./
COPY src/Yottaverse.MachineOps.Contracts/*.csproj src/Yottaverse.MachineOps.Contracts/
COPY src/Yottaverse.MachineOps.Simulator/*.csproj src/Yottaverse.MachineOps.Simulator/
RUN dotnet restore src/Yottaverse.MachineOps.Simulator/Yottaverse.MachineOps.Simulator.csproj

COPY src/ src/
RUN dotnet publish src/Yottaverse.MachineOps.Simulator/Yottaverse.MachineOps.Simulator.csproj \
    --configuration Release \
    --no-restore \
    --output /app

FROM mcr.microsoft.com/dotnet/runtime:10.0-noble-chiseled
WORKDIR /app
COPY --from=build /app .
USER $APP_UID
EXPOSE 5099
ENTRYPOINT ["dotnet", "Yottaverse.MachineOps.Simulator.dll", "--listen", "0.0.0.0", "--port", "5099"]
