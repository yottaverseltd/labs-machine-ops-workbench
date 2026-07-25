# MachineOps Workbench

MachineOps Workbench is a cross-platform desktop workbench for inspecting and
running CNC jobs against a deterministic simulator. It is built as a practical
reference for layered .NET desktop systems where the UI, application rules,
transport, and persistence stay independently testable.

The workbench opens a G-code file, validates supported commands, and renders an
XY toolpath without requiring a controller. When the API is running, the same
program can be saved through a versioned HTTP contract.

## Current capability

- Open `.nc`, `.ngc`, `.gcode`, and `.tap` files
- Parse absolute and relative G0 and G1 moves
- Convert inch programs to millimetres
- Report unsupported arc motion and invalid feed rates
- Preview rapid and cutting moves on a responsive XY surface
- Save validated jobs through an ASP.NET Core API
- Persist jobs in PostgreSQL through parameterised Dapper queries
- Apply ordered, transactional SQL migrations on API startup
- Inspect the generated OpenAPI document at `/openapi/v1.json`
- Run entirely offline on Windows, Linux, and macOS

## Run it

Install the .NET 10 SDK and Docker Desktop. Start PostgreSQL:

```shell
docker compose up -d postgres
```

Start the API:

```shell
dotnet restore
dotnet run --project src/Yottaverse.MachineOps.Api
```

Then start the desktop app in another terminal:

```shell
dotnet run --project src/Yottaverse.MachineOps.Desktop
```

The app opens with `samples/gcode/simple-pocket.ngc` already loaded. Choose
**Save job** to send it to the API. Local import and preview still work while
the API is stopped.

## Verify it

```shell
dotnet test
```

## Release path

| Version | Working capability |
| --- | --- |
| 0.1 | Local G-code import, validation, and XY preview |
| 0.2 | HTTP API and contract boundary |
| 0.3 | PostgreSQL persistence through Dapper |
| 0.4 | TCP simulator and session control |
| 0.5 | Start, pause, resume, and cancel |
| 0.6 | SignalR live updates and snapshot recovery |
| 0.7 | Alarms and operator acknowledgement |
| 0.8 | History, diagnostics, and replay |
| 0.9 | Automated quality, security, and packaging |
| 1.0 | Cross-platform release |

## Project principles

- Core rules do not depend on UI, transport, or storage.
- Desktop code communicates through defined service boundaries.
- Live events are notifications, not authoritative state.
- Simulator faults are deterministic and reproducible.
- Every version remains runnable and tested.

## Licence

MIT
