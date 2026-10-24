CREATE TABLE IF NOT EXISTS char_soulseed_log (
    log_seq                  BIGSERIAL    PRIMARY KEY,
    char_uid                 BIGINT       NOT NULL,
    seed_item_index          INTEGER      NOT NULL,
    power_point              SMALLINT     NOT NULL,
    magic_point              SMALLINT     NOT NULL,
    sense_point              SMALLINT     NOT NULL,
    charm_point              SMALLINT     NOT NULL,
    special_power_point      SMALLINT     NOT NULL,
    special_magic_point      SMALLINT     NOT NULL,
    special_sense_point      SMALLINT     NOT NULL,
    special_charm_point      SMALLINT     NOT NULL,
    start_at                 TIMESTAMPTZ  NOT NULL,
    last_update_at           TIMESTAMPTZ  NOT NULL,
    logged_at                TIMESTAMPTZ  NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS char_soulseed_log_char_idx ON char_soulseed_log (char_uid, logged_at DESC);
