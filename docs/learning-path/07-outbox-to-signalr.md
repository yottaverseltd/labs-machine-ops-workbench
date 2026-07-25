# Outbox to SignalR

An alarm acknowledgement changes durable state and also needs to notify open
desktops. Sending to SignalR inside the database transaction would couple two
systems with different failure behaviour. A committed acknowledgement could be
lost if the process stopped between the commit and the hub send.

`DapperAlarmRepository` writes the alarm change and an `outbox_messages` row in
the same PostgreSQL transaction. That is the durable hand-off point.

`OutboxPublisherService` then:

1. claims a small ordered batch using `FOR UPDATE SKIP LOCKED`;
2. leases each row for 30 seconds;
3. deserialises the versioned notification DTO;
4. sends the matching SignalR event;
5. marks the row as processed.

A failed item records the error and releases its lease for a later attempt.
Multiple API instances can poll safely because a claimed row is locked and then
leased. SignalR remains a notification channel. The alarms API remains the
authoritative query and the outbox remains the delivery record.

The same pattern handles alarm creation and acknowledgement. It can be extended
to run events without changing the desktop connection design.
