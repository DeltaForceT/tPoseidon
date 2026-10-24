CREATE TABLE IF NOT EXISTS couple (
    couple_code         BIGSERIAL    PRIMARY KEY,
    couple_authority    BOOLEAN      NOT NULL DEFAULT FALSE,
    title_index         SMALLINT     NOT NULL DEFAULT 0,
    ring_item_index     INTEGER      NOT NULL DEFAULT 0,
    dress_item_index    INTEGER      NOT NULL DEFAULT 0,
    advice_index1       SMALLINT     NOT NULL DEFAULT 0,
    advice_index2       SMALLINT     NOT NULL DEFAULT 0,
    couple_state        SMALLINT     NOT NULL DEFAULT 0,
    relation_point      SMALLINT     NOT NULL DEFAULT 0,
    love_point          SMALLINT     NOT NULL DEFAULT 0,
    friend_point        SMALLINT     NOT NULL DEFAULT 0,
    recent_update_at    TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    created_at          TIMESTAMPTZ  NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS couple_char (
    char_uid            BIGINT       PRIMARY KEY REFERENCES characters(character_id) ON DELETE CASCADE,
    couple_code         BIGINT       NOT NULL REFERENCES couple(couple_code) ON DELETE CASCADE,
    couple_count        SMALLINT     NOT NULL DEFAULT 0,
    couple_delete_count SMALLINT     NOT NULL DEFAULT 0,
    marriage_count      SMALLINT     NOT NULL DEFAULT 0,
    divorce_count       SMALLINT     NOT NULL DEFAULT 0,
    wedding_in_count    SMALLINT     NOT NULL DEFAULT 0,
    created_at          TIMESTAMPTZ  NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS couple_char_couple_idx ON couple_char (couple_code);

CREATE TABLE IF NOT EXISTS couple_join (
    couple_code         BIGINT       NOT NULL REFERENCES couple(couple_code) ON DELETE CASCADE,
    char_uid            BIGINT       NOT NULL REFERENCES characters(character_id) ON DELETE CASCADE,
    char_sex            BOOLEAN      NOT NULL DEFAULT FALSE,
    event_bitmask       BIGINT       NOT NULL DEFAULT 0,
    divorce_state       SMALLINT     NOT NULL DEFAULT 0,
    PRIMARY KEY (couple_code, char_uid)
);

CREATE TABLE IF NOT EXISTS char_awakening (
    char_uid            BIGINT       PRIMARY KEY REFERENCES characters(character_id) ON DELETE CASCADE,
    soulseed_flag       SMALLINT     NOT NULL DEFAULT 0,
    awakening_flag      SMALLINT     NOT NULL DEFAULT 0,
    slot_max_count      SMALLINT     NOT NULL DEFAULT 0,
    created_at          TIMESTAMPTZ  NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS char_awakening_levelup_check (
    char_uid            BIGINT       NOT NULL REFERENCES characters(character_id) ON DELETE CASCADE,
    char_level          INTEGER      NOT NULL,
    levelup_at          TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    PRIMARY KEY (char_uid, char_level)
);

CREATE TABLE IF NOT EXISTS char_soulguardian (
    guardian_uid        BIGSERIAL    PRIMARY KEY,
    char_uid            BIGINT       NOT NULL REFERENCES characters(character_id) ON DELETE CASCADE,
    guardian_type       SMALLINT     NOT NULL,
    slot_num            SMALLINT     NOT NULL,
    equip_flag          SMALLINT     NOT NULL DEFAULT 0,
    guardian_name       VARCHAR(32)  NOT NULL DEFAULT '',
    f_type              SMALLINT     NOT NULL DEFAULT 0,
    pts_p               SMALLINT     NOT NULL DEFAULT 0,
    pts_m               SMALLINT     NOT NULL DEFAULT 0,
    pts_s               SMALLINT     NOT NULL DEFAULT 0,
    pts_c               SMALLINT     NOT NULL DEFAULT 0,
    skill_point         INTEGER      NOT NULL DEFAULT 0,
    created_at          TIMESTAMPTZ  NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS char_soulguardian_char_idx ON char_soulguardian (char_uid);

CREATE TABLE IF NOT EXISTS char_soulguardian_slot (
    char_uid            BIGINT       NOT NULL REFERENCES characters(character_id) ON DELETE CASCADE,
    slot_num            SMALLINT     NOT NULL,
    guardian_uid        BIGINT       NULL REFERENCES char_soulguardian(guardian_uid) ON DELETE SET NULL,
    PRIMARY KEY (char_uid, slot_num)
);

CREATE TABLE IF NOT EXISTS char_soulseed (
    char_uid                 BIGINT      PRIMARY KEY REFERENCES characters(character_id) ON DELETE CASCADE,
    seed_item_index          INTEGER     NOT NULL DEFAULT 0,
    power_point              SMALLINT    NOT NULL DEFAULT 0,
    magic_point              SMALLINT    NOT NULL DEFAULT 0,
    sense_point              SMALLINT    NOT NULL DEFAULT 0,
    charm_point              SMALLINT    NOT NULL DEFAULT 0,
    special_power_point      SMALLINT    NOT NULL DEFAULT 0,
    special_magic_point      SMALLINT    NOT NULL DEFAULT 0,
    special_sense_point      SMALLINT    NOT NULL DEFAULT 0,
    special_charm_point      SMALLINT    NOT NULL DEFAULT 0,
    start_at                 TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    last_update_at           TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS pvp_match (
    match_seq           BIGSERIAL    PRIMARY KEY,
    match_begin         TIMESTAMPTZ  NOT NULL,
    match_end           TIMESTAMPTZ  NOT NULL,
    match_type          SMALLINT     NOT NULL,
    match_map           SMALLINT     NOT NULL,
    match_timeout       SMALLINT     NOT NULL,
    join_type           SMALLINT     NOT NULL,
    char_uid            BIGINT       NOT NULL,
    match_char_uid      BIGINT       NOT NULL,
    get_ladder          INTEGER      NOT NULL DEFAULT 0,
    get_exp             INTEGER      NOT NULL DEFAULT 0,
    match_result        SMALLINT     NOT NULL DEFAULT 0
);

CREATE INDEX IF NOT EXISTS pvp_match_char_idx ON pvp_match (char_uid, match_end DESC);

CREATE TABLE IF NOT EXISTS pvp_ladder (
    char_uid            BIGINT       PRIMARY KEY REFERENCES characters(character_id) ON DELETE CASCADE,
    pvp_ladder          INTEGER      NOT NULL DEFAULT 1000,
    match_count         SMALLINT     NOT NULL DEFAULT 0,
    win_count           SMALLINT     NOT NULL DEFAULT 0,
    lose_count          SMALLINT     NOT NULL DEFAULT 0,
    draw_count          SMALLINT     NOT NULL DEFAULT 0,
    disc_count          SMALLINT     NOT NULL DEFAULT 0
);

CREATE TABLE IF NOT EXISTS post (
    post_uid            BIGSERIAL    PRIMARY KEY,
    post_type           SMALLINT     NOT NULL,
    post_sub_type       SMALLINT     NOT NULL DEFAULT 0,
    post_state          SMALLINT     NOT NULL DEFAULT 0,
    title               VARCHAR(64)  NOT NULL DEFAULT '',
    content             VARCHAR(512) NOT NULL DEFAULT '',
    post_money_tax      BIGINT       NOT NULL DEFAULT 0,
    post_money          BIGINT       NOT NULL DEFAULT 0,
    post_money_take_at  TIMESTAMPTZ  NULL,
    sell_code           BIGINT       NOT NULL DEFAULT 0,
    parent_post_uid     BIGINT       NOT NULL DEFAULT 0,
    created_at          TIMESTAMPTZ  NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS post_date (
    date_seq            BIGSERIAL    PRIMARY KEY,
    post_uid            BIGINT       NOT NULL REFERENCES post(post_uid) ON DELETE CASCADE,
    post_state          SMALLINT     NOT NULL DEFAULT 0,
    user_type           SMALLINT     NOT NULL DEFAULT 0,
    account_id          BIGINT       NOT NULL,
    char_uid            BIGINT       NOT NULL,
    char_name           VARCHAR(32)  NOT NULL DEFAULT '',
    char_level          SMALLINT     NOT NULL DEFAULT 0,
    send_at             TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    return_at           TIMESTAMPTZ  NULL,
    created_at          TIMESTAMPTZ  NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS post_date_char_idx ON post_date (char_uid, send_at DESC);

CREATE TABLE IF NOT EXISTS post_item (
    item_seq            BIGSERIAL    PRIMARY KEY,
    post_uid            BIGINT       NOT NULL REFERENCES post(post_uid) ON DELETE CASCADE,
    item_uid            BIGINT       NOT NULL,
    item_class          SMALLINT     NOT NULL,
    item_index          INTEGER      NOT NULL,
    item_attr           SMALLINT     NOT NULL DEFAULT 0,
    add_minute          INTEGER      NOT NULL DEFAULT 0,
    item_flag           SMALLINT     NOT NULL DEFAULT 0,
    created_at          TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    take_char_uid       BIGINT       NULL,
    take_char_level     SMALLINT     NULL,
    take_at             TIMESTAMPTZ  NULL
);

CREATE INDEX IF NOT EXISTS post_item_post_idx ON post_item (post_uid);

CREATE TABLE IF NOT EXISTS account_block (
    block_seq           BIGSERIAL    PRIMARY KEY,
    target_account_id   BIGINT       NOT NULL,
    block_type          SMALLINT     NOT NULL,
    block_reason        SMALLINT     NOT NULL,
    gm_account_id       BIGINT       NULL,
    block_from          TIMESTAMPTZ  NOT NULL,
    block_to            TIMESTAMPTZ  NOT NULL,
    description         VARCHAR(1024) NOT NULL DEFAULT '',
    created_at          TIMESTAMPTZ  NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS account_block_target_idx ON account_block (target_account_id, block_to DESC);

CREATE TABLE IF NOT EXISTS account_logout (
    log_seq             BIGSERIAL    PRIMARY KEY,
    account_id          BIGINT       NOT NULL,
    login_at            TIMESTAMPTZ  NOT NULL,
    logout_at           TIMESTAMPTZ  NOT NULL,
    logout_world        SMALLINT     NOT NULL DEFAULT 0,
    play_time           INTEGER      NULL
);

CREATE INDEX IF NOT EXISTS account_logout_account_idx ON account_logout (account_id, logout_at DESC);

CREATE TABLE IF NOT EXISTS fortune_info (
    char_uid            BIGINT       PRIMARY KEY REFERENCES characters(character_id) ON DELETE CASCADE,
    view_count          INTEGER      NOT NULL DEFAULT 0,
    title_id            INTEGER      NOT NULL DEFAULT 0,
    item_attr_string    VARCHAR(255) NOT NULL DEFAULT '',
    updated_at          TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    expires_at          TIMESTAMPTZ  NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS fortune_info_log (
    log_seq             BIGSERIAL    PRIMARY KEY,
    char_uid            BIGINT       NOT NULL,
    view_count          INTEGER      NOT NULL,
    title_id            INTEGER      NOT NULL,
    item_attr_string    VARCHAR(255) NOT NULL DEFAULT '',
    logged_at           TIMESTAMPTZ  NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS fortune_info_log_char_idx ON fortune_info_log (char_uid, logged_at DESC);

CREATE TABLE IF NOT EXISTS broadcast (
    broadcast_code      BIGSERIAL    PRIMARY KEY,
    char_uid            BIGINT       NOT NULL,
    island_code         SMALLINT     NOT NULL DEFAULT 0,
    message             VARCHAR(512) NOT NULL DEFAULT '',
    message_flag        SMALLINT     NOT NULL DEFAULT 0,
    broadcast_type      SMALLINT     NOT NULL DEFAULT 0,
    color_type          SMALLINT     NOT NULL DEFAULT 0,
    created_at          TIMESTAMPTZ  NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS broadcast_island_idx ON broadcast (island_code, created_at DESC);

CREATE TABLE IF NOT EXISTS broadcast_log (
    log_seq             BIGSERIAL    PRIMARY KEY,
    char_uid            BIGINT       NOT NULL,
    island_code         SMALLINT     NOT NULL,
    message             VARCHAR(512) NOT NULL,
    broadcast_type      SMALLINT     NOT NULL,
    color_type          SMALLINT     NOT NULL DEFAULT 0,
    logged_at           TIMESTAMPTZ  NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS broadcast_log_char_idx ON broadcast_log (char_uid, logged_at DESC);
