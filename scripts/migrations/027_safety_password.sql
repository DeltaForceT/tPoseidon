CREATE TABLE IF NOT EXISTS account_safety_password (
    account_id          BIGINT       PRIMARY KEY
                                     REFERENCES accounts(account_id)
                                     ON DELETE CASCADE,
    safety_pw_hash      VARCHAR(64)  NOT NULL,
    lock_flag           SMALLINT     NOT NULL DEFAULT 0,
    lock_limit_bitmask  SMALLINT     NOT NULL DEFAULT 0,
    email               VARCHAR(254) NOT NULL DEFAULT '',
    created_at          TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    updated_at          TIMESTAMPTZ  NOT NULL DEFAULT NOW()
);
