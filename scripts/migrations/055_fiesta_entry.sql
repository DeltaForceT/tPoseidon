CREATE TABLE IF NOT EXISTS fiesta_entry (
    char_uid    BIGINT      PRIMARY KEY REFERENCES characters(character_id) ON DELETE CASCADE,
    entry_count SMALLINT    NOT NULL DEFAULT 0,
    slot_at     TIMESTAMP   NULL
);
