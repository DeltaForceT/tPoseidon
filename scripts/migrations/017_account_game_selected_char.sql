ALTER TABLE accounts
    ADD COLUMN IF NOT EXISTS acc_game_selected_char_uid BIGINT;
