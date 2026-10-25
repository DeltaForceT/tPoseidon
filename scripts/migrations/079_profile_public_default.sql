ALTER TABLE char_profile ALTER COLUMN public_flag_bitmask SET DEFAULT 3;

UPDATE char_profile SET public_flag_bitmask = 3 WHERE public_flag_bitmask = 0;
