# ADR 010: Treat earlier senders as prior art only

Status: accepted

## Context

Desktop G-code senders established a useful workflow category long before this
project. Reusing their implementation or visual identity would make this study
less independent and could introduce incompatible licensing.

## Decision

Use Universal G-Code Sender only as acknowledgement of prior art for the broad
load, inspect, send, and monitor workflow. Design the code, protocol, tests,
documentation, assets, and interface from first principles.

## Consequences

MachineOps is a clean-room modernisation study, not a port. No source code,
tests, screenshots, documentation, or assets from Universal G-Code Sender are
included.
