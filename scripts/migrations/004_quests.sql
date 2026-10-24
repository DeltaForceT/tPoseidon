CREATE TABLE IF NOT EXISTS quests (
    char_uid        BIGINT       NOT NULL
                                 REFERENCES characters(character_id)
                                 ON DELETE CASCADE,

    quest_type      SMALLINT     NOT NULL CHECK (quest_type BETWEEN 1 AND 7),
    quest_id        INTEGER      NOT NULL,

    com_count       SMALLINT     NOT NULL DEFAULT 0
                                 CHECK (com_count BETWEEN 0 AND 99),

    monster_id      INTEGER      NOT NULL DEFAULT 0,

    kill_count      SMALLINT     NOT NULL DEFAULT 0,

    record_time     INTEGER      NOT NULL DEFAULT 0,
    accepted_at     TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    PRIMARY KEY (char_uid, quest_type, quest_id, monster_id)
);

CREATE INDEX IF NOT EXISTS idx_quests_char_uid ON quests(char_uid);
