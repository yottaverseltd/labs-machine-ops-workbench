# ADR 009: Package with native open-source tools

Status: accepted

## Context

Users need installable desktop builds without buying a packaging service.

## Decision

Publish self-contained x64 applications. Use Inno Setup for a per-user Windows
installer, ZIP and tar archives for portable use, and `dpkg-deb` for
Debian/Ubuntu. Publish containers for the backend services.

## Consequences

No separate .NET runtime is required. Executables remain unsigned development
builds, so release checksums and clear warning text are required.
