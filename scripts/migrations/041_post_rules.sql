UPDATE post_date SET return_at = created_at + INTERVAL '7 days' WHERE return_at IS NULL;

ALTER TABLE post_date ALTER COLUMN return_at SET NOT NULL;

DO $$
BEGIN
    IF (SELECT character_maximum_length FROM information_schema.columns
        WHERE table_schema = current_schema() AND table_name = 'post' AND column_name = 'title') <> 50 THEN
        ALTER TABLE post
            ALTER COLUMN title TYPE VARCHAR(50) USING LEFT(title, 50),
            ALTER COLUMN content TYPE VARCHAR(300) USING LEFT(content, 300),
            ALTER COLUMN post_money TYPE INTEGER,
            ALTER COLUMN post_money_tax TYPE INTEGER;
    END IF;
END
$$;

CREATE INDEX IF NOT EXISTS post_state_idx ON post (post_uid, post_state);
CREATE INDEX IF NOT EXISTS post_created_idx ON post (created_at);
CREATE INDEX IF NOT EXISTS post_date_post_state_idx ON post_date (post_uid, post_state);
CREATE INDEX IF NOT EXISTS post_date_account_idx ON post_date (account_id);
CREATE INDEX IF NOT EXISTS post_item_item_idx ON post_item (item_uid);

CREATE OR REPLACE FUNCTION post_keep_until(from_at TIMESTAMPTZ, keep INTEGER) RETURNS TIMESTAMPTZ
LANGUAGE sql STABLE AS $$
    SELECT CASE keep
        WHEN 0 THEN TIMESTAMPTZ '2050-01-01 00:00:00+00'
        WHEN 1 THEN date_trunc('minute', from_at) + INTERVAL '7 days'
        WHEN 2 THEN date_trunc('minute', from_at) + INTERVAL '1 day'
        ELSE date_trunc('minute', from_at) + INTERVAL '3 days'
    END
$$;
