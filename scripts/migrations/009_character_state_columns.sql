ALTER TABLE characters

    ADD COLUMN IF NOT EXISTS hp                INTEGER     NOT NULL DEFAULT 100
        CHECK (hp >= 0),
    ADD COLUMN IF NOT EXISTS mp                INTEGER     NOT NULL DEFAULT 100
        CHECK (mp >= 0),
    ADD COLUMN IF NOT EXISTS exp               BIGINT      NOT NULL DEFAULT 0
        CHECK (exp >= 0),
    ADD COLUMN IF NOT EXISTS tm_exp_p          BIGINT      NOT NULL DEFAULT 0
        CHECK (tm_exp_p >= 0),
    ADD COLUMN IF NOT EXISTS tm_exp_m          BIGINT      NOT NULL DEFAULT 0
        CHECK (tm_exp_m >= 0),
    ADD COLUMN IF NOT EXISTS tm_exp_s          BIGINT      NOT NULL DEFAULT 0
        CHECK (tm_exp_s >= 0),
    ADD COLUMN IF NOT EXISTS tm_exp_c          BIGINT      NOT NULL DEFAULT 0
        CHECK (tm_exp_c >= 0),
    ADD COLUMN IF NOT EXISTS wh_slots          SMALLINT    NOT NULL DEFAULT 80
        CHECK (wh_slots BETWEEN 0 AND 1000),
    ADD COLUMN IF NOT EXISTS login_time        TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    ADD COLUMN IF NOT EXISTS logout_time       TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    ADD COLUMN IF NOT EXISTS total_con_time    INTEGER     NOT NULL DEFAULT 0
        CHECK (total_con_time >= 0),

    ADD COLUMN IF NOT EXISTS ap_pts            SMALLINT    NOT NULL DEFAULT 0
        CHECK (ap_pts >= 0),
    ADD COLUMN IF NOT EXISTS ac_pts            SMALLINT    NOT NULL DEFAULT 0
        CHECK (ac_pts >= 0),
    ADD COLUMN IF NOT EXISTS dx_pts            SMALLINT    NOT NULL DEFAULT 0
        CHECK (dx_pts >= 0),
    ADD COLUMN IF NOT EXISTS ma_pts            SMALLINT    NOT NULL DEFAULT 0
        CHECK (ma_pts >= 0),
    ADD COLUMN IF NOT EXISTS md_pts            SMALLINT    NOT NULL DEFAULT 0
        CHECK (md_pts >= 0),
    ADD COLUMN IF NOT EXISTS da_pts            SMALLINT    NOT NULL DEFAULT 0
        CHECK (da_pts >= 0),
    ADD COLUMN IF NOT EXISTS lk_pts            SMALLINT    NOT NULL DEFAULT 0
        CHECK (lk_pts >= 0),
    ADD COLUMN IF NOT EXISTS dp_pts            SMALLINT    NOT NULL DEFAULT 0
        CHECK (dp_pts >= 0),
    ADD COLUMN IF NOT EXISTS hv_pts            SMALLINT    NOT NULL DEFAULT 0
        CHECK (hv_pts >= 0),
    ADD COLUMN IF NOT EXISTS max_hp_pts        SMALLINT    NOT NULL DEFAULT 0
        CHECK (max_hp_pts >= 0),
    ADD COLUMN IF NOT EXISTS max_mp_pts        SMALLINT    NOT NULL DEFAULT 0
        CHECK (max_mp_pts >= 0),
    ADD COLUMN IF NOT EXISTS max_wt_pts        SMALLINT    NOT NULL DEFAULT 0
        CHECK (max_wt_pts >= 0),
    ADD COLUMN IF NOT EXISTS attr_pts          SMALLINT    NOT NULL DEFAULT 0
        CHECK (attr_pts >= 0),

    ADD COLUMN IF NOT EXISTS tm_level_p        SMALLINT    NOT NULL DEFAULT 0
        CHECK (tm_level_p BETWEEN 0 AND 30),
    ADD COLUMN IF NOT EXISTS tm_level_m        SMALLINT    NOT NULL DEFAULT 0
        CHECK (tm_level_m BETWEEN 0 AND 30),
    ADD COLUMN IF NOT EXISTS tm_level_s        SMALLINT    NOT NULL DEFAULT 0
        CHECK (tm_level_s BETWEEN 0 AND 30),
    ADD COLUMN IF NOT EXISTS tm_level_c        SMALLINT    NOT NULL DEFAULT 0
        CHECK (tm_level_c BETWEEN 0 AND 30),
    ADD COLUMN IF NOT EXISTS tm_point          SMALLINT    NOT NULL DEFAULT 0
        CHECK (tm_point >= 0),
    ADD COLUMN IF NOT EXISTS tm_level          SMALLINT    NOT NULL DEFAULT 0
        CHECK (tm_level BETWEEN 0 AND 120),
    ADD COLUMN IF NOT EXISTS tm_exp            INTEGER     NOT NULL DEFAULT 0
        CHECK (tm_exp >= 0),

    ADD COLUMN IF NOT EXISTS type_2            SMALLINT    NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS job_2             SMALLINT    NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS job_2_changed     TIMESTAMPTZ NOT NULL DEFAULT '2000-01-01 00:00:00 UTC',

    ADD COLUMN IF NOT EXISTS type_3            SMALLINT    NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS job_3             SMALLINT    NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS job_3_changed     TIMESTAMPTZ NOT NULL DEFAULT '2000-01-01 00:00:00 UTC',

    ADD COLUMN IF NOT EXISTS change_job_queststate SMALLINT NOT NULL DEFAULT 0
        CHECK (change_job_queststate BETWEEN 0 AND 255);
