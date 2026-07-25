ALTER TABLE alarms
    ADD COLUMN IF NOT EXISTS external_key varchar(160),
    ADD COLUMN IF NOT EXISTS version integer NOT NULL DEFAULT 0;

UPDATE alarms
SET external_key = id::text
WHERE external_key IS NULL;

ALTER TABLE alarms
    ALTER COLUMN external_key SET NOT NULL;

CREATE UNIQUE INDEX IF NOT EXISTS ux_alarms_machine_external_key
    ON alarms (machine_id, external_key);

ALTER TABLE alarm_acknowledgements
    ADD COLUMN IF NOT EXISTS idempotency_key uuid,
    ADD COLUMN IF NOT EXISTS alarm_version integer NOT NULL DEFAULT 1;

UPDATE alarm_acknowledgements
SET idempotency_key = id
WHERE idempotency_key IS NULL;

ALTER TABLE alarm_acknowledgements
    ALTER COLUMN idempotency_key SET NOT NULL;

CREATE UNIQUE INDEX IF NOT EXISTS ux_alarm_acknowledgements_idempotency
    ON alarm_acknowledgements (alarm_id, idempotency_key);

ALTER TABLE outbox_messages
    ADD COLUMN IF NOT EXISTS locked_until_utc timestamptz;
