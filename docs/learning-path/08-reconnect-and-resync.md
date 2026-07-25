# Reconnect and resynchronise

SignalR makes the screen responsive, but it is not the system of record.
MachineOps treats a hub message as a prompt to update the display, not as proof
that the client has seen every state change.

## Normal live updates

`RunMonitorService` requests a controller snapshot every 250 milliseconds while
a run is active. The controller session raises `SnapshotChanged`.
`MachineUpdateBroadcaster` writes that snapshot to a bounded channel with room
for one item. If the producer outruns the broadcaster, an older display sample
is replaced by the newest one. Run state remains persisted independently.

The broadcaster emits no more than ten updates per second. On the desktop,
`MachineLiveClient` receives the DTO and `MainViewModel` coalesces pending
updates before posting them to the Avalonia UI thread.

## Reconnection

The desktop connection uses delays of zero, two, five, and ten seconds. The
header shows `reconnecting` while those attempts are in progress. A successful
socket reconnect does not immediately mean the state is current.

`MachineLiveClient.ResynchroniseAsync` first calls the machine snapshot API.
Only after that request succeeds does it:

1. publish the authoritative snapshot to the view model;
2. change the live state to `Live`.

If all retries fail, the header shows `unavailable`. The local G-code editor and
toolpath remain usable.

This split also covers an API restart. SignalR reports the connection loss,
automatic reconnect establishes a fresh hub connection, and the HTTP snapshot
fills any sequence gap that occurred while the client was away.
