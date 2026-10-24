CREATE TABLE IF NOT EXISTS harcon_player (
    char_uid    BIGINT      PRIMARY KEY REFERENCES characters(character_id) ON DELETE CASCADE,
    coins       INTEGER     NOT NULL DEFAULT 0,
    entry_count SMALLINT    NOT NULL DEFAULT 0,
    entry_day   DATE        NULL
);

CREATE TABLE IF NOT EXISTS harcon_clear (
    char_uid    BIGINT      NOT NULL REFERENCES characters(character_id) ON DELETE CASCADE,
    stage_id    SMALLINT    NOT NULL,
    PRIMARY KEY (char_uid, stage_id)
);
