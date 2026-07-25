# Installation

## Backend demonstration

Docker Desktop or Docker Engine with Compose is required.

```shell
docker compose up -d --build --wait
```

This starts PostgreSQL, the API at `http://localhost:5080`, and the simulator on
the private Compose network. `GET /health/ready` confirms the database is
reachable and migrations have completed.

## Windows desktop

Use the `win-x64-setup.exe` release asset for a normal per-user installation.
The optional desktop shortcut and the Start menu entry launch the same
self-contained application. Remove it through Windows Installed Apps.

The portable ZIP can be extracted anywhere writable and run without
installation. These development releases are unsigned, so Windows can show a
reputation warning. Check the file against `SHA256SUMS` before proceeding.

## Debian and Ubuntu desktop

```shell
sudo apt install ./machineops-workbench-1.0.0-linux-x64.deb
machineops-workbench
```

Remove it with:

```shell
sudo apt remove machineops-workbench
```

For portable use, extract the Linux tarball and run
`Yottaverse.MachineOps.Desktop`. The package declares the X11 and font
libraries required by Avalonia. A graphical session is required.

## Source build

Install the .NET 10 SDK and Docker:

```shell
dotnet restore Yottaverse.MachineOps.slnx
docker compose up -d postgres
dotnet run --project src/Yottaverse.MachineOps.Api
dotnet run --project src/Yottaverse.MachineOps.Simulator
dotnet run --project src/Yottaverse.MachineOps.Desktop
```
