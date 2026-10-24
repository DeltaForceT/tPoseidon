UPDATE mycamp_item SET item_where = 1
WHERE item_where = 2 AND item_uid IN (SELECT item_uid FROM mycamp_deco);
