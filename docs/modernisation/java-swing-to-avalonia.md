# From Java/Swing workflow to C#/Avalonia

MachineOps is a practical modernisation study. It shows how a desktop workflow
commonly found in Java/Swing G-code senders can be rebuilt as a maintainable,
cross-platform .NET product.

It is not an automatic Java converter and it does not contain translated
Universal G-Code Sender source. The useful engineering question is not "How do
we rewrite each `.java` file in C#?" It is "Which observable behaviours and
operational responsibilities must survive, and where should they live in the
new system?"

## Why line-by-line conversion is the wrong target

A mechanical conversion tends to preserve the structure that made the legacy
application hard to change:

- Swing components and event listeners continue to coordinate business work.
- Event Dispatch Thread assumptions leak into services and protocol handling.
- Application state remains shared inside a single process.
- Device communication is difficult to test without hardware.
- Transient UI messages are treated as operational history.
- Platform behaviour is not checked until packaging.

Avalonia controls are not replacements for Swing classes in a one-to-one class
map. The migration boundary should be behaviour, not syntax.

## Responsibility map

| Legacy responsibility | MachineOps implementation | Evidence |
| --- | --- | --- |
| Open and inspect a G-code file | Avalonia file picker and view model call the Core parser | `GCodeFilePicker.cs`, `MainViewModel.cs`, `GCodeParser.cs` |
| Validate a program | Core returns parsed commands and line-specific findings without a UI dependency | `Yottaverse.MachineOps.Core/GCode` |
| Start, pause, resume, or cancel work | Application use cases coordinate the Core run state machine | `Yottaverse.MachineOps.Application/Runs` |
| Expose work to other processes | Controller-based ASP.NET Core API uses versioned Contracts DTOs | `JobsController.cs`, `Yottaverse.MachineOps.Contracts` |
| Persist jobs and operational evidence | Infrastructure uses explicit Dapper SQL and PostgreSQL transactions | `DapperJobRepository.cs`, `Database/Migrations` |
| Notify the desktop of changes | SignalR carries reduced notifications; HTTP restores authoritative state | `Yottaverse.MachineOps.Api/Live`, desktop live client |
| Exercise device behaviour | Deterministic JSON Lines TCP simulator provides repeatable faults and replay | `Yottaverse.MachineOps.Simulator` |
| Ship cross-platform | CI creates and smoke-tests Windows x64 and Linux x64 packages | `.github/workflows/release.yml`, `deploy/windows`, `deploy/linux` |

## Incremental migration path

The repository tags also model a migration sequence in which every stage is a
working product.

1. `v0.1` proves that the parser, validation rules, and Avalonia toolpath
   preview work locally without a server.
2. `v0.2` moves the application boundary behind versioned HTTP contracts and a
   typed desktop client.
3. `v0.3` adds PostgreSQL and Dapper without allowing persistence concerns into
   Core or Desktop.
4. `v0.4` replaces hardware dependency with a deterministic TCP simulator and
   an explicit execution state machine.
5. `v0.5` adds SignalR notifications, reconnect, and snapshot
   resynchronisation.
6. `v0.6` adds alarms, idempotency, an outbox, health checks, structured logs,
   and diagnostics.
7. `v0.7` proves self-contained Windows and Linux delivery.
8. `v1.0` hardens search, replay, documentation, security evidence, and the
   demonstration path.

This sequence supports a strangler-style migration. A team can first preserve
one coherent workflow, then move business rules and integrations across
controlled boundaries instead of attempting a high-risk rewrite.

## Example vertical slice

The job-import flow is a useful example of how a Swing responsibility can be
decomposed.

1. The Avalonia file picker reads the selected local file.
2. `MainViewModel` parses it for immediate offline feedback.
3. The typed API client sends a request DTO when the user saves the job.
4. `JobsController` handles HTTP validation and status selection.
5. `CreateJobHandler` coordinates the use case.
6. `GCodeParser` applies the domain parsing and validation rules.
7. `DapperJobRepository` stores the job and commands transactionally.
8. The response DTO returns stable external data to the desktop.

The view remains replaceable, the parser remains testable without Avalonia,
and the database remains invisible to the client.

## Windows and Linux parity gates

Cross-platform support is demonstrated by release automation, not only by
choosing Avalonia:

- Windows publishes a self-contained `win-x64` application, portable ZIP, and
  per-user installer.
- Linux publishes a self-contained `linux-x64` application, portable tarball,
  and Debian/Ubuntu package.
- CI installs and starts each native package before removing it.
- The Linux smoke test launches Avalonia under Xvfb on Ubuntu 24.04.
- The API and simulator are also built as Linux container images.
- Release output includes SHA-256 checksums and an SPDX SBOM.

These gates catch file-system, executable-name, packaging, and runtime
assumptions that a Windows-only development loop can miss.

## Behavioural parity checklist

For a real Java/Swing migration, acceptance should be written before
replacement:

- Record the supported workflows and their observable inputs and outputs.
- Capture representative files and protocol traces that are legally available.
- Add characterisation tests around rules that must not change.
- Classify UI behaviour, domain decisions, persistence, and transport
  responsibilities.
- Port domain behaviour into framework-independent C# tests first.
- Replace Swing event listeners with commands and explicit use cases.
- Keep real-time notifications separate from authoritative state.
- Run the same acceptance scenarios on Windows and Linux.
- Compare diagnostics, failure behaviour, and recovery, not only the happy
  path.
- Retire legacy slices only after the new path passes agreed parity gates.

## Clean-room boundary

[Universal G-Code Sender](https://github.com/winder/Universal-G-Code-Sender)
is acknowledged only as prior art for the broad load, inspect, send, and
monitor workflow. MachineOps source, interface, protocol, tests, diagrams,
screenshots, and documentation were created independently. This keeps the
repository useful as an architecture and migration exercise without presenting
copied or mechanically translated code as original work.
