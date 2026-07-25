#!/usr/bin/env bash
set -euo pipefail

version="${1:-1.0.0}"
repository_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
artifact_root="${repository_root}/artifacts"
publish_dir="${artifact_root}/machineops-linux-x64"
package_root="${artifact_root}/deb-root"

dotnet publish \
  "${repository_root}/src/Yottaverse.MachineOps.Desktop/Yottaverse.MachineOps.Desktop.csproj" \
  --configuration Release \
  --runtime linux-x64 \
  --self-contained true \
  -p:Version="${version}" \
  --output "${publish_dir}"

cp "${repository_root}/LICENSE" "${publish_dir}/"
cp "${repository_root}/README.md" "${publish_dir}/"
tar -C "${publish_dir}" -czf \
  "${artifact_root}/machineops-workbench-${version}-linux-x64.tar.gz" .

rm -rf "${package_root}"
mkdir -p \
  "${package_root}/DEBIAN" \
  "${package_root}/opt/machineops-workbench" \
  "${package_root}/usr/bin" \
  "${package_root}/usr/share/applications"
cp -a "${publish_dir}/." "${package_root}/opt/machineops-workbench/"
ln -s /opt/machineops-workbench/Yottaverse.MachineOps.Desktop \
  "${package_root}/usr/bin/machineops-workbench"
sed "s/@VERSION@/${version}/g" \
  "${repository_root}/deploy/linux/control" > "${package_root}/DEBIAN/control"
cp "${repository_root}/deploy/linux/machineops-workbench.desktop" \
  "${package_root}/usr/share/applications/"
dpkg-deb --root-owner-group --build \
  "${package_root}" \
  "${artifact_root}/machineops-workbench-${version}-linux-x64.deb"
