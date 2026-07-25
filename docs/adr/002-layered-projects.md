# ADR 002: Use layered projects

Status: accepted

## Context

Desktop, HTTP, machine protocol, storage, and domain rules change for different
reasons. Mixing them would make a simulator test require a window or database.

## Decision

Put immutable external shapes in Contracts, business rules in Core, use cases
and ports in Application, adapters in Infrastructure, HTTP in API, and
presentation in Desktop.

## Consequences

Dependencies point inward and architecture tests can enforce them. Some mapping
is explicit and repetitive, which is accepted in return for visible boundaries.
