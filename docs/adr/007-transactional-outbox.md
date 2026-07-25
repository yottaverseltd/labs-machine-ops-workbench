# ADR 007: Publish durable events through an outbox

Status: accepted

## Context

An alarm change and its notification cannot be committed atomically across
PostgreSQL and SignalR.

## Decision

Write the domain change and an outbox message in one database transaction. A
hosted worker leases committed rows, publishes the DTO, and records completion.

## Consequences

A process failure cannot silently lose a committed event. Delivery is at least
once, so event consumers and acknowledgement commands remain idempotent.
