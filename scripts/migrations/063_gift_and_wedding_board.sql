CREATE TABLE IF NOT EXISTS gift_item (
    gift_code        SERIAL       PRIMARY KEY,
    goods_code       INTEGER      NOT NULL,
    goods_category   SMALLINT     NOT NULL,
    item_count       SMALLINT     NULL,
    item_class       SMALLINT     NOT NULL,
    item_index       INTEGER      NOT NULL,
    item_attr        SMALLINT     NOT NULL DEFAULT 1,
    item_use_time    SMALLINT     NOT NULL DEFAULT 0,
    item_flag        SMALLINT     NOT NULL DEFAULT 0,
    from_account_id  BIGINT       NOT NULL REFERENCES accounts(account_id),
    from_char_uid    BIGINT       NOT NULL REFERENCES characters(character_id),
    to_account_id    BIGINT       NOT NULL REFERENCES accounts(account_id),
    to_char_uid      BIGINT       NOT NULL REFERENCES characters(character_id),
    gift_date        TIMESTAMPTZ  NOT NULL,
    take_date        TIMESTAMPTZ  NULL,
    gift_message     VARCHAR(100) NOT NULL DEFAULT '',
    receive_message  VARCHAR(100) NOT NULL DEFAULT '',
    sell_code        INTEGER      NOT NULL DEFAULT 0,
    limit_code       SMALLINT     NOT NULL DEFAULT 0
);
CREATE INDEX IF NOT EXISTS gift_item_from_idx ON gift_item (from_account_id);
CREATE INDEX IF NOT EXISTS gift_item_to_idx   ON gift_item (to_account_id, take_date);

CREATE TABLE IF NOT EXISTS couple_wedding_rsv_board (
    couple_code      BIGINT       PRIMARY KEY,
    rsv_title        VARCHAR(100) NOT NULL,
    wedding_map_id   SMALLINT     NOT NULL,
    card_msg         VARCHAR(150) NOT NULL,
    card_type        SMALLINT     NOT NULL,
    dress_type       INTEGER      NOT NULL,
    male_hair_type   SMALLINT     NOT NULL,
    male_job_type    SMALLINT     NOT NULL,
    male_name        VARCHAR(17)  NOT NULL,
    female_hair_type SMALLINT     NOT NULL,
    female_job_type  SMALLINT     NOT NULL,
    female_name      VARCHAR(17)  NOT NULL,
    latest_update_at TIMESTAMPTZ  NOT NULL,
    created_at       TIMESTAMPTZ  NOT NULL DEFAULT NOW()
);
