CREATE TABLE IF NOT EXISTS item_options (
    item_uid       BIGINT   NOT NULL
                            REFERENCES items(item_uid)
                            ON DELETE CASCADE,
    option_type    SMALLINT NOT NULL CHECK (option_type BETWEEN 0 AND 255),
    option_index   SMALLINT NOT NULL,
    option_value   INTEGER  NOT NULL,
    mixed_item     INTEGER  NOT NULL DEFAULT 0,
    PRIMARY KEY (item_uid, option_type, option_index)
);

CREATE INDEX IF NOT EXISTS idx_item_options_item_uid ON item_options(item_uid);
