ALTER TABLE accounts

    ADD COLUMN IF NOT EXISTS password_hash TEXT,

    ADD COLUMN IF NOT EXISTS account_authority CHAR(1) NOT NULL DEFAULT 'A',

    ADD COLUMN IF NOT EXISTS account_access SMALLINT NOT NULL DEFAULT 0
        CHECK (account_access BETWEEN 0 AND 2),

    ADD COLUMN IF NOT EXISTS account_created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    ADD COLUMN IF NOT EXISTS account_login_count INTEGER NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS account_latest_login_at TIMESTAMPTZ,
    ADD COLUMN IF NOT EXISTS account_latest_ip INET,

    ADD COLUMN IF NOT EXISTS acc_game_authcode BIGINT NOT NULL DEFAULT 0,

    ADD COLUMN IF NOT EXISTS acc_game_state SMALLINT NOT NULL DEFAULT 0
        CHECK (acc_game_state IN (0, 2, 3, 4)),

    ADD COLUMN IF NOT EXISTS acc_game_state_changed_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),

    ADD COLUMN IF NOT EXISTS acc_game_latest_world SMALLINT,
    ADD COLUMN IF NOT EXISTS acc_game_latest_island SMALLINT,

    ADD COLUMN IF NOT EXISTS acc_game_login_count INTEGER NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS acc_game_logout_state SMALLINT NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS acc_game_playtime_minutes INTEGER NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS acc_game_latest_login_at TIMESTAMPTZ,
    ADD COLUMN IF NOT EXISTS acc_game_latest_logout_at TIMESTAMPTZ,

    ADD COLUMN IF NOT EXISTS acc_game_latest_ip INET;

CREATE INDEX IF NOT EXISTS accounts_acc_game_state_ip_idx
    ON accounts (acc_game_latest_ip, acc_game_state_changed_at)
    WHERE acc_game_state = 3;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_class
        WHERE relname = 'accounts_account_id_seq'
          AND relnamespace = (SELECT oid FROM pg_namespace WHERE nspname = current_schema())
    ) THEN
        EXECUTE 'CREATE SEQUENCE accounts_account_id_seq START WITH 1000 OWNED BY accounts.account_id';
    END IF;
END $$;
