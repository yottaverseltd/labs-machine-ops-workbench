# Demonstration

The demonstration uses no physical controller.

1. Start the services with `docker compose up -d --build --wait`.
2. Install or extract the desktop package.
3. Launch MachineOps Workbench. The sample pocket is already loaded.
4. Inspect the source, validation panel, and XY toolpath.
5. Select **Save job**.
6. Select **Connect**, then **Start saved job**.
7. Watch state, XYZ position, feed, spindle, and progress update.
8. Open **Activity** and select **Refresh**.
9. Search for `state` to isolate persisted protocol samples.
10. Stop the API container and observe the live status leave `live`.
11. Start it again and watch the client reconnect and fetch a fresh snapshot.
12. Download `/api/diagnostics/export` to inspect the ZIP evidence bundle.

For a terminal-only acceptance check:

```powershell
./scripts/demo.ps1
```

The script starts Compose, saves a sample through HTTP, connects the simulator,
runs it to completion, and confirms that persisted history can find the job.
