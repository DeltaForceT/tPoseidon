UPDATE characters
SET type_2 = job_2
WHERE job_2 <> 0
  AND type_2 = 0;

UPDATE characters
SET type_3 = job_2 + 9
WHERE job_3 <> 0
  AND job_2 <> 0
  AND type_3 = 0;

UPDATE characters
SET job_state = CASE WHEN job_3 <> 0 THEN 3 WHEN job_2 <> 0 THEN 2 ELSE 1 END
WHERE (job_state = 0 AND (job_2 <> 0 OR job_3 <> 0))
   OR (job_state = 3 AND job_3 = 0)
   OR (job_state = 2 AND job_2 = 0);
