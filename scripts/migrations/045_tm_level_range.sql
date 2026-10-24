ALTER TABLE characters DROP CONSTRAINT IF EXISTS characters_tm_level_check;

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conrelid = 'characters'::regclass AND conname = 'characters_tm_level_range') THEN
        ALTER TABLE characters ADD CONSTRAINT characters_tm_level_range CHECK (tm_level BETWEEN 0 AND 500);
    END IF;
END $$;
