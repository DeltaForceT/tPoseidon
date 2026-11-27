DO $$
DECLARE
    dup RECORD;
    attempt INTEGER;
    suffix TEXT;
    stem TEXT;
    candidate TEXT;
BEGIN
    FOR dup IN
        SELECT character_id, name
        FROM (
            SELECT character_id, name,
                   row_number() OVER (PARTITION BY lower(name) ORDER BY created_at, character_id) AS rank
            FROM characters
        ) ranked
        WHERE rank > 1
        ORDER BY character_id
    LOOP
        attempt := 1;
        LOOP
            suffix := attempt::TEXT;
            stem := dup.name;
            WHILE octet_length(stem) > 16 - length(suffix) LOOP
                stem := left(stem, length(stem) - 1);
            END LOOP;
            candidate := stem || suffix;
            EXIT WHEN NOT EXISTS (SELECT 1 FROM characters WHERE lower(name) = lower(candidate));
            attempt := attempt + 1;
        END LOOP;
        UPDATE characters SET name = candidate WHERE character_id = dup.character_id;
        INSERT INTO char_name_history (char_uid, old_name, new_name, reason)
        VALUES (dup.character_id, dup.name, candidate, 'duplicate name');
    END LOOP;
END
$$;

CREATE UNIQUE INDEX IF NOT EXISTS characters_name_lower_key ON characters (lower(name));
