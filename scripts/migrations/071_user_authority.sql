ALTER TABLE accounts ADD COLUMN IF NOT EXISTS user_authority SMALLINT NOT NULL DEFAULT 0;

CREATE OR REPLACE FUNCTION usp_update_user_authority_by_char_name(
    p_char_name VARCHAR,
    p_authority SMALLINT) RETURNS INTEGER
LANGUAGE plpgsql AS $$
DECLARE
    v_owner BIGINT;
    v_rows INTEGER;
BEGIN
    SELECT account_id INTO v_owner FROM characters WHERE name = p_char_name LIMIT 1;
    IF v_owner IS NULL THEN
        RETURN 0;
    END IF;

    UPDATE accounts SET user_authority = p_authority WHERE account_id = v_owner;
    GET DIAGNOSTICS v_rows = ROW_COUNT;
    RETURN v_rows;
END
$$;
