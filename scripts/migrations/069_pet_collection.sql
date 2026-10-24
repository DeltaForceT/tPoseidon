CREATE TABLE IF NOT EXISTS pet_collection_item (
    collected_uid BIGSERIAL   PRIMARY KEY,
    char_uid      BIGINT      NOT NULL REFERENCES characters(character_id) ON DELETE CASCADE,
    item_id       INTEGER     NOT NULL,
    item_class    SMALLINT    NOT NULL,
    item_count    INTEGER     NOT NULL CHECK (item_count > 0),
    collected_at  TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS pet_collection_item_char_idx ON pet_collection_item (char_uid, collected_uid);
