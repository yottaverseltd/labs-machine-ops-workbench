# Windows and Linux packaging

The desktop release is self-contained. A user does not need to install the
.NET runtime separately, and the package runs the same assemblies that pass
the release test suite.

## Windows

`deploy/windows/build.ps1` publishes for `win-x64` and creates a portable ZIP.
When Inno Setup is available, the same script also produces a per-user
installer. The installer writes only beneath the selected application folder,
adds optional shortcuts, and registers a normal Windows uninstaller.

The executable is not code signed. Windows can therefore display a reputation
warning for a development build. The SHA-256 checksum lets a user verify that
the file matches the GitHub release.

## Linux

`deploy/linux/build.sh` publishes for `linux-x64`, creates a portable tarball,
and lays out a Debian package beneath `/opt/machineops-workbench`. The package
adds a command symlink and a desktop entry. Removing the package removes those
files without touching PostgreSQL data.

## Services

The API and simulator are separate Linux container images. Compose supplies
their network names and keeps the desktop outside the container boundary. This
matches a realistic deployment: a desktop can reconnect while the backend is
restarted independently.

## Reproducibility

Package versions are centrally pinned. CI starts from a tagged commit, reruns
the tests, builds both platforms, generates an SPDX SBOM and calculates
checksums before creating a release. A tag is therefore a source input, not an
unverified label attached to local output.

The release jobs also install the packages they just built. Windows launches
the installed application for ten seconds before running its uninstaller. The
Linux job installs the Debian package into Ubuntu 24.04, starts Avalonia under
Xvfb, removes the package, and checks that its application directory is gone.
