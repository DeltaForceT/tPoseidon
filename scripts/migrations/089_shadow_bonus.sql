CREATE TABLE IF NOT EXISTS shadow_bonus (
    char_uid     BIGINT       NOT NULL PRIMARY KEY REFERENCES characters(character_id) ON DELETE CASCADE,
    reward_id    INTEGER      NOT NULL,
    expires_at   TIMESTAMPTZ  NOT NULL
);
