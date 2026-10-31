UPDATE characters
SET job_state = CASE WHEN job_3 <> 0 THEN 3 ELSE 2 END
WHERE job_state = 0
  AND (job_2 <> 0 OR job_3 <> 0);
