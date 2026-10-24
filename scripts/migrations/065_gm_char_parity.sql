DELETE FROM tbl_gm_authority WHERE authority_tool IN ('Y', 'N');

DO $$
DECLARE
    primary_key_name TEXT;
    primary_key_columns INTEGER;
BEGIN
    SELECT c.conname, array_length(c.conkey, 1)
      INTO primary_key_name, primary_key_columns
      FROM pg_constraint AS c
      JOIN pg_class AS t ON t.oid = c.conrelid
      JOIN pg_namespace AS n ON n.oid = t.relnamespace
     WHERE n.nspname = current_schema()
       AND t.relname = 'tbl_gm_char'
       AND c.contype = 'p';

    IF primary_key_columns IS DISTINCT FROM 1 THEN
        DELETE FROM tbl_gm_char AS older
         USING tbl_gm_char AS newer
         WHERE older.account_code = newer.account_code
           AND (older.created_date, older.char_name) < (newer.created_date, newer.char_name);
        IF primary_key_name IS NOT NULL THEN
            EXECUTE format('ALTER TABLE tbl_gm_char DROP CONSTRAINT %I', primary_key_name);
        END IF;
        ALTER TABLE tbl_gm_char ADD PRIMARY KEY (account_code);
    END IF;
END $$;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint AS c
          JOIN pg_class AS t ON t.oid = c.conrelid
          JOIN pg_namespace AS n ON n.oid = t.relnamespace
         WHERE n.nspname = current_schema()
           AND t.relname = 'tbl_gm_char'
           AND c.conname = 'fk_tbl_gm_char_account_code'
    ) THEN
        DELETE FROM tbl_gm_char WHERE account_code NOT IN (SELECT account_id FROM accounts);
        ALTER TABLE tbl_gm_char
            ADD CONSTRAINT fk_tbl_gm_char_account_code
            FOREIGN KEY (account_code) REFERENCES accounts (account_id);
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint AS c
          JOIN pg_class AS t ON t.oid = c.conrelid
          JOIN pg_namespace AS n ON n.oid = t.relnamespace
         WHERE n.nspname = current_schema()
           AND t.relname = 'tbl_gm_char_permit_ip'
           AND c.conname = 'fk_tbl_gm_char_permit_ip_account_code'
    ) THEN
        DELETE FROM tbl_gm_char_permit_ip WHERE account_code NOT IN (SELECT account_code FROM tbl_gm_char);
        ALTER TABLE tbl_gm_char_permit_ip
            ADD CONSTRAINT fk_tbl_gm_char_permit_ip_account_code
            FOREIGN KEY (account_code) REFERENCES tbl_gm_char (account_code);
    END IF;
END $$;

DO $$
BEGIN
    IF EXISTS (
        SELECT 1 FROM information_schema.columns
         WHERE table_schema = current_schema()
           AND table_name = 'accounts'
           AND column_name = 'acc_game_authority'
           AND data_type = 'smallint'
    ) THEN
        ALTER TABLE accounts ALTER COLUMN acc_game_authority DROP DEFAULT;
        ALTER TABLE accounts ALTER COLUMN acc_game_authority TYPE CHAR(1) USING 'A';
        ALTER TABLE accounts ALTER COLUMN acc_game_authority SET DEFAULT 'A';
    END IF;
END $$;
