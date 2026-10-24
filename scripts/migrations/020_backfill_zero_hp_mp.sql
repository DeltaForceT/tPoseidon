UPDATE characters
SET hp = 120 + FLOOR((GREATEST(level, 1) - 1) * pts_c * 7.5) + max_hp_pts
WHERE hp = 0;

UPDATE characters
SET mp = 80 + FLOOR((GREATEST(level, 1) - 1) * pts_m * 7.5) + max_mp_pts
WHERE mp = 0;
