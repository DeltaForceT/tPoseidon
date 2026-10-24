CREATE TABLE IF NOT EXISTS tbl_world (
    game_code           SMALLINT    NOT NULL,
    area_code           SMALLINT    NOT NULL DEFAULT 1,
    world_code          SMALLINT    NOT NULL,
    world_name          TEXT        NOT NULL,
    world_created       TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    world_ls_ip         TEXT        NOT NULL,
    world_ls_port       INTEGER     NOT NULL,
    world_user_max      INTEGER     NOT NULL DEFAULT 1000,
    world_user_cur      INTEGER     NOT NULL DEFAULT 0,
    world_heart_beat    TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    world_user_maxjoin  INTEGER     NOT NULL DEFAULT 1000,
    world_enable        BOOLEAN     NOT NULL DEFAULT TRUE,
    world_visible       BOOLEAN     NOT NULL DEFAULT TRUE,
    PRIMARY KEY (game_code, world_code)
);

CREATE TABLE IF NOT EXISTS tbl_island (
    game_code           SMALLINT    NOT NULL,
    world_code          SMALLINT    NOT NULL,
    island_code         SMALLINT    NOT NULL,
    island_name         TEXT        NOT NULL,
    island_created      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    island_ls_ip        TEXT        NOT NULL,
    island_ls_port      INTEGER     NOT NULL,
    island_user_max     INTEGER     NOT NULL DEFAULT 1000,
    island_user_cur     INTEGER     NOT NULL DEFAULT 0,
    island_heart_beat   TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    island_user_maxjoin INTEGER     NOT NULL DEFAULT 1000,
    island_enable       BOOLEAN     NOT NULL DEFAULT TRUE,
    island_visible      BOOLEAN     NOT NULL DEFAULT TRUE,
    PRIMARY KEY (game_code, world_code, island_code)
);

CREATE TABLE IF NOT EXISTS tbl_parm_server (
    reg_date        TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    is_valid        BOOLEAN     NOT NULL DEFAULT TRUE,
    svr_no          INTEGER     NOT NULL,
    svr_type        INTEGER     NULL,
    major_ip        TEXT        NOT NULL,
    minor_ip        TEXT        NULL,
    world_no        INTEGER     NULL,
    thd_ni_cnt      INTEGER     NOT NULL DEFAULT 1,
    session_cnt     INTEGER     NOT NULL DEFAULT 1000,
    send_cnt        INTEGER     NOT NULL DEFAULT 1,
    tcp_port        INTEGER     NOT NULL,
    udp_port        INTEGER     NOT NULL DEFAULT 0,
    thd_db_cnt      INTEGER     NOT NULL DEFAULT 1,
    multi_qool_sz   INTEGER     NOT NULL DEFAULT 0,
    svr_desc        TEXT        NOT NULL DEFAULT '',
    fls_opened      BOOLEAN     NOT NULL DEFAULT FALSE,
    PRIMARY KEY (svr_no)
);

INSERT INTO tbl_world
    (game_code, area_code, world_code, world_name, world_ls_ip, world_ls_port)
VALUES
    (1, 1, 1, 'ModernTrickster', '127.0.0.1', 9980)
ON CONFLICT (game_code, world_code) DO NOTHING;

INSERT INTO tbl_island
    (game_code, world_code, island_code, island_name, island_ls_ip, island_ls_port)
VALUES
    (1, 1, 1, 'Main', '127.0.0.1', 9980)
ON CONFLICT (game_code, world_code, island_code) DO NOTHING;

INSERT INTO tbl_parm_server
    (svr_no, svr_type, major_ip, world_no, tcp_port, svr_desc)
VALUES
    (1, 1, '127.0.0.1',      1,  9980, 'FirstLogin/Lobby'),
    (2, 2, '127.0.0.1',      1, 13336, 'Login'),
    (3, 3, '127.0.0.1',      1, 10006, 'Channel'),
    (4, 4, '127.0.0.1',      1, 22006, 'Game1'),
    (5, 5, '127.0.0.1',      1, 23006, 'Game2'),
    (6, 6, '127.0.0.1',      1, 16646, 'Master (inter-server loopback)'),
    (7, 7, '127.0.0.1',      1, 13336, 'System (inter-server loopback)')
ON CONFLICT (svr_no) DO NOTHING;
