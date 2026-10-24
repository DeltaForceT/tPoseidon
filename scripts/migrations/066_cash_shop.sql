CREATE TABLE IF NOT EXISTS shop_limit (
    limit_code   SMALLINT  PRIMARY KEY,
    limit_flag   SMALLINT  NOT NULL DEFAULT 0,
    limit_use    SMALLINT  NOT NULL DEFAULT 0
);

CREATE TABLE IF NOT EXISTS shop_goods (
    goods_code        INTEGER       PRIMARY KEY,
    goods_name        VARCHAR(100)  NOT NULL,
    goods_desc        VARCHAR(500)  NOT NULL DEFAULT '',
    goods_category    SMALLINT      NOT NULL DEFAULT 0,
    goods_set_count   SMALLINT      NOT NULL DEFAULT 1,
    goods_issell      BOOLEAN       NOT NULL DEFAULT TRUE,
    goods_sellcount   INTEGER       NOT NULL DEFAULT 0,
    goods_created     TIMESTAMPTZ   NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS shop_goods_item (
    goods_code   INTEGER   NOT NULL REFERENCES shop_goods(goods_code) ON DELETE CASCADE,
    item_index   INTEGER   NOT NULL,
    item_class   SMALLINT  NOT NULL,
    item_count   SMALLINT  NOT NULL DEFAULT 1,
    PRIMARY KEY (goods_code, item_index, item_class)
);

CREATE TABLE IF NOT EXISTS shop_goods_limit (
    goods_code         INTEGER   NOT NULL REFERENCES shop_goods(goods_code) ON DELETE CASCADE,
    limit_code         SMALLINT  NOT NULL REFERENCES shop_limit(limit_code),
    goods_limit_price  INTEGER   NOT NULL,
    default_display    BOOLEAN   NOT NULL DEFAULT FALSE,
    PRIMARY KEY (goods_code, limit_code)
);

CREATE TABLE IF NOT EXISTS shop_sell (
    sell_code       SERIAL       PRIMARY KEY,
    account_id      BIGINT       NOT NULL REFERENCES accounts(account_id),
    goods_code      INTEGER      NOT NULL REFERENCES shop_goods(goods_code),
    limit_code      SMALLINT     NOT NULL,
    sell_price      INTEGER      NOT NULL,
    sell_world      SMALLINT     NOT NULL,
    sell_store      SMALLINT     NOT NULL,
    sell_type       SMALLINT     NOT NULL,
    to_account_id   BIGINT       NULL REFERENCES accounts(account_id),
    sell_date       TIMESTAMPTZ  NOT NULL DEFAULT NOW()
);
CREATE INDEX IF NOT EXISTS shop_sell_account_idx ON shop_sell (account_id, sell_date);

CREATE TABLE IF NOT EXISTS cash_balance (
    account_id   BIGINT       PRIMARY KEY REFERENCES accounts(account_id),
    balance      BIGINT       NOT NULL CHECK (balance >= 0),
    updated_at   TIMESTAMPTZ  NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS cash_grant (
    grant_id     BIGSERIAL    PRIMARY KEY,
    account_id   BIGINT       NOT NULL REFERENCES accounts(account_id),
    amount       BIGINT       NOT NULL,
    reason       VARCHAR(100) NOT NULL DEFAULT '',
    granted_at   TIMESTAMPTZ  NOT NULL DEFAULT NOW()
);
