param(
    [string]$BaseAddress = "http://localhost:5080"
)

$ErrorActionPreference = "Stop"

docker compose up -d --build --wait

$ready = Invoke-RestMethod -Uri "$BaseAddress/health/ready"
if ($ready.database -ne "ready") {
    throw "The database readiness check did not pass."
}

$name = "Demonstration $(Get-Date -Format 'yyyyMMdd-HHmmss')"
$jobBody = @{
    name = $name
    gCode = "G21`nG90`nG0 X0 Y0`nG1 X20 Y0 F600`nG1 X20 Y20`nG1 X0 Y20`nG1 X0 Y0"
} | ConvertTo-Json
$job = Invoke-RestMethod `
    -Method Post `
    -Uri "$BaseAddress/api/jobs" `
    -ContentType "application/json" `
    -Body $jobBody

Invoke-RestMethod `
    -Method Post `
    -Uri "$BaseAddress/api/machines/simulator/connect" `
    -ContentType "application/json" `
    -Body '{"port":5099}' | Out-Null

$runBody = @{ jobId = $job.id } | ConvertTo-Json
$run = Invoke-RestMethod `
    -Method Post `
    -Uri "$BaseAddress/api/runs" `
    -ContentType "application/json" `
    -Body $runBody

for ($attempt = 0; $attempt -lt 40 -and $run.state -eq "Running"; $attempt++) {
    Start-Sleep -Milliseconds 300
    $run = Invoke-RestMethod -Uri "$BaseAddress/api/runs/active"
}

if ($run.state -ne "Completed") {
    throw "The simulated run ended in state '$($run.state)'."
}

$history = Invoke-RestMethod `
    -Uri "$BaseAddress/api/history?query=$([uri]::EscapeDataString($name))&skip=0&take=10"
if ($history.jobs.total -ne 1) {
    throw "The completed demonstration job was not found in history."
}

[pscustomobject]@{
    Api = "ready"
    Job = $job.id
    Segments = $job.segmentCount
    Run = $run.state
    HistoryMatches = $history.jobs.total
}
