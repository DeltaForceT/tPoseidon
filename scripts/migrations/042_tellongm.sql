CREATE TABLE IF NOT EXISTS tellongm (
    tellongm_code       SERIAL        PRIMARY KEY,
    tellongm_island     SMALLINT      NOT NULL,
    tellongm_created    TIMESTAMPTZ   NOT NULL DEFAULT NOW(),
    send_char           BIGINT        NOT NULL,
    receive_char        BIGINT        NOT NULL,
    tellongm_title      VARCHAR(50)   NOT NULL,
    tellongm_desc       VARCHAR(200)  NOT NULL,
    tellongm_kind       SMALLINT      NOT NULL DEFAULT 1,
    handling_type       SMALLINT      NOT NULL DEFAULT 1,
    is_still            BOOLEAN       NOT NULL DEFAULT FALSE,
    handling_gm         VARCHAR(13)   NULL,
    handling_date       TIMESTAMPTZ   NULL,
    handling_desc       VARCHAR(100)  NULL
);

CREATE INDEX IF NOT EXISTS tellongm_created_idx ON tellongm (tellongm_created);

CREATE TABLE IF NOT EXISTS tellongm_chatting (
    tellongm_code       INTEGER       NOT NULL REFERENCES tellongm(tellongm_code) ON DELETE CASCADE,
    chatting_no         SMALLINT      NOT NULL,
    chatting            VARCHAR(5000) NOT NULL
);

CREATE INDEX IF NOT EXISTS tellongm_chatting_code_idx ON tellongm_chatting (tellongm_code);
