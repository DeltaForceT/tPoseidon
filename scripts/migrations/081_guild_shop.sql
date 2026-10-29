ALTER TABLE guild
    ADD COLUMN IF NOT EXISTS guild_coin INTEGER NOT NULL DEFAULT 0 CHECK (guild_coin >= 0);

CREATE TABLE IF NOT EXISTS guild_shop_history (
    history_seq  BIGSERIAL    PRIMARY KEY,
    guild_code   BIGINT       NOT NULL REFERENCES guild(guild_code) ON DELETE CASCADE,
    char_name    VARCHAR(32)  NOT NULL,
    goods_index  INTEGER      NOT NULL,
    coin_cost    INTEGER      NOT NULL CHECK (coin_cost >= 0),
    created_at   TIMESTAMPTZ  NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS guild_shop_history_guild_idx ON guild_shop_history (guild_code, history_seq DESC);
