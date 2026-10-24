CREATE TABLE IF NOT EXISTS unique_item_info (
    item_index       INTEGER      PRIMARY KEY,
    unique_type      SMALLINT     NOT NULL,
    limit_count      INTEGER      NOT NULL,
    total_count      INTEGER      NOT NULL DEFAULT 0,
    server_count     INTEGER      NOT NULL DEFAULT 0,
    user_count       INTEGER      NOT NULL DEFAULT 0,
    base_count       INTEGER      NOT NULL,
    current_count    INTEGER      NOT NULL DEFAULT 0,
    update_flag      CHAR(1)      NOT NULL DEFAULT 'A',
    created_date     TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    day_limit_count  INTEGER      NOT NULL DEFAULT 0
);

CREATE TABLE IF NOT EXISTS unique_item_target (
    item_index    INTEGER   NOT NULL REFERENCES unique_item_info(item_index) ON DELETE CASCADE,
    target_order  SMALLINT  NOT NULL,
    target_count  INTEGER   NOT NULL,
    target_flag   SMALLINT  NOT NULL DEFAULT 0,
    PRIMARY KEY (item_index, target_order)
);

CREATE TABLE IF NOT EXISTS unique_char_duty_info (
    char_uid            BIGINT       PRIMARY KEY,
    bossmonmedal_count  SMALLINT     NOT NULL DEFAULT 0,
    unique_item_count   SMALLINT     NOT NULL DEFAULT 0,
    duty_end_date       TIMESTAMPTZ  NOT NULL
);

CREATE TABLE IF NOT EXISTS unique_item_char (
    item_uid     BIGINT       PRIMARY KEY,
    item_index   INTEGER      NOT NULL,
    char_uid     BIGINT       NOT NULL,
    pickup_date  TIMESTAMPTZ  NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS unique_item_char_char_idx ON unique_item_char (char_uid);

CREATE TABLE IF NOT EXISTS unique_item_log (
    log_seq        BIGSERIAL    PRIMARY KEY,
    log_type       SMALLINT     NOT NULL,
    item_index     INTEGER      NOT NULL,
    char_uid       BIGINT       NOT NULL,
    current_count  INTEGER      NOT NULL,
    target_order   SMALLINT     NULL,
    log_date       TIMESTAMPTZ  NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS unique_item_duty_log (
    log_seq                    BIGSERIAL    PRIMARY KEY,
    log_type                   SMALLINT     NOT NULL,
    char_uid                   BIGINT       NOT NULL,
    bossmonmedal_count         SMALLINT     NOT NULL,
    unique_item_count          SMALLINT     NOT NULL,
    unique_item_deprive_count  SMALLINT     NOT NULL,
    duty_end_date              TIMESTAMPTZ  NOT NULL,
    log_date                   TIMESTAMPTZ  NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS unique_item_schedule_log (
    log_seq             BIGSERIAL    PRIMARY KEY,
    log_type            SMALLINT     NOT NULL,
    start_time          TIMESTAMPTZ  NOT NULL,
    end_time            TIMESTAMPTZ  NOT NULL,
    duty_success_count  INTEGER      NOT NULL,
    duty_fail_count     INTEGER      NOT NULL,
    created_date        TIMESTAMPTZ  NOT NULL DEFAULT NOW()
);

INSERT INTO unique_item_info (item_index, unique_type, limit_count, base_count, day_limit_count) VALUES
    (50000, 1, 5, 500000, 0),
    (50001, 2, 99999, 100, 0),
    (50004, 2, 99999, 100, 0),
    (50005, 2, 99999, 100, 0),
    (50006, 1, 5, 700000, 0),
    (50007, 2, 99999, 100, 0),
    (50008, 2, 99999, 100, 0),
    (50009, 2, 99999, 100, 0),
    (50012, 2, 99999, 100, 0),
    (50014, 2, 99999, 100, 0),
    (50015, 2, 99999, 100, 0),
    (50016, 2, 99999, 100, 0),
    (50017, 2, 99999, 100, 0),
    (50018, 2, 99999, 100, 0),
    (50019, 2, 99999, 100, 0),
    (50020, 2, 99999, 100, 0),
    (50021, 2, 99999, 100, 0),
    (50022, 2, 99999, 100, 0),
    (50023, 2, 99999, 100, 0),
    (50024, 2, 99999, 100, 0),
    (50025, 2, 99999, 100, 0),
    (50026, 2, 99999, 100, 0),
    (50027, 2, 99999, 100, 0),
    (50028, 2, 99999, 100, 0),
    (50032, 2, 99999, 100, 0),
    (50033, 2, 99999, 100, 0),
    (50034, 2, 99999, 100, 0),
    (52000, 2, 99999, 100, 0),
    (52001, 2, 99999, 100, 0),
    (52002, 2, 99999, 100, 0),
    (52004, 2, 99999, 100, 0),
    (54000, 1, 5, 500000, 0),
    (54001, 2, 99999, 100, 0),
    (54003, 2, 99999, 100, 0),
    (54004, 2, 99999, 100, 0),
    (54005, 2, 99999, 100, 0),
    (54006, 1, 5, 700000, 0),
    (54007, 2, 99999, 100, 0),
    (54008, 2, 99999, 100, 0),
    (54009, 2, 99999, 100, 0),
    (54012, 2, 99999, 100, 0),
    (54013, 2, 99999, 100, 0),
    (54014, 2, 99999, 100, 0),
    (54016, 2, 99999, 100, 0),
    (56000, 1, 5, 500000, 0),
    (56001, 2, 99999, 100, 0),
    (56004, 2, 99999, 100, 0),
    (56006, 1, 5, 700000, 0),
    (56007, 2, 99999, 100, 0),
    (56009, 2, 99999, 100, 0),
    (56012, 2, 99999, 100, 0),
    (56013, 2, 99999, 100, 0),
    (56014, 2, 99999, 100, 0),
    (56016, 2, 99999, 100, 0),
    (58005, 2, 99999, 100, 0),
    (58012, 2, 99999, 100, 0),
    (58014, 2, 99999, 100, 0)
ON CONFLICT (item_index) DO NOTHING;
