# ADR 008: Support the simulator first

Status: accepted

## Context

Physical machines introduce vendor variation, motion hazards, serial-driver
behaviour, and safety certification questions that a reference workbench cannot
honestly solve.

## Decision

Support only the supplied deterministic TCP simulator in v1.0. Name every
controller surface accordingly and reject any claim of general GRBL
compatibility.

## Consequences

The complete demonstration is safe and reproducible without hardware. A future
hardware adapter requires a separate protocol, safety, and threat design.
