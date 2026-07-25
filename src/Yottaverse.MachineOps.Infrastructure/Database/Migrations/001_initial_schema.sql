CREATE TABLE IF NOT EXISTS machines
(
    id uuid PRIMARY KEY,
    name varchar(120) NOT NULL,
    host varchar(255) NOT NULL,
    port integer NOT NULL CHECK (port BETWEEN 1 AND 65535),
    scenario varchar(50) NOT NULL,
    created_at_utc timestamptz NOT NULL
);

CREATE TABLE IF NOT EXISTS jobs
(
    id uuid PRIMARY KEY,
    name varchar(120) NOT NULL,
    gcode text NOT NULL,
    state varchar(30) NOT NULL,
    created_at_utc timestamptz NOT NULL,
    segment_count integer NOT NULL CHECK (segment_count >= 0),
    travel_distance double precision NOT NULL CHECK (travel_distance >= 0),
    minimum_x double precision NOT NULL,
    minimum_y double precision NOT NULL,
    maximum_x double precision NOT NULL,
    maximum_y double precision NOT NULL,
    toolpath jsonb NOT NULL,
    diagnostics jsonb NOT NULL
);

CREATE TABLE IF NOT EXISTS job_commands
(
    id uuid PRIMARY KEY,
    job_id uuid NOT NULL REFERENCES jobs(id) ON DELETE CASCADE,
    command_type varchar(30) NOT NULL,
    correlation_id uuid NOT NULL,
    requested_at_utc timestamptz NOT NULL,
    accepted_at_utc timestamptz,
    details jsonb NOT NULL DEFAULT '{}'::jsonb
);

CREATE TABLE IF NOT EXISTS job_runs
(
    id uuid PRIMARY KEY,
    job_id uuid NOT NULL REFERENCES jobs(id) ON DELETE RESTRICT,
    machine_id uuid REFERENCES machines(id) ON DELETE RESTRICT,
    state varchar(30) NOT NULL,
    started_at_utc timestamptz,
    finished_at_utc timestamptz,
    last_command_index integer NOT NULL DEFAULT 0,
    failure_reason text
);

CREATE TABLE IF NOT EXISTS controller_sessions
(
    id uuid PRIMARY KEY,
    machine_id uuid NOT NULL REFERENCES machines(id) ON DELETE CASCADE,
    connected_at_utc timestamptz NOT NULL,
    disconnected_at_utc timestamptz,
    disconnect_reason text,
    last_sequence bigint NOT NULL DEFAULT 0
);

CREATE TABLE IF NOT EXISTS machine_samples
(
    id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    machine_id uuid NOT NULL REFERENCES machines(id) ON DELETE CASCADE,
    job_run_id uuid REFERENCES job_runs(id) ON DELETE SET NULL,
    sequence bigint NOT NULL,
    recorded_at_utc timestamptz NOT NULL,
    position_x double precision NOT NULL,
    position_y double precision NOT NULL,
    position_z double precision NOT NULL,
    feed_rate double precision,
    spindle_speed double precision,
    machine_state varchar(30) NOT NULL,
    progress double precision CHECK (progress BETWEEN 0 AND 100),
    UNIQUE (machine_id, sequence)
);

CREATE TABLE IF NOT EXISTS alarms
(
    id uuid PRIMARY KEY,
    machine_id uuid NOT NULL REFERENCES machines(id) ON DELETE CASCADE,
    job_run_id uuid REFERENCES job_runs(id) ON DELETE SET NULL,
    code varchar(50) NOT NULL,
    severity varchar(20) NOT NULL,
    message text NOT NULL,
    raised_at_utc timestamptz NOT NULL,
    cleared_at_utc timestamptz
);

CREATE TABLE IF NOT EXISTS alarm_acknowledgements
(
    id uuid PRIMARY KEY,
    alarm_id uuid NOT NULL REFERENCES alarms(id) ON DELETE CASCADE,
    acknowledged_by varchar(120) NOT NULL,
    acknowledged_at_utc timestamptz NOT NULL,
    note text
);

CREATE TABLE IF NOT EXISTS protocol_messages
(
    id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    session_id uuid NOT NULL REFERENCES controller_sessions(id) ON DELETE CASCADE,
    sequence bigint NOT NULL,
    direction varchar(10) NOT NULL,
    message_type varchar(40) NOT NULL,
    payload text NOT NULL,
    observed_at_utc timestamptz NOT NULL,
    UNIQUE (session_id, sequence, direction)
);

CREATE TABLE IF NOT EXISTS outbox_messages
(
    id uuid PRIMARY KEY,
    message_type varchar(200) NOT NULL,
    payload jsonb NOT NULL,
    occurred_at_utc timestamptz NOT NULL,
    processed_at_utc timestamptz,
    attempt_count integer NOT NULL DEFAULT 0,
    last_error text
);

CREATE INDEX IF NOT EXISTS ix_jobs_created_at
    ON jobs (created_at_utc DESC);

CREATE INDEX IF NOT EXISTS ix_job_commands_job_requested
    ON job_commands (job_id, requested_at_utc);

CREATE INDEX IF NOT EXISTS ix_job_runs_job
    ON job_runs (job_id, started_at_utc DESC);

CREATE INDEX IF NOT EXISTS ix_machine_samples_machine_recorded
    ON machine_samples (machine_id, recorded_at_utc DESC);

CREATE INDEX IF NOT EXISTS ix_alarms_machine_open
    ON alarms (machine_id, raised_at_utc DESC)
    WHERE cleared_at_utc IS NULL;

CREATE INDEX IF NOT EXISTS ix_protocol_messages_session_sequence
    ON protocol_messages (session_id, sequence);

CREATE INDEX IF NOT EXISTS ix_outbox_unprocessed
    ON outbox_messages (occurred_at_utc)
    WHERE processed_at_utc IS NULL;
