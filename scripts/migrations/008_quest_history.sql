CREATE TABLE IF NOT EXISTS quest_history (
    char_uid              BIGINT       NOT NULL
                                       REFERENCES characters(character_id)
                                       ON DELETE CASCADE,
    quest_type            SMALLINT     NOT NULL CHECK (quest_type BETWEEN 1 AND 7),
    quest_id              INTEGER      NOT NULL,
    com_count             SMALLINT     NOT NULL DEFAULT 1
                                       CHECK (com_count BETWEEN 1 AND 99),
    monster_id            INTEGER      NOT NULL DEFAULT 0,

    kill_count            SMALLINT     NOT NULL DEFAULT 0,

    record_time           INTEGER      NOT NULL DEFAULT 0,
    quest_completed_date  TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    PRIMARY KEY (char_uid, quest_type, quest_id, monster_id)
);

CREATE INDEX IF NOT EXISTS idx_quest_history_char_uid ON quest_history(char_uid);

CREATE TABLE IF NOT EXISTS quest_history_log (
    log_seq               BIGSERIAL    PRIMARY KEY,
    char_uid              BIGINT       NOT NULL,
    quest_type            SMALLINT     NOT NULL CHECK (quest_type BETWEEN 1 AND 7),
    quest_id              INTEGER      NOT NULL,
    com_count             SMALLINT     NOT NULL,
    monster_id            INTEGER      NOT NULL DEFAULT 0,
    kill_count            SMALLINT     NOT NULL DEFAULT 0,
    record_time           INTEGER      NOT NULL DEFAULT 0,
    quest_completed_date  TIMESTAMPTZ  NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_quest_history_log_char_uid ON quest_history_log(char_uid);
CREATE INDEX IF NOT EXISTS idx_quest_history_log_quest_id ON quest_history_log(quest_id);
