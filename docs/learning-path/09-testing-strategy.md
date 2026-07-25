# Testing strategy

The suite follows the cost and confidence of each boundary.

## Fast rule tests

Core tests exercise parsing, units, diagnostics, progress monotonicity, run
transitions, alarm idempotency, and optimistic concurrency. Application tests
use hand-written boundaries where orchestration matters. Desktop tests execute
view-model commands without opening a window.

## Boundary tests

API tests run through `WebApplicationFactory`. They verify JSON contracts,
status codes, Problem Details, OpenAPI, SignalR negotiation, and diagnostic
downloads.

Simulator tests use a real loopback TCP socket. Duplicate, malformed,
out-of-order, alarm, execution, and replay scenarios exercise the same adapter
used by the API.

## Real storage tests

Infrastructure tests create a disposable PostgreSQL container. They apply the
actual embedded migrations and execute the real Dapper statements. These tests
cover round trips, idempotency, outbox leasing, constraints, and paged history.
An in-memory substitute cannot give that assurance.

## Architecture tests

Reflection-based rules inspect project references and controller fields. They
fail when an inner layer gains an outer dependency or a controller reaches for
Dapper/Npgsql directly.

CI runs the fast tests on Windows and Linux. PostgreSQL integration tests run on
Linux with Docker. The combined report starts with a 50% line coverage floor,
which should rise as useful tests are added. Coverage is evidence about
exercised code, not a target to game with empty assertions.
