param(
    [string]$Version = "1.0.0",
    [string]$OutputRoot = "artifacts"
)

$ErrorActionPreference = "Stop"
$repositoryRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..")
$artifactRoot = Join-Path $repositoryRoot $OutputRoot
$publishDirectory = Join-Path $artifactRoot "machineops-win-x64"
$archivePath = Join-Path $artifactRoot "machineops-workbench-$Version-win-x64.zip"

dotnet publish (Join-Path $repositoryRoot "src\Yottaverse.MachineOps.Desktop\Yottaverse.MachineOps.Desktop.csproj") `
    --configuration Release `
    --runtime win-x64 `
    --self-contained true `
    -p:Version=$Version `
    --output $publishDirectory

Copy-Item (Join-Path $repositoryRoot "LICENSE") $publishDirectory
Copy-Item (Join-Path $repositoryRoot "README.md") $publishDirectory
if (Test-Path $archivePath) {
    Remove-Item -LiteralPath $archivePath
}

Compress-Archive -Path (Join-Path $publishDirectory "*") -DestinationPath $archivePath

$innoCompiler = (Get-Command iscc -ErrorAction SilentlyContinue).Source
if ([string]::IsNullOrWhiteSpace($innoCompiler)) {
    $userCompiler = Join-Path $env:LOCALAPPDATA "Programs\Inno Setup 6\ISCC.exe"
    if (Test-Path $userCompiler) {
        $innoCompiler = $userCompiler
    }
}

if (-not [string]::IsNullOrWhiteSpace($innoCompiler)) {
    $env:MACHINEOPS_PUBLISH_DIR = $publishDirectory
    $env:MACHINEOPS_ARTIFACT_DIR = $artifactRoot
    & $innoCompiler "/DMyAppVersion=$Version" (Join-Path $PSScriptRoot "machineops.iss")
}
else {
    Write-Host "Inno Setup was not found. The portable ZIP is ready; CI builds the installer."
}
