CREATE TABLE IF NOT EXISTS char_honor_title (
    char_uid    BIGINT       NOT NULL REFERENCES characters(character_id) ON DELETE CASCADE,
    title_id    INTEGER      NOT NULL,
    created_at  TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    PRIMARY KEY (char_uid, title_id)
);

CREATE TABLE IF NOT EXISTS char_honor_title_log (
    log_seq       BIGSERIAL    PRIMARY KEY,
    char_uid      BIGINT       NOT NULL,
    title_id      INTEGER      NOT NULL,
    quest_id      INTEGER      NOT NULL,
    quest_type    SMALLINT     NOT NULL,
    clear_time    INTEGER      NOT NULL,
    hunter_count  INTEGER      NOT NULL,
    logged_at     TIMESTAMPTZ  NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS char_honor_title_log_char_idx ON char_honor_title_log (char_uid, logged_at DESC);
