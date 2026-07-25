# Security

## Supported version

Security fixes are made against the latest release.

## Reporting

Please report a suspected vulnerability privately to
`contact@qaisershah.co.uk`. Include the affected version, reproduction steps,
and likely impact. Do not include credentials or production data.

## Security boundary

MachineOps Workbench v1.0 supports only its supplied deterministic simulator.
It has no authentication because the Compose demonstration binds to a local
developer machine. Do not expose the API to an untrusted network.

The repository contains development-only PostgreSQL credentials. They are
intentionally limited to the local Compose network and must not be reused in
another environment. Diagnostic exports can contain job source, alarm details,
and protocol payloads. Review them before sharing.

Release executables are not code signed. Verify their SHA-256 checksums against
the release manifest.
