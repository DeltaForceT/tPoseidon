CREATE TABLE IF NOT EXISTS forbidden_name (
    word        VARCHAR(32) PRIMARY KEY
);

CREATE TABLE IF NOT EXISTS char_name_history (
    seq         BIGSERIAL    PRIMARY KEY,
    char_uid    BIGINT       NOT NULL REFERENCES characters(character_id) ON DELETE CASCADE,
    old_name    VARCHAR(24)  NOT NULL,
    new_name    VARCHAR(24)  NOT NULL,
    changed_at  TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    reason      VARCHAR(64)  NOT NULL DEFAULT ''
);

CREATE INDEX IF NOT EXISTS char_name_history_char_idx ON char_name_history (char_uid);

CREATE TABLE IF NOT EXISTS imprint_active (
    char_uid      BIGINT       NOT NULL REFERENCES characters(character_id) ON DELETE CASCADE,
    imprint_type  SMALLINT     NOT NULL,
    item_id       INTEGER      NOT NULL,
    expires_at    TIMESTAMPTZ  NOT NULL,
    PRIMARY KEY (char_uid, imprint_type)
);
