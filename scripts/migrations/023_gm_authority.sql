CREATE TABLE IF NOT EXISTS tbl_gm_authority (
    authority_code   INTEGER     NOT NULL,
    authority_tool   TEXT        NOT NULL,
    authority_usenum SMALLINT    NOT NULL DEFAULT 0,
    authority_name   TEXT        NOT NULL,
    authority_note   TEXT        NULL,
    PRIMARY KEY (authority_code)
);

CREATE TABLE IF NOT EXISTS tbl_gm_authority_list (
    account_code     BIGINT      NOT NULL,
    authority_code   INTEGER     NOT NULL,
    group_name       TEXT        NULL,
    created_date     TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    authority_right  SMALLINT    NOT NULL DEFAULT 0,
    PRIMARY KEY (account_code, authority_code)
);

CREATE TABLE IF NOT EXISTS tbl_gm_char (
    account_code     BIGINT      NOT NULL,
    char_name        TEXT        NOT NULL,
    permitted_ip     TEXT        NOT NULL,
    created_date     TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    PRIMARY KEY (account_code, char_name)
);

CREATE TABLE IF NOT EXISTS tbl_gm_char_permit_ip (
    account_code     BIGINT      NULL,
    permit_ip        TEXT        NULL
);
CREATE INDEX IF NOT EXISTS ix_gm_char_permit_ip_account
    ON tbl_gm_char_permit_ip (account_code);
