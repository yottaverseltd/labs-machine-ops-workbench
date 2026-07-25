# Release evidence

Release evidence was regenerated from the v1.0 release candidate on
2026-07-25.

| Check | v1.0 result |
| --- | --- |
| Release build | Passed with 0 warnings and 0 errors |
| Automated tests | 50 passed, 0 failed, 0 skipped |
| PostgreSQL integration | 6 tests passed against disposable PostgreSQL containers |
| Architecture rules | 5 tests passed |
| Compose readiness | PostgreSQL, API, and simulator reported healthy |
| Simulated run | Demonstration completed and returned persisted history |
| Rendered interface | Running and completed states inspected; 2 screenshots retained |
| Windows install and remove | Installer, 10-second launch, and uninstaller passed |
| Ubuntu install and remove | Debian install, 10-second Xvfb launch, and removal passed |
| Dependency vulnerability audit | 0 known vulnerable NuGet packages |
| Container vulnerability scan | 0 fixed high or critical findings |
| Repository secret scan | Gitleaks found no leaks in files or history |
| Workflow validation | Actionlint reported no errors |
| Coverage | 55.8% lines, 51.9% branches, 85.3% Core, 66.4% Application |
| Performance smoke | 500 requests, 0 failures, 143.8 requests/second |
| Release artefacts | 4 packages, SPDX JSON SBOM, and SHA-256 manifest generated |

The CI artifacts contain Cobertura coverage files. The release contains
checksums and an SPDX JSON SBOM. Detailed local commands are in
`docs/learning-path/09-testing-strategy.md`.
