# ADR 004: Use Dapper and visible SQL

Status: accepted

## Context

Operational history and outbox leasing need precise PostgreSQL queries. The
schema is small and the team should be able to study every statement.

## Decision

Use Dapper with parameterised SQL, short-lived pooled Npgsql connections, and
explicit transactions. Do not add a generic repository.

## Consequences

Queries and indexes can be reviewed together, and mapping behaviour is tested
against PostgreSQL. Schema changes require deliberate SQL migrations.
