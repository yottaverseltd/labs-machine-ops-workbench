# ADR 006: SignalR is not authoritative

Status: accepted

## Context

Live connections can drop, reorder notifications, or reconnect after state has
changed.

## Decision

Use SignalR for low-latency notification only. After reconnect, fetch the
current versioned snapshot through HTTP before reporting the feed as live.

## Consequences

The UI can show live, reconnecting, stale, or unavailable honestly. The API
snapshot must remain complete enough to recover without replaying every event.
