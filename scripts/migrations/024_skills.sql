CREATE TABLE IF NOT EXISTS skills (
    char_uid       BIGINT      NOT NULL
                               REFERENCES characters(character_id)
                               ON DELETE CASCADE,
    skill_id       INT         NOT NULL,
    skill_level    INT         NOT NULL DEFAULT 1,
    skill_updated  TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    PRIMARY KEY (char_uid, skill_id)
);

CREATE INDEX IF NOT EXISTS idx_skills_char_uid ON skills (char_uid);
