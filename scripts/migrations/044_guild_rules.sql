ALTER TABLE guild
    ADD COLUMN IF NOT EXISTS guild_max_members SMALLINT NOT NULL DEFAULT 64,
    ADD COLUMN IF NOT EXISTS icon_purchase     SMALLINT NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS icon_times        SMALLINT NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS icon_background   SMALLINT NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS icon_color        SMALLINT NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS icon_item         INTEGER  NOT NULL DEFAULT 0;

ALTER TABLE guild ALTER COLUMN guild_pw SET DEFAULT '0000';

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conrelid = 'guild'::regclass AND conname = 'guild_name_min_len') THEN
        ALTER TABLE guild ADD CONSTRAINT guild_name_min_len CHECK (char_length(guild_name) >= 2);
    END IF;
END $$;

CREATE UNIQUE INDEX IF NOT EXISTS guild_master_account_uq ON guild (master_account_id);
CREATE UNIQUE INDEX IF NOT EXISTS guild_master_char_uq ON guild (master_char_uid);

DO $$
BEGIN
    IF EXISTS (
        SELECT 1 FROM pg_constraint c
        WHERE c.conrelid = 'guild_member'::regclass AND c.contype = 'p' AND array_length(c.conkey, 1) <> 1)
    THEN
        ALTER TABLE guild_member DROP CONSTRAINT guild_member_pkey;
        ALTER TABLE guild_member ADD PRIMARY KEY (char_uid);
    END IF;
END $$;

CREATE INDEX IF NOT EXISTS guild_member_guild_idx ON guild_member (guild_code);

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conrelid = 'guild_member'::regclass AND conname = 'guild_member_grade_fk') THEN
        ALTER TABLE guild_member ADD CONSTRAINT guild_member_grade_fk
            FOREIGN KEY (guild_code, grade_code) REFERENCES guild_grade (guild_code, grade_code);
    END IF;
END $$;

ALTER TABLE guild_member_standby ADD COLUMN IF NOT EXISTS standby_seq BIGSERIAL;

DO $$
BEGIN
    IF EXISTS (
        SELECT 1 FROM pg_constraint c
        WHERE c.conrelid = 'guild_member_standby'::regclass AND c.contype = 'p' AND array_length(c.conkey, 1) <> 1)
    THEN
        ALTER TABLE guild_member_standby DROP CONSTRAINT guild_member_standby_pkey;
        ALTER TABLE guild_member_standby ADD PRIMARY KEY (standby_seq);
    END IF;
END $$;

CREATE INDEX IF NOT EXISTS guild_member_standby_char_idx ON guild_member_standby (char_uid);
CREATE INDEX IF NOT EXISTS guild_member_standby_state_idx ON guild_member_standby (guild_code, standby_state);

ALTER TABLE guild_ladder ALTER COLUMN guild_ladder SET DEFAULT 0;
