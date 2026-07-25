# Working in this repository

Keep the product simulator-only. Do not add a serial-port adapter or imply that
the current safety behaviour is suitable for physical machinery.

Before changing code, identify the owning layer:

- Core owns invariants and state transitions.
- Application owns use-case coordination and ports.
- Infrastructure owns SQL and controller transport.
- API owns HTTP mapping.
- Desktop owns presentation and client state.
- Contracts owns versioned external shapes.

Do not bypass these boundaries for convenience. Prefer a small explicit
interface over a general framework. All I/O should be asynchronous and accept a
cancellation token.

Before a commit, run:

```shell
dotnet format Yottaverse.MachineOps.slnx
dotnet build Yottaverse.MachineOps.slnx --configuration Release
dotnet test Yottaverse.MachineOps.slnx --configuration Release
```

Integration tests need a running Docker engine. Never commit package output,
database passwords for real environments, local paths, or diagnostic exports.
