DO $$
BEGIN
    IF EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_name = 'characters' AND column_name = 'starter_hp_potion'
    ) THEN
        ALTER TABLE characters RENAME COLUMN starter_hp_potion TO starter_ear;
    END IF;

    IF EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_name = 'characters' AND column_name = 'starter_mp_potion'
    ) THEN
        ALTER TABLE characters RENAME COLUMN starter_mp_potion TO starter_tail;
    END IF;
END $$;

UPDATE characters
   SET starter_ear  = 6900 + (species - 1),
       starter_tail = 6950 + (species - 1)
 WHERE starter_ear  = 6905
   AND starter_tail = 6955;
