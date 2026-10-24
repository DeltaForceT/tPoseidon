DO $$
BEGIN
    IF EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = current_schema()
          AND table_name = 'unique_char_duty_info'
          AND column_name = 'duty_end_at'
    ) THEN
        ALTER TABLE unique_char_duty_info RENAME COLUMN duty_end_at TO duty_end_date;
    END IF;
END $$;
