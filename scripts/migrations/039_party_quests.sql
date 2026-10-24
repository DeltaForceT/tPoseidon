CREATE TABLE IF NOT EXISTS party_quest (
    party_uid      BIGSERIAL    PRIMARY KEY,
    quest_id       SMALLINT     NOT NULL,
    monster_id     SMALLINT     NOT NULL,
    target_count   SMALLINT     NOT NULL,
    record_time    INTEGER      NOT NULL DEFAULT 0,
    member_count   SMALLINT     NOT NULL DEFAULT 0,
    created_at     TIMESTAMPTZ  NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS party_quest_member (
    party_uid      BIGINT       NOT NULL REFERENCES party_quest(party_uid) ON DELETE CASCADE,
    char_uid       BIGINT       NOT NULL REFERENCES characters(character_id) ON DELETE CASCADE,
    char_level     SMALLINT     NOT NULL,
    char_job       SMALLINT     NOT NULL,
    kill_count     SMALLINT     NOT NULL,
    com_count      INTEGER      NOT NULL,
    reward_date    TIMESTAMPTZ  NULL,
    PRIMARY KEY (party_uid, char_uid)
);

CREATE INDEX IF NOT EXISTS party_quest_member_char_idx ON party_quest_member (char_uid) WHERE reward_date IS NULL;
