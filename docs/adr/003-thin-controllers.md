# ADR 003: Keep domain rules out of controllers

Status: accepted

## Context

HTTP status selection is not a machining rule. Rules embedded in controllers
would be difficult to reuse and easy to bypass.

## Decision

Controllers validate transport input, call one application use case, map the
result, and choose an HTTP response. Core and Application make the decisions.

## Consequences

Use cases can run from tests or another adapter without ASP.NET Core. Controllers
remain small enough for integration tests to cover as a boundary.
