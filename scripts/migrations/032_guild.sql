ALTER TABLE characters
    ADD COLUMN IF NOT EXISTS guild_code      BIGINT   NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS guild_state     SMALLINT NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS is_guild_chief  BOOLEAN  NOT NULL DEFAULT FALSE;

CREATE TABLE IF NOT EXISTS guild (
    guild_code            BIGSERIAL    PRIMARY KEY,
    guild_name            VARCHAR(64)  NOT NULL UNIQUE,
    guild_password        VARCHAR(64)  NOT NULL DEFAULT '',
    master_account_id     BIGINT       NOT NULL,
    master_char_uid       BIGINT       NOT NULL,
    guild_created_at      TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    guild_gelder          BIGINT       NOT NULL DEFAULT 0,
    guild_point           INTEGER      NOT NULL DEFAULT 0,
    guild_members         SMALLINT     NOT NULL DEFAULT 1,
    guild_intro           VARCHAR(255) NOT NULL DEFAULT '',
    guild_notice          VARCHAR(255) NOT NULL DEFAULT '',
    emblem_code           INTEGER      NOT NULL DEFAULT 0,
    guild_join_point      SMALLINT     NOT NULL DEFAULT 1,
    guild_sms_point       SMALLINT     NOT NULL DEFAULT 1,
    guild_url             VARCHAR(255) NOT NULL DEFAULT '',
    guild_pw              VARCHAR(64)  NOT NULL DEFAULT '',
    guild_point_increase  INTEGER      NOT NULL DEFAULT 0,
    guild_ranking         INTEGER      NOT NULL DEFAULT 0,
    guild_ranking_point   INTEGER      NOT NULL DEFAULT 0
);

CREATE TABLE IF NOT EXISTS guild_grade (
    guild_code   BIGINT      NOT NULL REFERENCES guild(guild_code) ON DELETE CASCADE,
    grade_code   SMALLINT    NOT NULL,
    grade_point  INTEGER     NOT NULL DEFAULT 0,
    grade_name   VARCHAR(32) NOT NULL DEFAULT '',
    PRIMARY KEY (guild_code, grade_code)
);

CREATE TABLE IF NOT EXISTS guild_member (
    guild_code      BIGINT      NOT NULL REFERENCES guild(guild_code) ON DELETE CASCADE,
    char_uid        BIGINT      NOT NULL REFERENCES characters(character_id) ON DELETE CASCADE,
    member_nick     VARCHAR(32) NOT NULL DEFAULT '',
    member_created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    member_point    INTEGER     NOT NULL DEFAULT 0,
    grade_code      SMALLINT    NOT NULL DEFAULT 0,
    member_right    SMALLINT    NOT NULL DEFAULT 0,
    PRIMARY KEY (guild_code, char_uid)
);

CREATE INDEX IF NOT EXISTS guild_member_char_idx ON guild_member (char_uid);

CREATE TABLE IF NOT EXISTS guild_member_standby (
    guild_code     BIGINT      NOT NULL REFERENCES guild(guild_code) ON DELETE CASCADE,
    char_uid       BIGINT      NOT NULL REFERENCES characters(character_id) ON DELETE CASCADE,
    standby_state  SMALLINT    NOT NULL DEFAULT 0,
    standby_at     TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    PRIMARY KEY (guild_code, char_uid)
);

CREATE TABLE IF NOT EXISTS guild_recruit (
    guild_code        BIGINT       NOT NULL REFERENCES guild(guild_code) ON DELETE CASCADE,
    char_uid          BIGINT       NOT NULL,
    recruit_title     VARCHAR(64)  NOT NULL DEFAULT '',
    recruit_content   VARCHAR(255) NOT NULL DEFAULT '',
    recruit_updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    PRIMARY KEY (guild_code)
);

CREATE TABLE IF NOT EXISTS guild_notice (
    notice_code      BIGSERIAL    PRIMARY KEY,
    guild_code       BIGINT       NOT NULL REFERENCES guild(guild_code) ON DELETE CASCADE,
    char_uid         BIGINT       NOT NULL,
    notice_content   VARCHAR(255) NOT NULL DEFAULT '',
    notice_created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS guild_notice_guild_idx ON guild_notice (guild_code, notice_created_at DESC);

CREATE TABLE IF NOT EXISTS guild_log (
    log_seq      BIGSERIAL    PRIMARY KEY,
    log_at       TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    log_type     SMALLINT     NOT NULL,
    guild_code   BIGINT       NOT NULL,
    log_msg      VARCHAR(64)  NOT NULL DEFAULT ''
);

CREATE INDEX IF NOT EXISTS guild_log_guild_idx ON guild_log (guild_code, log_at DESC);

CREATE TABLE IF NOT EXISTS guild_penalty (
    char_uid        BIGINT      NOT NULL,
    guild_name      VARCHAR(64) NOT NULL DEFAULT '',
    penalty_type    SMALLINT    NOT NULL,
    secede_at       TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    release_at      TIMESTAMPTZ NOT NULL,
    PRIMARY KEY (char_uid, secede_at)
);

CREATE TABLE IF NOT EXISTS guild_point_history (
    seq                BIGSERIAL    PRIMARY KEY,
    char_uid           BIGINT       NULL,
    add_guild_point    INTEGER      NOT NULL DEFAULT 0,
    created_at         TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    history_type       SMALLINT     NOT NULL DEFAULT 0,
    guild_code         BIGINT       NOT NULL
);

CREATE INDEX IF NOT EXISTS guild_point_history_guild_idx
    ON guild_point_history (guild_code, created_at DESC);

CREATE TABLE IF NOT EXISTS guild_ladder (
    guild_code        BIGINT      PRIMARY KEY REFERENCES guild(guild_code) ON DELETE CASCADE,
    guild_ladder      INTEGER     NOT NULL DEFAULT 1000,
    match_count       SMALLINT    NOT NULL DEFAULT 0,
    win_count         SMALLINT    NOT NULL DEFAULT 0,
    lose_count        SMALLINT    NOT NULL DEFAULT 0,
    draw_count        SMALLINT    NOT NULL DEFAULT 0,
    disc_count        SMALLINT    NOT NULL DEFAULT 0,
    ladder_increase   INTEGER     NOT NULL DEFAULT 1000
);

CREATE TABLE IF NOT EXISTS guild_ladder_log (
    seq              BIGSERIAL    PRIMARY KEY,
    guild_code       BIGINT       NULL,
    guild_name       VARCHAR(64)  NULL,
    guild_ladder     INTEGER      NULL,
    ladder_increase  INTEGER      NULL,
    log_at           TIMESTAMPTZ  NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS guild_ladder_increase_update (
    seq              BIGSERIAL    PRIMARY KEY,
    guild_code       BIGINT       NULL,
    guild_name       VARCHAR(64)  NULL,
    guild_ladder     INTEGER      NULL,
    ladder_increase  INTEGER      NULL,
    log_at           TIMESTAMPTZ  NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS guild_match (
    match_code     BIGSERIAL   PRIMARY KEY,
    guild_code     BIGINT      NOT NULL REFERENCES guild(guild_code) ON DELETE CASCADE,
    join_type      SMALLINT    NOT NULL,
    join_users     SMALLINT    NOT NULL,
    kill_count     SMALLINT    NOT NULL DEFAULT 0,
    death_count    SMALLINT    NOT NULL DEFAULT 0,
    get_ladder     SMALLINT    NOT NULL DEFAULT 0,
    total_ladder   INTEGER     NOT NULL DEFAULT 0,
    guild_value    INTEGER     NOT NULL DEFAULT 0,
    matched_at     TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS guild_ranking (
    seq                   BIGSERIAL   PRIMARY KEY,
    week_day              VARCHAR(64) NOT NULL DEFAULT '',
    guild_ranking         INTEGER     NOT NULL,
    guild_code            BIGINT      NOT NULL,
    guild_name            VARCHAR(64) NOT NULL,
    guild_point_increase  INTEGER     NOT NULL DEFAULT 0,
    guild_ranking_point   INTEGER     NOT NULL DEFAULT 0,
    snapshot_at           TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS guild_ranking_guild_idx ON guild_ranking (guild_code, snapshot_at DESC);

CREATE TABLE IF NOT EXISTS guild_state (
    seq          BIGSERIAL    PRIMARY KEY,
    guild_code   BIGINT       NOT NULL REFERENCES guild(guild_code) ON DELETE CASCADE,
    state_type   SMALLINT     NOT NULL,
    state_note   VARCHAR(255) NOT NULL DEFAULT '',
    reg_at       TIMESTAMPTZ  NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS guild_state_guild_idx ON guild_state (guild_code, reg_at DESC);
