CREATE TABLE IF NOT EXISTS char_soul (
    char_uid    BIGINT       PRIMARY KEY REFERENCES characters(character_id) ON DELETE CASCADE,
    soul        BIGINT       NOT NULL DEFAULT 0 CHECK (soul BETWEEN 0 AND 4294967295),
    updated_at  TIMESTAMPTZ  NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS char_soulguardian_source (
    guardian_uid     BIGINT       PRIMARY KEY REFERENCES char_soulguardian(guardian_uid) ON DELETE CASCADE,
    source_item_uid  BIGINT       NOT NULL DEFAULT 0,
    source_count     INTEGER      NOT NULL DEFAULT 0,
    potions_ready    INTEGER      NOT NULL DEFAULT 0,
    last_tick        TIMESTAMPTZ  NOT NULL DEFAULT NOW()
);
