# Dapper transaction

Alarm acknowledgement shows why the repository is more than a CRUD wrapper.

1. `AcknowledgeAlarmRequest` carries an idempotency key and expected version.
2. `AlarmsController` maps not-found, conflict, and success to HTTP.
3. `AlarmService` loads the alarm and asks Core to apply the rule.
4. `MachineAlarm.Acknowledge` rejects stale versions and conflicting repeats.
5. `DapperAlarmRepository.SaveAcknowledgementAsync` opens one pooled connection
   and begins one transaction.
6. A conditional `UPDATE` changes the alarm only when its stored version is the
   expected version.
7. An acknowledgement row is inserted with a unique idempotency key.
8. An outbox row is inserted before the transaction commits.

If any statement fails, none of the three changes become visible. A concurrent
request that loses the version race receives an `AlarmConcurrencyException`
with the actual version.

The SQL is kept as formatted text beside its mapping code. Parameters are
always passed separately. No user value is interpolated into a SQL statement.
Connections and transactions are short-lived; pooling belongs to
`NpgsqlDataSource`.
