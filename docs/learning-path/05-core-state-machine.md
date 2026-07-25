# Core state machines

The Core project decides which run and alarm changes are legal. Neither the API
controller nor the TCP adapter is allowed to repair an invalid transition.

## Job runs

A `JobRun` starts in `Ready`. Its normal path is:

```text
Ready -> Running -> Paused -> Running -> Completed
                    |           |
                    +-> Cancelled
```

Completion comes from observed controller progress rather than a desktop
command. Acknowledgement indexes only move forward. An older or duplicated
sample is ignored, so network reordering cannot move a run backwards.

The public methods tell the story directly: `Start`, `Pause`, `Resume`,
`Cancel`, `ObserveProgress`, and `Fail`. Each method checks its own invariant.
There is no general-purpose state setter.

## Alarms

`MachineAlarm.Acknowledge` checks three things:

1. repeating the same idempotency key returns the first result;
2. an acknowledged alarm cannot be acknowledged under a different key;
3. the supplied version must equal the current version.

The version changes from zero to one when the acknowledgement succeeds. The
Dapper repository repeats that version check in its SQL update. The Core gives
the caller an immediate and readable rule violation. PostgreSQL protects the
same rule if two API requests race.
