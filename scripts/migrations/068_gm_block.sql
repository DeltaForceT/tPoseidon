CREATE TABLE IF NOT EXISTS gm_block1 (
    uid_type     SMALLINT      NOT NULL,
    uid          BIGINT        NOT NULL,
    block_type   SMALLINT      NOT NULL,
    block_reason SMALLINT      NOT NULL DEFAULT 0,
    gm_uid_type  SMALLINT      NOT NULL,
    gm_uid       BIGINT        NOT NULL,
    cnt          SMALLINT      NOT NULL,
    block_from   TIMESTAMPTZ   NOT NULL,
    block_to     TIMESTAMPTZ   NOT NULL,
    block_desc   VARCHAR(1000) NOT NULL,
    note         VARCHAR(1000) NOT NULL,
    PRIMARY KEY (uid_type, uid, block_type)
);

CREATE TABLE IF NOT EXISTS gm_block_history1 (
    reg_date     TIMESTAMPTZ   NOT NULL DEFAULT now(),
    uid_type     SMALLINT      NOT NULL,
    uid          BIGINT        NOT NULL,
    block_type   SMALLINT      NOT NULL,
    block_reason SMALLINT      NOT NULL,
    gm_uid_type  SMALLINT      NOT NULL,
    gm_uid       BIGINT        NOT NULL,
    block_from   TIMESTAMPTZ   NOT NULL,
    block_to     TIMESTAMPTZ   NOT NULL,
    block_desc   VARCHAR(1000) NOT NULL,
    note         VARCHAR(1000) NOT NULL
);

CREATE INDEX IF NOT EXISTS gm_block_history1_uid_idx ON gm_block_history1 (uid_type, uid, block_type, block_to);

CREATE OR REPLACE FUNCTION gm_block_minute(at_time TIMESTAMPTZ) RETURNS TIMESTAMPTZ
LANGUAGE sql IMMUTABLE AS $$
    SELECT date_trunc('minute', at_time + INTERVAL '30.001 seconds')
$$;

CREATE OR REPLACE FUNCTION gsp_block_char(
    p_uid BIGINT,
    p_block_type SMALLINT,
    p_block_reason SMALLINT,
    p_gm_uid_type SMALLINT,
    p_gm_uid BIGINT,
    p_minutes INTEGER,
    p_desc VARCHAR,
    p_note VARCHAR) RETURNS INTEGER
LANGUAGE plpgsql AS $$
DECLARE
    v_now TIMESTAMPTZ := gm_block_minute(now());
    v_from TIMESTAMPTZ;
    v_to TIMESTAMPTZ;
    v_rows INTEGER;
BEGIN
    UPDATE characters SET char_authority = 1 WHERE character_id = p_uid;
    GET DIAGNOSTICS v_rows = ROW_COUNT;
    IF v_rows <> 1 THEN
        RETURN 8998;
    END IF;

    SELECT MAX(block_to) INTO v_from FROM gm_block1
     WHERE uid_type = 2 AND uid = p_uid AND block_type = p_block_type AND block_to > now();
    IF v_from IS NULL THEN
        v_from := v_now;
    ELSE
        v_from := v_from + INTERVAL '1 minute';
    END IF;
    v_to := v_from + make_interval(mins => p_minutes);

    IF EXISTS (SELECT 1 FROM gm_block1 WHERE uid_type = 2 AND uid = p_uid AND block_type = p_block_type) THEN
        UPDATE gm_block1
           SET gm_uid_type = p_gm_uid_type,
               gm_uid = p_gm_uid,
               cnt = cnt + 1,
               block_from = CASE WHEN block_to >= now() THEN block_from ELSE v_from END,
               block_to = v_to,
               block_desc = p_desc,
               note = p_note,
               block_reason = p_block_reason
         WHERE uid_type = 2 AND uid = p_uid AND block_type = p_block_type;
    ELSE
        INSERT INTO gm_block1 (uid_type, uid, block_type, block_reason, gm_uid_type, gm_uid, cnt, block_from, block_to, block_desc, note)
        VALUES (2, p_uid, p_block_type, p_block_reason, p_gm_uid_type, p_gm_uid, 1, v_from, v_to, p_desc, p_note);
    END IF;

    INSERT INTO gm_block_history1 (uid_type, uid, block_type, block_reason, gm_uid_type, gm_uid, block_from, block_to, block_desc, note)
    VALUES (2, p_uid, p_block_type, p_block_reason, p_gm_uid_type, p_gm_uid, v_from, v_to, p_desc, p_note);
    RETURN 0;
END
$$;

CREATE OR REPLACE FUNCTION gsp_unblock_char(
    p_uid BIGINT,
    p_block_type SMALLINT,
    p_gm_uid_type SMALLINT,
    p_gm_uid BIGINT) RETURNS INTEGER
LANGUAGE plpgsql AS $$
DECLARE
    v_cnt SMALLINT;
    v_from TIMESTAMPTZ;
    v_to TIMESTAMPTZ;
    v_prior gm_block_history1%ROWTYPE;
BEGIN
    SELECT block_from, block_to, cnt INTO v_from, v_to, v_cnt FROM gm_block1
     WHERE uid_type = 2 AND uid = p_uid AND block_type = p_block_type;
    IF v_from IS NULL THEN
        RETURN 8001;
    END IF;

    IF v_cnt < 2 THEN
        IF NOT EXISTS (SELECT 1 FROM characters WHERE character_id = p_uid) THEN
            RETURN 8998;
        END IF;
        UPDATE gm_block1
           SET block_to = TIMESTAMPTZ '1980-01-01 00:00:00',
               block_from = TIMESTAMPTZ '1980-01-01 00:00:00',
               cnt = 0,
               gm_uid_type = p_gm_uid_type,
               gm_uid = p_gm_uid,
               block_desc = 'Cancel',
               note = 'Cancel'
         WHERE uid_type = 2 AND uid = p_uid AND block_type = p_block_type;
        UPDATE characters SET char_authority = 0 WHERE character_id = p_uid;
        RETURN 0;
    END IF;

    SELECT * INTO v_prior FROM gm_block_history1
     WHERE uid_type = 2 AND uid = p_uid AND block_type = p_block_type AND block_to < v_to
     ORDER BY block_to DESC
     LIMIT 1;
    IF NOT FOUND THEN
        RETURN 8003;
    END IF;

    IF now() < v_prior.block_to THEN
        UPDATE gm_block1
           SET block_to = v_prior.block_to,
               cnt = cnt - 1,
               gm_uid_type = v_prior.gm_uid_type,
               gm_uid = v_prior.gm_uid,
               block_desc = v_prior.block_desc,
               note = v_prior.note,
               block_reason = v_prior.block_reason
         WHERE uid_type = 2 AND uid = p_uid AND block_type = p_block_type;
        RETURN 0;
    END IF;

    IF NOT EXISTS (SELECT 1 FROM characters WHERE character_id = p_uid) THEN
        RETURN 8998;
    END IF;
    UPDATE gm_block1
       SET block_to = v_prior.block_to,
           block_from = v_prior.block_from,
           cnt = cnt - 1,
           gm_uid_type = v_prior.gm_uid_type,
           gm_uid = v_prior.gm_uid,
           block_desc = v_prior.block_desc,
           note = v_prior.note,
           block_reason = v_prior.block_reason
     WHERE uid_type = 2 AND uid = p_uid AND block_type = p_block_type;
    UPDATE characters SET char_authority = 0 WHERE character_id = p_uid;
    RETURN 0;
END
$$;
