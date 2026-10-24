ALTER TABLE characters
    ADD COLUMN IF NOT EXISTS depth_count BIGINT NOT NULL DEFAULT 0
        CHECK (depth_count >= 0),
    ADD COLUMN IF NOT EXISTS dig_count   INTEGER NOT NULL DEFAULT 0
        CHECK (dig_count >= 0);
