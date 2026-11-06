UPDATE characters
SET job_2 = species + 9,
    type_2 = species + 9,
    job_state = 2
WHERE job_tier >= 2
  AND job_2 = 0
  AND job_3 = 0;
