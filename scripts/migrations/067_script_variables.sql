CREATE TABLE IF NOT EXISTS script_variable (
    scope         SMALLINT     NOT NULL,
    owner_id      BIGINT       NOT NULL,
    name          VARCHAR(64)  NOT NULL,
    number_value  BIGINT       NOT NULL DEFAULT 0,
    text_value    VARCHAR(2048),
    updated_at    TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    PRIMARY KEY (scope, owner_id, name)
);
