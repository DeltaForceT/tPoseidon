DO $$
DECLARE
    uid_sequence TEXT := pg_get_serial_sequence('items', 'item_uid');
BEGIN
    EXECUTE format(
        'SELECT setval(%L::regclass, GREATEST((SELECT last_value FROM %s), (SELECT COALESCE(MAX(item_uid), 1) FROM charged_item)))',
        uid_sequence, uid_sequence);
    EXECUTE format('ALTER TABLE charged_item ALTER COLUMN item_uid SET DEFAULT nextval(%L::regclass)', uid_sequence);
    EXECUTE format(
        'UPDATE charged_item c SET item_uid = nextval(%L::regclass) WHERE c.item_use_date IS NULL AND EXISTS (SELECT 1 FROM items i WHERE i.item_uid = c.item_uid)',
        uid_sequence);
END $$;
