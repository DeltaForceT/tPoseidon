DO $$
BEGIN
    IF (SELECT data_type FROM information_schema.columns
        WHERE table_schema = current_schema() AND table_name = 'characters' AND column_name = 'hp') <> 'bigint' THEN
        ALTER TABLE characters ALTER COLUMN hp TYPE BIGINT;
    END IF;
    IF (SELECT data_type FROM information_schema.columns
        WHERE table_schema = current_schema() AND table_name = 'characters' AND column_name = 'mp') <> 'bigint' THEN
        ALTER TABLE characters ALTER COLUMN mp TYPE BIGINT;
    END IF;
    IF (SELECT data_type FROM information_schema.columns
        WHERE table_schema = current_schema() AND table_name = 'characters' AND column_name = 'tm_exp') <> 'bigint' THEN
        ALTER TABLE characters ALTER COLUMN tm_exp TYPE BIGINT;
    END IF;
    IF EXISTS (SELECT 1 FROM pg_constraint
               WHERE conname = 'characters_money_check' AND conrelid = 'characters'::regclass) THEN
        ALTER TABLE characters DROP CONSTRAINT characters_money_check;
        ALTER TABLE characters ADD CONSTRAINT characters_money_nonnegative CHECK (money >= 0);
    END IF;
END
$$;
