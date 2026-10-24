ALTER TABLE characters DROP CONSTRAINT IF EXISTS characters_species_check;
ALTER TABLE characters ADD CONSTRAINT characters_species_check
    CHECK (species BETWEEN 1 AND 9);
