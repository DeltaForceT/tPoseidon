ALTER TABLE characters DROP CONSTRAINT IF EXISTS characters_slot_check;
ALTER TABLE characters ADD CONSTRAINT characters_slot_check
    CHECK (slot BETWEEN 0 AND 7);
