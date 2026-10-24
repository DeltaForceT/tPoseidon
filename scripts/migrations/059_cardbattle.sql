CREATE TABLE IF NOT EXISTS cardbattle (
    char_uid    BIGINT      PRIMARY KEY REFERENCES characters(character_id) ON DELETE CASCADE,
    winpoint    INTEGER     NOT NULL DEFAULT 0,
    wincount    INTEGER     NOT NULL DEFAULT 0,
    battlecount INTEGER     NOT NULL DEFAULT 0,
    dis         INTEGER     NOT NULL DEFAULT 0,
    specialdis  INTEGER     NOT NULL DEFAULT 0
);

CREATE TABLE IF NOT EXISTS cardbattle_count (
    char_uid    BIGINT      NOT NULL REFERENCES characters(character_id) ON DELETE CASCADE,
    other_uid   BIGINT      NOT NULL,
    created_at  TIMESTAMP   NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS cardbattle_count_char_idx ON cardbattle_count (char_uid, created_at);
