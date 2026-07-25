# ADR 005: Use PostgreSQL

Status: accepted

## Context

The product needs transactions, JSON payloads, constraints, indexed history,
optimistic concurrency, and safe multi-worker outbox claiming.

## Decision

Use PostgreSQL with `timestamptz`, `jsonb`, foreign keys, checks, partial
indexes, and `FOR UPDATE SKIP LOCKED`.

## Consequences

Local setup needs Docker or PostgreSQL. Testcontainers provides the real engine
in integration tests, so provider-specific behaviour is verified rather than
mocked.
