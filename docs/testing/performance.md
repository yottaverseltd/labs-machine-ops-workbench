# Performance smoke test

The release uses a bounded live-state path:

- controller samples are requested at 250 ms while a run is active;
- the server broadcaster holds only the newest pending display sample;
- SignalR emits at no more than ten updates per second;
- the desktop coalesces pending snapshots before using the UI thread;
- all unbounded history endpoints require `skip` and a maximum `take` of 100.

The repeatable HTTP smoke test is:

```powershell
./scripts/load-smoke.ps1 -Requests 500 -Concurrency 20
```

Measured on 2026-07-25 against the three-container Docker Desktop stack on the
development Windows workstation:

| Measure | Result |
| --- | ---: |
| Requests | 500 |
| Concurrency | 20 |
| Failed responses | 0 |
| Elapsed | 3.48 seconds |
| Throughput | 143.8 requests/second |
| Median batch latency | 101.2 ms |
| 95th percentile batch latency | 280.8 ms |

The script alternates the lightweight health endpoint with a paged PostgreSQL
history query. Latency is measured per concurrent batch, so it is deliberately
conservative rather than a precise per-request benchmark. This is a workstation
smoke test, not a capacity promise.
