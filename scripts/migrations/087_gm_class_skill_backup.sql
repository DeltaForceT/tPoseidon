CREATE TABLE IF NOT EXISTS gm_skill_swap (
    char_uid    BIGINT      PRIMARY KEY
                            REFERENCES characters(character_id)
                            ON DELETE CASCADE,
    started_at  TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS gm_skill_swap_skills (
    char_uid     BIGINT NOT NULL
                        REFERENCES gm_skill_swap(char_uid)
                        ON DELETE CASCADE,
    skill_id     INT    NOT NULL,
    skill_level  INT    NOT NULL,
    PRIMARY KEY (char_uid, skill_id)
);
