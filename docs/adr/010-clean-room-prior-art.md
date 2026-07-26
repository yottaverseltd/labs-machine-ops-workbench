# ADR 010: Treat Java/Swing senders as prior art only

Status: accepted

## Context

Java/Swing desktop G-code senders established a useful workflow category long
before this project. Reusing their implementation or visual identity would
make this study less independent and could introduce incompatible licensing.

## Decision

Use Universal G-Code Sender only as acknowledgement of prior art for the broad
load, inspect, send, and monitor workflow. Treat migration as preservation of
observable behaviour and responsibilities, not mechanical translation of
`.java` files. Design the C#, Avalonia interface, protocol, tests,
documentation, and assets from first principles.

## Consequences

MachineOps is a clean-room modernisation study, not a source port or automatic
Java converter. No source code, tests, screenshots, documentation, or assets
from Universal G-Code Sender are included.
