DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = current_schema()
          AND table_name   = 'characters'
          AND column_name  = 'pts_p'
    ) THEN
        RAISE NOTICE 'migration 015: characters.pts_p missing in schema %, skipping (run migration 014 first).', current_schema();
        RETURN;
    END IF;

    UPDATE characters SET
        pts_p = CASE ftype WHEN 1 THEN 4 ELSE 2 END,
        pts_m = CASE ftype WHEN 2 THEN 4 ELSE 2 END,
        pts_s = CASE ftype WHEN 3 THEN 4 ELSE 2 END,
        pts_c = CASE ftype WHEN 4 THEN 4 ELSE 2 END
    WHERE

        (ftype = 1 AND pts_p = 6 AND pts_m = 3 AND pts_s = 3 AND pts_c = 3)
     OR (ftype = 2 AND pts_p = 3 AND pts_m = 6 AND pts_s = 3 AND pts_c = 3)
     OR (ftype = 3 AND pts_p = 3 AND pts_m = 3 AND pts_s = 6 AND pts_c = 3)
     OR (ftype = 4 AND pts_p = 3 AND pts_m = 3 AND pts_s = 3 AND pts_c = 6)

     OR (ftype = 1 AND pts_p = 4 AND pts_m = 1 AND pts_s = 1 AND pts_c = 1)
     OR (ftype = 2 AND pts_p = 1 AND pts_m = 4 AND pts_s = 1 AND pts_c = 1)
     OR (ftype = 3 AND pts_p = 1 AND pts_m = 1 AND pts_s = 4 AND pts_c = 1)
     OR (ftype = 4 AND pts_p = 1 AND pts_m = 1 AND pts_s = 1 AND pts_c = 4);

    UPDATE characters SET
        pts_p = CASE ftype WHEN 1 THEN 4 ELSE 2 END,
        pts_m = CASE ftype WHEN 2 THEN 4 ELSE 2 END,
        pts_s = CASE ftype WHEN 3 THEN 4 ELSE 2 END,
        pts_c = CASE ftype WHEN 4 THEN 4 ELSE 2 END
    WHERE pts_p = 0 AND pts_m = 0 AND pts_s = 0 AND pts_c = 0;
END $$;
