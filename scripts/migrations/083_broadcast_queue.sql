ALTER TABLE broadcast
    ADD COLUMN IF NOT EXISTS refund_item_index INTEGER  NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS refund_item_class SMALLINT NOT NULL DEFAULT 0;

CREATE INDEX IF NOT EXISTS broadcast_waiting_idx ON broadcast (broadcast_code) WHERE message_flag <= 1;

CREATE TABLE IF NOT EXISTS broadcast_state_log (
    log_seq        BIGSERIAL    PRIMARY KEY,
    broadcast_code BIGINT       NOT NULL REFERENCES broadcast(broadcast_code) ON DELETE CASCADE,
    message_flag   SMALLINT     NOT NULL,
    created_at     TIMESTAMPTZ  NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS broadcast_state_log_code_idx ON broadcast_state_log (broadcast_code);
