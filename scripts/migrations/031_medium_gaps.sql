ALTER TABLE accounts
    ADD COLUMN IF NOT EXISTS acc_game_authority SMALLINT NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS acc_game_access    SMALLINT NOT NULL DEFAULT 0;

ALTER TABLE characters
    ADD COLUMN IF NOT EXISTS char_authority SMALLINT NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS char_in_game   SMALLINT NOT NULL DEFAULT 0;

CREATE TABLE IF NOT EXISTS deleted_char (
    char_uid       BIGINT       PRIMARY KEY,
    account_id     BIGINT       NOT NULL,
    char_name      VARCHAR(32)  NOT NULL,
    user_ip        INET         NULL,
    deleted_at     TIMESTAMPTZ  NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS deleted_char_account_idx ON deleted_char (account_id);

CREATE TABLE IF NOT EXISTS char_restore_log (
    seq            BIGSERIAL    PRIMARY KEY,
    account_id     BIGINT       NOT NULL,
    char_uid       BIGINT       NOT NULL,
    old_name       VARCHAR(32)  NOT NULL,
    new_name       VARCHAR(32)  NOT NULL,
    deleted_at     TIMESTAMPTZ  NOT NULL,
    restored_at    TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    gm_id          VARCHAR(64)  NULL,
    reason         VARCHAR(255) NULL
);

CREATE INDEX IF NOT EXISTS char_restore_log_char_idx ON char_restore_log (char_uid);

CREATE TABLE IF NOT EXISTS memory_port (
    char_uid       BIGINT       NOT NULL REFERENCES characters(character_id) ON DELETE CASCADE,
    port_type      SMALLINT     NOT NULL,
    slot_num       SMALLINT     NOT NULL,
    zone           SMALLINT     NOT NULL,
    pos_x          INTEGER      NOT NULL,
    pos_y          INTEGER      NOT NULL,
    pt_desc        VARCHAR(32)  NOT NULL DEFAULT '',
    created_at     TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    PRIMARY KEY (char_uid, port_type, slot_num)
);

CREATE TABLE IF NOT EXISTS pkinfo (
    char_uid              BIGINT  PRIMARY KEY REFERENCES characters(character_id) ON DELETE CASCADE,
    kill_today_count      INTEGER NOT NULL DEFAULT 0,
    kill_total_count      INTEGER NOT NULL DEFAULT 0,
    death_today_count     INTEGER NOT NULL DEFAULT 0,
    death_total_count     INTEGER NOT NULL DEFAULT 0,
    latest_update_at      TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS pkinfo_log (
    log_seq            BIGSERIAL PRIMARY KEY,
    char_uid           BIGINT      NOT NULL,
    kill_today_count   INTEGER     NOT NULL,
    kill_total_count   INTEGER     NOT NULL,
    death_today_count  INTEGER     NOT NULL,
    death_total_count  INTEGER     NOT NULL,
    log_at             TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS pkinfo_log_char_idx ON pkinfo_log (char_uid, log_at DESC);

CREATE TABLE IF NOT EXISTS unique_char_duty_info (
    char_uid          BIGINT      PRIMARY KEY REFERENCES characters(character_id) ON DELETE CASCADE,
    bossmonmedal_count SMALLINT   NOT NULL DEFAULT 0,
    unique_item_count  SMALLINT   NOT NULL DEFAULT 0,
    duty_end_at        TIMESTAMPTZ NULL
);

CREATE TABLE IF NOT EXISTS char_profile (
    char_uid             BIGINT      PRIMARY KEY REFERENCES characters(character_id) ON DELETE CASCADE,
    public_flag_bitmask  INTEGER     NOT NULL DEFAULT 0,
    self_intro           VARCHAR(255) NOT NULL DEFAULT '',
    title_type           INTEGER     NOT NULL DEFAULT 0,
    title_id             INTEGER     NOT NULL DEFAULT 0,
    updated_at           TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
