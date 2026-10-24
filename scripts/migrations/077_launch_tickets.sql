CREATE TABLE IF NOT EXISTS launch_tickets (
    ticket_id   BIGSERIAL PRIMARY KEY,
    account_id  BIGINT NOT NULL REFERENCES accounts(account_id) ON DELETE CASCADE,
    client_ips  TEXT[] NOT NULL,
    expires_at  TIMESTAMPTZ NOT NULL
);

CREATE INDEX IF NOT EXISTS launch_tickets_account_idx ON launch_tickets (account_id);
CREATE INDEX IF NOT EXISTS launch_tickets_expires_idx ON launch_tickets (expires_at);
