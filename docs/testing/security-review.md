# Security review

Review date: 2026-07-25

## Threat boundary

The reviewed deployment is a developer workstation using the supplied
simulator and local Compose network. There is no supported real-machine,
multi-tenant, or internet-facing deployment.

## Controls present

- API input uses data annotations and Core validation.
- SQL values use Dapper parameters.
- PostgreSQL constraints repeat important invariants.
- Correlation IDs connect request and structured log evidence.
- Alarm commands carry idempotency keys and expected versions.
- Queues are bounded and live samples are reduced.
- Diagnostic export is explicit and read-only.
- Containers run as the platform non-root application user.
- Package versions are pinned and dependency updates are monitored.

## Accepted risks

- The local API has no authentication or TLS.
- Compose credentials are public development values.
- Release executables are not code signed.
- Diagnostic ZIP files may contain sensitive job and protocol content.
- A user with local database access can alter simulator history.

## Required production work

Any networked or physical-controller use needs authentication, authorization,
TLS, secret management, rate limits, audit retention policy, hardware safety
analysis, signed packages, and an operational incident process. That work is
outside v1.0.

Final dependency, secret, and container checks are recorded in
`release-evidence.md`.
