ALTER TABLE bossmon ADD COLUMN IF NOT EXISTS kill_date TIMESTAMPTZ;

UPDATE bossmon b
SET kill_date = COALESCE(
    (SELECT MIN(h.kill_date) FROM bossmon_history h WHERE h.char_uid = b.char_uid AND h.mon_id = b.mon_id),
    b.latest_update)
WHERE b.kill_date IS NULL;

ALTER TABLE bossmon ALTER COLUMN kill_date SET NOT NULL;
ALTER TABLE bossmon ALTER COLUMN kill_date SET DEFAULT NOW();
