CREATE TABLE IF NOT EXISTS harcon_stage_record (
    char_uid          BIGINT   NOT NULL REFERENCES characters(character_id) ON DELETE CASCADE,
    stage_id          SMALLINT NOT NULL,
    plays             INTEGER  NOT NULL DEFAULT 0,
    wins              INTEGER  NOT NULL DEFAULT 0,
    losses            INTEGER  NOT NULL DEFAULT 0,
    total_score       BIGINT   NOT NULL DEFAULT 0,
    contribution_sum  REAL     NOT NULL DEFAULT 0,
    best_score        INTEGER  NOT NULL DEFAULT 0,
    best_rank         SMALLINT NOT NULL DEFAULT 0,
    best_contribution REAL     NOT NULL DEFAULT 0,
    best_deaths       SMALLINT NOT NULL DEFAULT 0,
    best_tower_score  INTEGER  NOT NULL DEFAULT 0,
    best_towers_built SMALLINT NOT NULL DEFAULT 0,
    PRIMARY KEY (char_uid, stage_id)
);
