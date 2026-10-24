DO $$
BEGIN
    IF EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = current_schema()
          AND table_name = 'friend_info'
          AND column_name = 'owner_account_id')
    THEN
        DROP TABLE IF EXISTS friend_sms;
        DROP TABLE IF EXISTS friend_info;
        DROP TABLE IF EXISTS friend_group_info;
    END IF;
END $$;

CREATE TABLE IF NOT EXISTS friend_group_info (
    owner_character_id  BIGINT       NOT NULL REFERENCES characters(character_id) ON DELETE CASCADE,
    group_num           SMALLINT     NOT NULL CHECK (group_num BETWEEN 0 AND 4),
    group_name          VARCHAR(20)  NOT NULL,
    PRIMARY KEY (owner_character_id, group_num)
);

CREATE TABLE IF NOT EXISTS friend_info (
    owner_character_id   BIGINT    NOT NULL REFERENCES characters(character_id) ON DELETE CASCADE,
    friend_character_id  BIGINT    NOT NULL REFERENCES characters(character_id) ON DELETE CASCADE,
    group_num            SMALLINT  NOT NULL DEFAULT 0 CHECK (group_num BETWEEN 0 AND 4),
    deleted              BOOLEAN   NOT NULL DEFAULT FALSE,
    PRIMARY KEY (owner_character_id, friend_character_id)
);

CREATE INDEX IF NOT EXISTS friend_info_friend_idx ON friend_info (friend_character_id);

CREATE TABLE IF NOT EXISTS friend_sms (
    sms_seq              BIGSERIAL     PRIMARY KEY,
    owner_character_id   BIGINT        NOT NULL REFERENCES characters(character_id) ON DELETE CASCADE,
    sender_character_id  BIGINT        NOT NULL,
    sent_at              TIMESTAMPTZ   NOT NULL DEFAULT NOW(),
    content              VARCHAR(200)  NOT NULL,
    sms_kind             SMALLINT      NOT NULL DEFAULT 0 CHECK (sms_kind BETWEEN 0 AND 4)
);

CREATE INDEX IF NOT EXISTS friend_sms_owner_idx ON friend_sms (owner_character_id, sms_kind DESC);
