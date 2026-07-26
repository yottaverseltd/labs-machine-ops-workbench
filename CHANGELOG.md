# Changelog

All notable changes are recorded here. Versions follow semantic versioning.

## Unreleased

- Added live toolhead movement and completed-path rendering to the Avalonia
  toolpath view.
- Sent saved job geometry to the simulator so reported positions follow the
  selected G-code program.
- Added live feed and spindle values and state-aware desktop commands.
- Added a Visual Studio multi-project launch profile and aligned local ports.
- Removed avoidable local container startup warnings.

## 1.0.0 - 2026-07-25

- Added paged search across jobs, runs, alarms, and protocol messages.
- Added a virtualised activity and protocol view to the desktop.
- Added deterministic simulator playback from JSON Lines replay files.
- Added system-aware light and dark design tokens.
- Added desktop view-model tests and full release documentation.
- Propagated desktop command cancellation through HTTP and run polling.
- Kept failed controller transitions consistent when a command is rejected.
- Added reproducible verification, coverage, performance, security, and
  deployment records.

## 0.7.0 - 2026-07-25

- Added self-contained Windows and Linux builds.
- Added an Inno Setup installer, portable archives, and a Debian package.
- Added API and simulator container images with a complete Compose stack.
- Added release checksums, SBOM generation, and GitHub release automation.
- Verified install, launch, and removal on Windows and Ubuntu.

## 0.6.0 - 2026-07-25

- Added durable alarms, idempotent acknowledgement, optimistic concurrency,
  transactional outbox delivery, structured logging, health checks, and ZIP
  diagnostic export.

## 0.5.0 - 2026-07-25

- Added reduced SignalR live state, automatic reconnect, authoritative snapshot
  recovery, and bounded UI update cadence.

## 0.4.0 - 2026-07-25

- Added the deterministic TCP simulator, controller session, run state
  transitions, command sequencing, and persisted protocol audit.

## 0.3.0 - 2026-07-25

- Added PostgreSQL migrations and explicit Dapper persistence.

## 0.2.0 - 2026-07-25

- Added the controller-based Web API, external contracts, typed desktop client,
  OpenAPI, Problem Details, and API integration tests.

## 0.1.0 - 2026-07-25

- Added local G-code import, validation, source-line diagnostics, and XY
  toolpath preview.
