CREATE TABLE IF NOT EXISTS item_option (
    item_uid    BIGINT    NOT NULL REFERENCES items(item_uid) ON DELETE CASCADE,
    position    SMALLINT  NOT NULL,
    prop_type   SMALLINT  NOT NULL,
    prop_id     INTEGER   NOT NULL,
    prop_value  SMALLINT  NOT NULL,
    prop_extra  BIGINT    NOT NULL DEFAULT 0,
    PRIMARY KEY (item_uid, position)
);
