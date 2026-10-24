CREATE TABLE IF NOT EXISTS guild_game_match (
    match_code     BIGSERIAL   PRIMARY KEY,
    match_begin    TIMESTAMPTZ NOT NULL,
    match_end      TIMESTAMPTZ NOT NULL,
    match_type     SMALLINT    NOT NULL,
    match_map      INTEGER     NOT NULL,
    match_timeout  SMALLINT    NOT NULL,
    winning_count  INTEGER     NOT NULL,
    etc_num        SMALLINT    NOT NULL DEFAULT 0
);

CREATE INDEX IF NOT EXISTS guild_game_match_begin_idx ON guild_game_match (match_begin);

INSERT INTO guild_game_match (match_code, match_begin, match_end, match_type, match_map, match_timeout, winning_count, etc_num)
SELECT DISTINCT m.match_code, m.matched_at, m.matched_at, 0, 0, 0, 0, 0
FROM guild_match AS m
WHERE NOT EXISTS (SELECT 1 FROM guild_game_match AS g WHERE g.match_code = m.match_code);

SELECT setval(
    pg_get_serial_sequence('guild_game_match', 'match_code'),
    GREATEST((SELECT COALESCE(MAX(match_code), 0) FROM guild_game_match), 1),
    EXISTS (SELECT 1 FROM guild_game_match));

ALTER TABLE guild_match ALTER COLUMN match_code DROP DEFAULT;

DO $$
BEGIN
    IF EXISTS (
        SELECT 1 FROM pg_constraint c
        WHERE c.conrelid = 'guild_match'::regclass AND c.contype = 'p' AND array_length(c.conkey, 1) = 1)
    THEN
        ALTER TABLE guild_match DROP CONSTRAINT guild_match_pkey;
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conrelid = 'guild_match'::regclass AND contype = 'p') THEN
        ALTER TABLE guild_match ADD PRIMARY KEY (match_code, guild_code);
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conrelid = 'guild_match'::regclass AND conname = 'guild_match_game_fk') THEN
        ALTER TABLE guild_match ADD CONSTRAINT guild_match_game_fk
            FOREIGN KEY (match_code) REFERENCES guild_game_match (match_code) ON DELETE CASCADE;
    END IF;
END $$;

DROP SEQUENCE IF EXISTS guild_match_match_code_seq;
