# Solution tour

MachineOps is split by reasons to change, not by technical fashion.

```text
Desktop -> Contracts -> API -> Application -> Core
                              |
                              +-> Infrastructure -> PostgreSQL
                                      |
                                      +-> TCP simulator
```

The arrows show compile-time knowledge. Core has no outward arrow. Contracts
contains only transport shapes. Desktop does not reference Infrastructure, so a
view model cannot open a database connection even by accident.

## Start in Core

Read `GCodeParser`, `JobRun`, and `MachineAlarm`. They contain the rules that
remain true whether a request came from Avalonia, HTTP, a test, or another
adapter. Notice the absence of framework attributes.

## Move to Application

`CreateJobHandler`, `RunCoordinator`, and `AlarmService` describe use cases.
They depend on small ports such as `IJobRepository` and `IControllerSession`.
Their constructors state every capability they need.

## Look outward

Infrastructure implements the ports with Dapper and a TCP session. The API
maps HTTP requests to use cases and Core results to DTOs. Desktop uses a typed
HTTP client and SignalR client, then presents immutable DTOs and local preview
data.

Architecture tests make these dependency directions executable. If Desktop
references Infrastructure or Core begins to depend on ASP.NET Core, the build
fails.
