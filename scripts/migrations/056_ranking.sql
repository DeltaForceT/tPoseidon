CREATE TABLE IF NOT EXISTS ranking (
    ranking_type   SMALLINT    NOT NULL,
    monster_id     INTEGER     NOT NULL,
    ranking_order  SMALLINT    NOT NULL,
    char_uid       BIGINT      NOT NULL,
    char_type      SMALLINT    NOT NULL,
    char_name      VARCHAR(24) NOT NULL,
    value1         INTEGER     NOT NULL,
    value2         INTEGER     NOT NULL,
    value3         INTEGER     NOT NULL,
    PRIMARY KEY (ranking_type, monster_id, ranking_order)
);
