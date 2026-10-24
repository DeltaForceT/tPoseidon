CREATE TABLE IF NOT EXISTS gacha_coupon (
    coupon_id   BIGSERIAL    PRIMARY KEY,
    account_id  BIGINT       NOT NULL REFERENCES accounts(account_id) ON DELETE CASCADE,
    source      SMALLINT     NOT NULL DEFAULT 0,
    created_at  TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    spent_at    TIMESTAMPTZ  NULL,
    draw_id     BIGINT       NULL
);

CREATE INDEX IF NOT EXISTS gacha_coupon_unused_idx ON gacha_coupon (account_id, coupon_id) WHERE spent_at IS NULL;

CREATE TABLE IF NOT EXISTS gacha_prize (
    area            SMALLINT  NOT NULL,
    deep            BOOLEAN   NOT NULL,
    item_index      INTEGER   NOT NULL,
    item_class      SMALLINT  NOT NULL,
    item_count      SMALLINT  NOT NULL DEFAULT 1 CHECK (item_count > 0),
    use_time_hours  SMALLINT  NOT NULL DEFAULT 0 CHECK (use_time_hours >= 0),
    item_flag       SMALLINT  NOT NULL DEFAULT 0,
    weight          INTEGER   NOT NULL CHECK (weight > 0),
    PRIMARY KEY (area, deep, item_index)
);

CREATE TABLE IF NOT EXISTS gacha_draw (
    draw_id           BIGSERIAL    PRIMARY KEY,
    account_id        BIGINT       NOT NULL REFERENCES accounts(account_id) ON DELETE CASCADE,
    area              SMALLINT     NOT NULL,
    deep              BOOLEAN      NOT NULL,
    item_index        INTEGER      NOT NULL,
    charged_item_uid  BIGINT       NOT NULL,
    coupons           SMALLINT     NOT NULL,
    pool_weight       INTEGER      NOT NULL,
    remote_address    TEXT         NOT NULL,
    drawn_at          TIMESTAMPTZ  NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS gacha_draw_account_idx ON gacha_draw (account_id, draw_id);
