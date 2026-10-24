CREATE TABLE IF NOT EXISTS account_safety_password_log (
    log_seq             BIGSERIAL    PRIMARY KEY,
    log_type            SMALLINT     NOT NULL,
    account_id          BIGINT       NOT NULL,
    safety_pw_hash      VARCHAR(64)  NOT NULL DEFAULT '',
    lock_flag           SMALLINT     NULL,
    lock_limit_bitmask  SMALLINT     NULL,
    email               VARCHAR(254) NOT NULL DEFAULT '',
    created_at          TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    gm_id               VARCHAR(64)  NULL,
    reason              VARCHAR(255) NULL
);

CREATE INDEX IF NOT EXISTS account_safety_password_log_account_idx
    ON account_safety_password_log (account_id, created_at DESC);
