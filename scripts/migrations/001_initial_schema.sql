CREATE TABLE IF NOT EXISTS accounts (
    account_id          BIGINT       PRIMARY KEY
                                     CHECK (account_id BETWEEN 0 AND 4294967295),
    username            VARCHAR(32)  NOT NULL UNIQUE,

    password_placeholder VARCHAR(64) NOT NULL,
    created_at          TIMESTAMPTZ  NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS characters (
    character_id        BIGINT       PRIMARY KEY,
    account_id          BIGINT       NOT NULL
                                     REFERENCES accounts(account_id)
                                     ON DELETE CASCADE,
    slot                SMALLINT     NOT NULL CHECK (slot BETWEEN 0 AND 2),
    name                VARCHAR(24)  NOT NULL,

    species             SMALLINT     NOT NULL CHECK (species BETWEEN 1 AND 9),
    ftype               SMALLINT     NOT NULL CHECK (ftype BETWEEN 1 AND 4),
    job_tier            SMALLINT     NOT NULL DEFAULT 1,
    hair                INTEGER      NOT NULL DEFAULT 0,
    money               BIGINT       NOT NULL DEFAULT 0
                                     CHECK (money BETWEEN 0 AND 4294967295),
    level               INTEGER      NOT NULL DEFAULT 1,

    position_x          INTEGER      NOT NULL DEFAULT 0,
    position_y          INTEGER      NOT NULL DEFAULT 0,
    map_id              INTEGER      NOT NULL DEFAULT 0,

    starter_ear         INTEGER      NOT NULL DEFAULT 6905,
    starter_tail        INTEGER      NOT NULL DEFAULT 6955,
    created_at          TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    UNIQUE (account_id, slot),
    UNIQUE (account_id, name)
);

CREATE INDEX IF NOT EXISTS idx_characters_account_id
    ON characters(account_id);

CREATE TABLE IF NOT EXISTS session_tokens (
    token_value         BIGINT       PRIMARY KEY
                                     CHECK (token_value BETWEEN 0 AND 4294967295),
    account_id          BIGINT       NOT NULL
                                     REFERENCES accounts(account_id)
                                     ON DELETE CASCADE,
    issued_at           TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    expires_at          TIMESTAMPTZ  NOT NULL
);

CREATE INDEX IF NOT EXISTS idx_session_tokens_account_id
    ON session_tokens(account_id);
CREATE INDEX IF NOT EXISTS idx_session_tokens_expires_at
    ON session_tokens(expires_at);
