CREATE TABLE IF NOT EXISTS couple_event_history (
    history_log BIGSERIAL   PRIMARY KEY,
    couple_code BIGINT      NOT NULL,
    char_uid    BIGINT      NOT NULL,
    event_code  SMALLINT    NOT NULL,
    created_at  TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS couple_event_history_idx ON couple_event_history (couple_code, created_at);
