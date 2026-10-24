ALTER TABLE couple
    ADD COLUMN IF NOT EXISTS begin_couple_date      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    ADD COLUMN IF NOT EXISTS begin_marriage_date    TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    ADD COLUMN IF NOT EXISTS divorce_request_date   TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    ADD COLUMN IF NOT EXISTS divorce_completed_date TIMESTAMPTZ NOT NULL DEFAULT NOW();

ALTER TABLE couple ALTER COLUMN love_point SET DEFAULT 50;
ALTER TABLE couple ALTER COLUMN friend_point SET DEFAULT 50;

ALTER TABLE couple_char DROP CONSTRAINT IF EXISTS couple_char_couple_code_fkey;
ALTER TABLE couple_char ALTER COLUMN couple_code SET DEFAULT 0;

CREATE TABLE IF NOT EXISTS couple_status_log (
    log_seq      BIGSERIAL   PRIMARY KEY,
    couple_code  BIGINT      NOT NULL,
    couple_state SMALLINT    NOT NULL,
    log_at       TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS couple_status_log_at_idx ON couple_status_log (log_at);

CREATE TABLE IF NOT EXISTS wedding_in_log (
    log_seq     BIGSERIAL   PRIMARY KEY,
    couple_code BIGINT      NOT NULL,
    char_uid    BIGINT      NOT NULL,
    created_at  TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS couple_skill_log (
    log_seq       BIGSERIAL   PRIMARY KEY,
    couple_code   BIGINT      NOT NULL,
    char_uid      BIGINT      NOT NULL,
    char_sex      SMALLINT    NULL,
    skill_id      SMALLINT    NOT NULL,
    skill_level   SMALLINT    NOT NULL,
    skill_updated TIMESTAMPTZ NULL,
    action        SMALLINT    NOT NULL CHECK (action IN (0, 1)),
    created_at    TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
