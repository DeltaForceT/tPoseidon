CREATE TABLE IF NOT EXISTS penalty (
    penalty_code SMALLINT     PRIMARY KEY,
    penalty_name VARCHAR(50)  NOT NULL,
    penalty_kind SMALLINT     NOT NULL,
    penalty_type SMALLINT     NOT NULL DEFAULT 1
);

CREATE TABLE IF NOT EXISTS penalty_time (
    penalty_code  SMALLINT NOT NULL,
    penalty_count SMALLINT NOT NULL,
    penalty_time  INTEGER  NOT NULL,
    PRIMARY KEY (penalty_code, penalty_count)
);

CREATE TABLE IF NOT EXISTS penalty_user (
    seq           BIGSERIAL    PRIMARY KEY,
    account_id    BIGINT       NOT NULL,
    penalty_code  SMALLINT     NOT NULL,
    penalty_count SMALLINT     NOT NULL,
    apply_date    TIMESTAMPTZ  NOT NULL,
    penalty_time  INTEGER      NOT NULL,
    release_date  TIMESTAMPTZ  NOT NULL,
    released_date TIMESTAMPTZ  NULL,
    gm_account_id BIGINT       NOT NULL,
    penalty_world SMALLINT     NOT NULL,
    tellongm_code INTEGER      NULL,
    penalty_note  VARCHAR(200) NULL
);

CREATE INDEX IF NOT EXISTS penalty_user_running_idx ON penalty_user (account_id, release_date) WHERE released_date IS NULL;

CREATE OR REPLACE FUNCTION uspt_penalty_user_insert_block(
    p_penalty_world SMALLINT,
    p_tellongm_code INTEGER,
    p_account_gid VARCHAR,
    p_penalty_code SMALLINT,
    p_gm_account_id BIGINT,
    p_penalty_note VARCHAR) RETURNS INTEGER
LANGUAGE plpgsql AS $$
DECLARE
    v_account BIGINT;
    v_access SMALLINT;
    v_code SMALLINT := p_penalty_code;
    v_note VARCHAR := p_penalty_note;
    v_count SMALLINT;
    v_time INTEGER;
    v_total INTEGER;
    v_name VARCHAR;
    v_apply TIMESTAMPTZ := gm_block_minute(now());
BEGIN
    SELECT account_id, acc_game_access INTO v_account, v_access FROM accounts WHERE username = p_account_gid;
    IF v_account IS NULL THEN
        RETURN 100;
    ELSIF v_access = 1 THEN
        RETURN 101;
    ELSIF v_access = 2 THEN
        RETURN 102;
    END IF;

    SELECT COALESCE(MAX(penalty_count), 0) INTO v_count
      FROM penalty_user WHERE account_id = v_account AND penalty_code = v_code;

    SELECT penalty_time INTO v_time
      FROM penalty_time WHERE penalty_code = v_code AND penalty_count = v_count + 1;
    IF v_time IS NULL THEN
        v_time := 9999;
    END IF;

    SELECT COALESCE(SUM(penalty_count), 0) INTO v_total FROM penalty_user WHERE account_id = v_account;
    IF v_total >= 4 THEN
        SELECT penalty_name INTO v_name FROM penalty WHERE penalty_code = p_penalty_code;
        IF v_name IS NOT NULL THEN
            v_note := v_name || ', ' || p_penalty_note;
        END IF;
        v_code := 1018;
        v_time := 9999;
        v_count := 0;
    END IF;

    INSERT INTO penalty_user (account_id, penalty_code, penalty_count, apply_date, penalty_time, release_date,
                              released_date, gm_account_id, penalty_world, tellongm_code, penalty_note)
    VALUES (v_account, v_code, v_count + 1, v_apply, v_time, v_apply + v_time * INTERVAL '1 hour',
            NULL, p_gm_account_id, p_penalty_world, p_tellongm_code, v_note);

    UPDATE accounts SET acc_game_access = 1 WHERE account_id = v_account;
    RETURN 0;
END
$$;

CREATE OR REPLACE FUNCTION uspt_penalty_user_update_release_by_gm(
    p_gm_id VARCHAR,
    p_account_id BIGINT,
    p_release_date TIMESTAMPTZ) RETURNS INTEGER
LANGUAGE plpgsql AS $$
DECLARE
    v_rows INTEGER;
BEGIN
    UPDATE penalty_user
       SET released_date = now(),
           penalty_note = substring(COALESCE(penalty_note, '') || ', Released by GM (' || p_gm_id || ')' FROM 1 FOR 100)
     WHERE account_id = p_account_id AND release_date = p_release_date;
    GET DIAGNOSTICS v_rows = ROW_COUNT;
    IF v_rows = 0 THEN
        RETURN 100;
    END IF;

    UPDATE accounts SET acc_game_access = 0 WHERE account_id = p_account_id;
    RETURN 0;
END
$$;

CREATE OR REPLACE FUNCTION uspj_penalty_user_update_release() RETURNS INTEGER
LANGUAGE plpgsql AS $$
DECLARE
    v_today TIMESTAMPTZ := now();
    v_rows INTEGER;
BEGIN
    WITH released AS (
        UPDATE penalty_user
           SET released_date = v_today
         WHERE released_date IS NULL AND release_date <= v_today
        RETURNING account_id)
    UPDATE accounts SET acc_game_access = 0
     WHERE account_id IN (SELECT account_id FROM released);
    GET DIAGNOSTICS v_rows = ROW_COUNT;
    RETURN v_rows;
END
$$;
