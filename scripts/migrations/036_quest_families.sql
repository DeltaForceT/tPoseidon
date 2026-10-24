ALTER TABLE quests DROP CONSTRAINT IF EXISTS quests_quest_type_check;
ALTER TABLE quests ADD CONSTRAINT quests_quest_type_check
    CHECK (quest_type BETWEEN 1 AND 9);

ALTER TABLE quest_history DROP CONSTRAINT IF EXISTS quest_history_quest_type_check;
ALTER TABLE quest_history ADD CONSTRAINT quest_history_quest_type_check
    CHECK (quest_type BETWEEN 1 AND 9);

ALTER TABLE quest_history_log DROP CONSTRAINT IF EXISTS quest_history_log_quest_type_check;
ALTER TABLE quest_history_log ADD CONSTRAINT quest_history_log_quest_type_check
    CHECK (quest_type BETWEEN 1 AND 9);

UPDATE quests SET quest_type = 8 WHERE quest_type = 6 AND current_schema() = 'kr';
UPDATE quest_history SET quest_type = 8 WHERE quest_type = 6 AND current_schema() = 'kr';
UPDATE quest_history_log SET quest_type = 8 WHERE quest_type = 6 AND current_schema() = 'kr';

CREATE TABLE IF NOT EXISTS monster_quest_hunts (
    char_uid        BIGINT       PRIMARY KEY
                                 REFERENCES characters(character_id)
                                 ON DELETE CASCADE,
    quest_id        INTEGER      NOT NULL,
    grade           SMALLINT     NOT NULL DEFAULT 0,
    active          BOOLEAN      NOT NULL DEFAULT FALSE,
    remaining_ms    INTEGER      NOT NULL DEFAULT 0,
    accumulator_ms  INTEGER      NOT NULL DEFAULT 0,
    kill_count      INTEGER      NOT NULL DEFAULT 0,
    target_count    INTEGER      NOT NULL DEFAULT 0,
    updated_at      TIMESTAMPTZ  NOT NULL DEFAULT NOW()
);
