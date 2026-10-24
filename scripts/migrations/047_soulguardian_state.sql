ALTER TABLE char_soulguardian
    ADD COLUMN IF NOT EXISTS rare_type    SMALLINT NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS title_index  SMALLINT NOT NULL DEFAULT 0;

CREATE TABLE IF NOT EXISTS char_soulguardian_status (
    guardian_uid    BIGINT    PRIMARY KEY REFERENCES char_soulguardian(guardian_uid) ON DELETE CASCADE,
    guardian_level  SMALLINT  NOT NULL DEFAULT 1,
    guardian_exp    INTEGER   NOT NULL DEFAULT 0,
    guardian_gauge  INTEGER   NOT NULL DEFAULT 0,
    pts_ap          SMALLINT  NOT NULL DEFAULT 0,
    pts_ac          SMALLINT  NOT NULL DEFAULT 0,
    pts_dx          SMALLINT  NOT NULL DEFAULT 0,
    pts_maxmp       SMALLINT  NOT NULL DEFAULT 0,
    pts_ma          SMALLINT  NOT NULL DEFAULT 0,
    pts_md          SMALLINT  NOT NULL DEFAULT 0,
    pts_maxwt       SMALLINT  NOT NULL DEFAULT 0,
    pts_da          SMALLINT  NOT NULL DEFAULT 0,
    pts_lk          SMALLINT  NOT NULL DEFAULT 0,
    pts_maxhp       SMALLINT  NOT NULL DEFAULT 0,
    pts_dp          SMALLINT  NOT NULL DEFAULT 0,
    pts_hv          SMALLINT  NOT NULL DEFAULT 0
);

CREATE TABLE IF NOT EXISTS char_soulguardian_skill (
    guardian_uid   BIGINT       NOT NULL REFERENCES char_soulguardian(guardian_uid) ON DELETE CASCADE,
    skill_id       INTEGER      NOT NULL,
    skill_type     SMALLINT     NOT NULL,
    skill_f_type   SMALLINT     NOT NULL DEFAULT 0,
    skill_level    SMALLINT     NOT NULL DEFAULT 1,
    skill_slot     SMALLINT     NOT NULL,
    latest_update  TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    PRIMARY KEY (guardian_uid, skill_type, skill_slot)
);

CREATE TABLE IF NOT EXISTS char_soulguardian_levelup_log (
    log_seq         BIGSERIAL    PRIMARY KEY,
    guardian_uid    BIGINT       NOT NULL,
    guardian_level  SMALLINT     NOT NULL,
    stat_points     SMALLINT[]   NOT NULL,
    skill_point     INTEGER      NOT NULL,
    logged_at       TIMESTAMPTZ  NOT NULL DEFAULT NOW()
);
