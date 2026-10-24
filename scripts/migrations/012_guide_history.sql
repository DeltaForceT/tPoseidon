CREATE TABLE IF NOT EXISTS guide_history (
    char_uid          BIGINT        PRIMARY KEY
                                    REFERENCES characters(character_id)
                                    ON DELETE CASCADE,
    system_guide      VARCHAR(1000) NOT NULL DEFAULT '',
    tutorial_guide    BIGINT        NOT NULL DEFAULT 0,
    update_date       TIMESTAMPTZ   NOT NULL DEFAULT NOW()
);
