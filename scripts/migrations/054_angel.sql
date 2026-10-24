CREATE TABLE IF NOT EXISTS angel_info (
    newbie_char_uid BIGINT      NOT NULL REFERENCES characters(character_id) ON DELETE CASCADE,
    angel_char_uid  BIGINT      NOT NULL REFERENCES characters(character_id) ON DELETE CASCADE,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    PRIMARY KEY (newbie_char_uid, angel_char_uid)
);

CREATE INDEX IF NOT EXISTS angel_info_angel_idx ON angel_info (angel_char_uid);

CREATE TABLE IF NOT EXISTS angel_result (
    angel_char_uid         BIGINT      PRIMARY KEY REFERENCES characters(character_id) ON DELETE CASCADE,
    graduation_count       INTEGER     NOT NULL DEFAULT 0,
    latest_graduation_date TIMESTAMPTZ NOT NULL DEFAULT '2008-01-01'
);
