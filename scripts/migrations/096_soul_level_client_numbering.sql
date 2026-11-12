UPDATE item_option
SET prop_value = LEAST(prop_value + 1, 11)
WHERE prop_type = 16
  AND prop_id = 28001
  AND prop_value BETWEEN 0 AND 10;
