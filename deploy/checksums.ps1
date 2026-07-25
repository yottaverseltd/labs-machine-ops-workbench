param([string]$ArtifactRoot = "artifacts")

$ErrorActionPreference = "Stop"
$resolvedRoot = Resolve-Path $ArtifactRoot
$checksumFile = Join-Path $resolvedRoot "SHA256SUMS"
$lines = Get-ChildItem -LiteralPath $resolvedRoot -File |
    Where-Object { $_.Name -ne "SHA256SUMS" } |
    Sort-Object Name |
    ForEach-Object {
        $hash = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
        "$hash  $($_.Name)"
    }
[System.IO.File]::WriteAllLines($checksumFile, $lines)
