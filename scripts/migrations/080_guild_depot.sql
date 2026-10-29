ALTER TABLE guild
    ADD COLUMN IF NOT EXISTS depot_tabs SMALLINT NOT NULL DEFAULT 0 CHECK (depot_tabs BETWEEN 0 AND 10);

CREATE TABLE IF NOT EXISTS guild_depot_item (
    item_uid    BIGINT   PRIMARY KEY REFERENCES items(item_uid) ON DELETE CASCADE,
    guild_code  BIGINT   NOT NULL REFERENCES guild(guild_code),
    tab         SMALLINT NOT NULL CHECK (tab BETWEEN 1 AND 10),
    entry_seq   BIGSERIAL NOT NULL
);

CREATE INDEX IF NOT EXISTS guild_depot_item_tab_idx ON guild_depot_item (guild_code, tab, entry_seq);

CREATE TABLE IF NOT EXISTS guild_depot_history (
    history_seq   BIGSERIAL    PRIMARY KEY,
    guild_code    BIGINT       NOT NULL REFERENCES guild(guild_code) ON DELETE CASCADE,
    char_name     VARCHAR(32)  NOT NULL,
    item_index    INTEGER      NOT NULL,
    item_count    INTEGER      NOT NULL CHECK (item_count > 0),
    action        SMALLINT     NOT NULL CHECK (action IN (0, 1)),
    created_at    TIMESTAMPTZ  NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS guild_depot_history_guild_idx ON guild_depot_history (guild_code, history_seq DESC);
