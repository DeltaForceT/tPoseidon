CREATE TABLE IF NOT EXISTS angel_levelup_item_info (
    item_index    INTEGER  PRIMARY KEY,
    item_class    SMALLINT NOT NULL,
    item_attr     SMALLINT NOT NULL,
    item_use_time SMALLINT NOT NULL DEFAULT 0,
    item_flag     SMALLINT NOT NULL DEFAULT 0,
    sell_code     INTEGER  NOT NULL DEFAULT 0
);

INSERT INTO angel_levelup_item_info (item_index, item_class, item_attr, item_use_time, item_flag, sell_code)
VALUES (7342, 2, 1, 0, 0, 2000000011)
ON CONFLICT (item_index) DO NOTHING;

CREATE TABLE IF NOT EXISTS angel_result_log (
    log_seq         BIGSERIAL   PRIMARY KEY,
    newbie_char_uid BIGINT      NOT NULL,
    angel_char_uid  BIGINT      NOT NULL,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
