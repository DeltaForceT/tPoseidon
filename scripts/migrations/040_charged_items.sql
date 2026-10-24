CREATE TABLE IF NOT EXISTS charged_item (
    item_uid       BIGSERIAL    PRIMARY KEY,
    item_class     SMALLINT     NOT NULL,
    account_id     BIGINT       NOT NULL REFERENCES accounts(account_id) ON DELETE CASCADE,
    item_index     INTEGER      NOT NULL,
    item_attr      SMALLINT     NOT NULL DEFAULT 1,
    item_buy_date  TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    item_use_date  TIMESTAMPTZ  NULL,
    item_use_time  SMALLINT     NOT NULL DEFAULT 0,
    item_flag      SMALLINT     NOT NULL DEFAULT 0,
    sell_code      INTEGER      NOT NULL DEFAULT 0,
    sell_type      SMALLINT     NOT NULL DEFAULT 0,
    take_char      BIGINT       NULL REFERENCES characters(character_id) ON DELETE SET NULL
);

CREATE INDEX IF NOT EXISTS charged_item_waiting_idx ON charged_item (account_id) WHERE item_use_date IS NULL;
