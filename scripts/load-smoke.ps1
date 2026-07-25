param(
    [ValidateRange(1, 100000)]
    [int]$Requests = 500,
    [ValidateRange(1, 200)]
    [int]$Concurrency = 20,
    [string]$BaseAddress = "http://localhost:5080"
)

$ErrorActionPreference = "Stop"
$client = [System.Net.Http.HttpClient]::new()
$client.BaseAddress = [Uri]$BaseAddress
$client.Timeout = [TimeSpan]::FromSeconds(10)
$latencies = [System.Collections.Concurrent.ConcurrentBag[double]]::new()
$failures = [System.Collections.Concurrent.ConcurrentBag[string]]::new()
$clock = [System.Diagnostics.Stopwatch]::StartNew()

for ($offset = 0; $offset -lt $Requests; $offset += $Concurrency) {
    $batchSize = [Math]::Min($Concurrency, $Requests - $offset)
    $batchClock = [System.Diagnostics.Stopwatch]::StartNew()
    $tasks = @()
    for ($item = 0; $item -lt $batchSize; $item++) {
        $index = $offset + $item
        $path = if ($index % 2 -eq 0) {
            "/health"
        }
        else {
            "/api/history?skip=0&take=25"
        }
        $tasks += $client.GetAsync($path)
    }

    try {
        [System.Threading.Tasks.Task]::WhenAll(
            [System.Threading.Tasks.Task[]]$tasks).GetAwaiter().GetResult()
    }
    catch {
        $failures.Add("Batch $offset`: $($_.Exception.Message)")
    }
    $batchClock.Stop()

    foreach ($task in $tasks) {
        $latencies.Add($batchClock.Elapsed.TotalMilliseconds)
        if ($task.Status -eq [System.Threading.Tasks.TaskStatus]::RanToCompletion) {
            $response = $task.Result
            if (-not $response.IsSuccessStatusCode) {
                $failures.Add("$($response.RequestMessage.RequestUri): $($response.StatusCode)")
            }
            $response.Dispose()
        }
        else {
            $failures.Add("A request ended in state $($task.Status).")
        }
    }
}

$clock.Stop()
$ordered = $latencies.ToArray() | Sort-Object
$p95Index = [Math]::Min(
    $ordered.Length - 1,
    [Math]::Ceiling($ordered.Length * 0.95) - 1)
$result = [pscustomobject]@{
    Requests = $Requests
    Concurrency = $Concurrency
    Failures = $failures.Count
    DurationSeconds = [Math]::Round($clock.Elapsed.TotalSeconds, 2)
    RequestsPerSecond = [Math]::Round($Requests / $clock.Elapsed.TotalSeconds, 1)
    MedianMilliseconds = [Math]::Round($ordered[[int]($ordered.Length / 2)], 1)
    P95Milliseconds = [Math]::Round($ordered[$p95Index], 1)
}

$client.Dispose()
$result
if ($failures.Count -gt 0) {
    exit 1
}
