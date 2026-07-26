# Simulator protocol

The simulator uses UTF-8 JSON Lines over TCP. It resembles the request,
acknowledgement, and state rhythm of a small controller, but it does not claim
complete GRBL compatibility.

Every command has:

- `type`
- `correlationId`
- `protocolVersion`
- optional string `payload`

Events have a monotonic `sequence`, optional matching `correlationId`, and
optional state, error, or alarm code.

Supported commands are `hello`, `get_state`, `start`, `pause`, `resume`,
`cancel`, and `disconnect`. Supported events are `hello_accepted`, `state`,
`command_accepted`, `protocol_error`, and `alarm`.

The `start` payload contains a `ControllerRunPlanWire` with the saved job's
ordered path segments. In the normal scenario, each `get_state` advances five
percent through the total three-dimensional path length. Reported X, Y, Z and
feed values therefore follow the selected G-code program rather than a generic
simulator trajectory.

Run the normal scenario:

```shell
dotnet run --project src/Yottaverse.MachineOps.Simulator
```

Choose a deterministic fault:

```shell
dotnet run --project src/Yottaverse.MachineOps.Simulator -- --scenario OutOfOrder
```

Replay recorded states:

```shell
dotnet run --project src/Yottaverse.MachineOps.Simulator -- \
  --replay samples/replays/normal-run.jsonl
```

Each non-empty replay line is one `ControllerStateWire` JSON object. Reaching
the end holds the last state, which makes repeated test observations stable.
