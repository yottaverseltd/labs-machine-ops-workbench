# Controller to application

This release adds the first complete machine execution path. The simulator is
deliberately small, but it uses a real TCP connection and a versioned JSON-line
protocol. That makes connection loss, malformed data, late messages, and
duplicate messages testable without special hardware.

## Follow a connection

1. `MainViewModel.ConnectSimulatorAsync` calls the typed desktop API client.
2. `MachinesController` validates the port and calls `ConnectSimulatorHandler`.
3. The handler supplies the fixed simulator identity and loopback address.
4. `TcpControllerSession` opens the socket and sends a `hello` command.
5. `SimulatorServer` replies with `hello_accepted` and its current state.
6. The session maps that wire model to the Core `MachineSnapshot`.
7. The API maps the Core snapshot to `MachineSnapshotDto`.

The view model sees a DTO. It does not know about sockets, Dapper, or a database
connection.

## Follow a run

`RunCoordinator` is the application use case for execution. Starting a run
checks that the job exists and that the simulator is connected. It then creates
a Core `JobRun`, applies the start transition, sends the controller command,
and saves the run through `IRunRepository`.

Pause, resume, and cancel follow the same path. The Core object rejects invalid
transitions before a command reaches the transport. For example, a ready run
cannot be paused and a completed run cannot be cancelled.

The desktop polls the authoritative snapshot during this release. Each
`get_state` request moves a normal simulator run by a fixed ten percent. Version
0.5 replaces that polling loop with SignalR notifications while retaining the
snapshot endpoint for recovery.

## The protocol transcript

`TcpControllerSession` records each outbound command before writing it and each
valid inbound event after parsing it. `DapperControllerAuditStore` stores these
messages in `protocol_messages` and updates the owning controller session.
Outbound and inbound counters are separate from the simulator wire sequence.
This is important because duplicate wire responses are valid fault-test input
and must still be captured.

The TCP adapter owns transport concerns. The simulator owns its deterministic
behaviour. The Core owns allowed run transitions. Keeping those jobs separate
makes each failure easier to locate and test.
