CREATE TABLE IF NOT EXISTS gm_rate_event (
    event_kind     SMALLINT NOT NULL CHECK (event_kind IN (0, 1)),
    event_list     SMALLINT NOT NULL CHECK (event_list IN (0, 1)),
    position       INTEGER  NOT NULL CHECK (position >= 0),
    stat           SMALLINT NOT NULL,
    first_percent  INTEGER  NOT NULL,
    second_percent INTEGER  NOT NULL,
    start_time     BIGINT   NOT NULL,
    end_time       BIGINT   NOT NULL,
    PRIMARY KEY (event_kind, event_list, position)
);

CREATE TABLE IF NOT EXISTS gm_rate_direct (
    id          SMALLINT PRIMARY KEY CHECK (id = 1),
    exp_percent INTEGER  NOT NULL,
    tm_percent  INTEGER  NOT NULL
);
