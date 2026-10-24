CREATE TABLE IF NOT EXISTS bossmon (
    char_uid       BIGINT       NOT NULL,
    mon_id         INTEGER      NOT NULL,
    kill_count     INTEGER      NOT NULL DEFAULT 0,
    latest_update  TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    PRIMARY KEY (char_uid, mon_id)
);

CREATE TABLE IF NOT EXISTS bossmon_history (
    log_seq    BIGSERIAL    PRIMARY KEY,
    char_uid   BIGINT       NOT NULL,
    mon_id     INTEGER      NOT NULL,
    kill_date  TIMESTAMPTZ  NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS bossmon_history_char_idx ON bossmon_history (char_uid);
