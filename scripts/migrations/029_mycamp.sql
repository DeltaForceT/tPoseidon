CREATE TABLE IF NOT EXISTS mycamp_slot (
    slot_code      BIGSERIAL    PRIMARY KEY,
    char_uid       BIGINT       NOT NULL REFERENCES characters(character_id) ON DELETE CASCADE,
    slot_name      VARCHAR(32)  NOT NULL DEFAULT '',
    slot_uptodate  TIMESTAMPTZ  NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS mycamp_slot_char_idx ON mycamp_slot (char_uid);

CREATE TABLE IF NOT EXISTS mycamp_item (
    item_uid       BIGSERIAL    PRIMARY KEY,
    account_id     BIGINT       NOT NULL REFERENCES accounts(account_id) ON DELETE CASCADE,
    item_index     INTEGER      NOT NULL,
    item_where     SMALLINT     NOT NULL DEFAULT 0,
    item_buy_date  TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    item_use_date  TIMESTAMPTZ  NULL,
    item_use_time  SMALLINT     NOT NULL DEFAULT 0,
    item_flag      SMALLINT     NOT NULL DEFAULT 0,
    sell_code      BIGINT       NOT NULL DEFAULT 0,
    sell_type      SMALLINT     NOT NULL DEFAULT 0
);

CREATE INDEX IF NOT EXISTS mycamp_item_account_idx ON mycamp_item (account_id);

CREATE TABLE IF NOT EXISTS mycamp_deco (
    slot_code        BIGINT       NOT NULL REFERENCES mycamp_slot(slot_code) ON DELETE CASCADE,
    item_uid         BIGINT       NOT NULL REFERENCES mycamp_item(item_uid) ON DELETE CASCADE,
    deco_x           SMALLINT     NOT NULL DEFAULT 0,
    deco_y           SMALLINT     NOT NULL DEFAULT 0,
    deco_z           SMALLINT     NOT NULL DEFAULT 0,
    deco_direction   SMALLINT     NOT NULL DEFAULT 0,
    parents_uid      BIGINT       NOT NULL DEFAULT 0,
    PRIMARY KEY (slot_code, item_uid)
);

CREATE TABLE IF NOT EXISTS user_mycamp_log (
    account_id     BIGINT       PRIMARY KEY REFERENCES accounts(account_id) ON DELETE CASCADE,
    initialized_at TIMESTAMPTZ  NOT NULL DEFAULT NOW()
);
