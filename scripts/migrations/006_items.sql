CREATE TABLE IF NOT EXISTS items (
    item_uid       BIGSERIAL    PRIMARY KEY,
    item_index     INTEGER      NOT NULL,
    item_class     SMALLINT     NOT NULL,
    item_attr      SMALLINT     NOT NULL DEFAULT 0,
    item_charged   SMALLINT     NOT NULL DEFAULT 0,
    item_breaked   TIMESTAMPTZ  NOT NULL DEFAULT '2020-01-01',
    refine_type    SMALLINT     NOT NULL DEFAULT 0,
    refine_level   SMALLINT     NOT NULL DEFAULT 0,
    refine_state   SMALLINT     NOT NULL DEFAULT 0,
    item_slot      SMALLINT     NOT NULL DEFAULT 0,
    item_stack     INTEGER      NOT NULL DEFAULT 1
);

CREATE TABLE IF NOT EXISTS item_char (
    char_uid       BIGINT       NOT NULL REFERENCES characters(character_id) ON DELETE CASCADE,
    item_uid       BIGINT       NOT NULL UNIQUE REFERENCES items(item_uid) ON DELETE CASCADE,
    item_where     SMALLINT     NOT NULL,
    item_pos       SMALLINT     NOT NULL DEFAULT 0,
    PRIMARY KEY (char_uid, item_uid)
);

CREATE INDEX IF NOT EXISTS item_char_lookup_idx
    ON item_char (char_uid, item_where, item_pos);
