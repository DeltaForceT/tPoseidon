ALTER TABLE accounts
    ADD COLUMN IF NOT EXISTS account_bank_money BIGINT NOT NULL DEFAULT 0;

CREATE TABLE IF NOT EXISTS item_user (
    account_id     BIGINT       NOT NULL REFERENCES accounts(account_id) ON DELETE CASCADE,
    item_uid       BIGINT       NOT NULL UNIQUE REFERENCES items(item_uid) ON DELETE CASCADE,
    item_pos       SMALLINT     NOT NULL DEFAULT 0,
    PRIMARY KEY (account_id, item_uid)
);

CREATE INDEX IF NOT EXISTS item_user_lookup_idx
    ON item_user (account_id, item_pos);
