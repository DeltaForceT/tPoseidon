# Installing PostgreSQL

tPoseidon keeps accounts and characters in PostgreSQL. You install PostgreSQL once; `tposeidon setup` does the rest (it creates the database, a login that can only touch that database, and all the tables).

You need **PostgreSQL 14 or newer**. We test on 16, so pick 16 or 17 if you have the choice.

When you're done, you should know three things for setup: the host (usually `localhost`), the port (usually `5432`), and an admin login with its password (usually `postgres`). Setup uses the admin login once and doesn't save its password.

## Windows

1. Download the installer from https://www.postgresql.org/download/windows/ (the "Download the installer" link).
2. Run it and keep the defaults. You can untick Stack Builder at the end; you don't need it.
3. It asks for a password for the `postgres` user. Pick one and write it down. That's your admin login for setup.
4. Leave the port at 5432.

PostgreSQL now runs as a Windows service called `postgresql-x64-16` (or whichever version you picked) and starts with Windows. If setup says nothing answered on port 5432, open Services (`services.msc`) and start it.

`tposeidon backup` uses `pg_dump`, which the installer puts in `C:\Program Files\PostgreSQL\<version>\bin`. tPoseidon finds it there on its own.

## Linux

### Debian, Ubuntu and friends

```
sudo apt update
sudo apt install postgresql
```

It starts right away and at every boot. The `postgres` login has no password yet and only works through `sudo`, so give it one:

```
sudo -u postgres psql -c "ALTER USER postgres PASSWORD 'pick-a-password'"
```

Use `postgres` and that password in setup.

### Fedora, RHEL, Rocky, Alma

```
sudo dnf install postgresql-server
sudo postgresql-setup --initdb
sudo systemctl enable --now postgresql
sudo -u postgres psql -c "ALTER USER postgres PASSWORD 'pick-a-password'"
```

These distributions only allow "ident" logins over the network by default, which tPoseidon can't use. Open `/var/lib/pgsql/data/pg_hba.conf` as root, change `ident` to `scram-sha-256` on the two `host` lines for `127.0.0.1/32` and `::1/128`, then run `sudo systemctl reload postgresql`.

### Arch

```
sudo pacman -S postgresql
sudo -u postgres initdb -D /var/lib/postgres/data
sudo systemctl enable --now postgresql
sudo -u postgres psql -c "ALTER USER postgres PASSWORD 'pick-a-password'"
```

If your distribution ships something older than 14, use the PostgreSQL project's own repository instead: https://www.postgresql.org/download/linux/

## macOS

Either of these works.

**Postgres.app** (the easiest): download it from https://postgresapp.com, drag it to Applications, open it and press Initialize. In setup, the admin login is your macOS user name and the password is empty.

**Homebrew:**

```
brew install postgresql@16
brew services start postgresql@16
```

Again the admin login is your macOS user name with an empty password.

## Docker (any system)

If you already use Docker, this runs PostgreSQL in a container that only this computer can reach. Save it as `compose.yml` in a folder of its own, put your own password in, and run `docker compose up -d` there.

```yaml
services:
  postgres:
    image: postgres:16
    restart: unless-stopped
    environment:
      POSTGRES_PASSWORD: pick-a-password
    ports:
      - "127.0.0.1:5432:5432"
    volumes:
      - tposeidon-db:/var/lib/postgresql/data

volumes:
  tposeidon-db:
```

In setup, the admin login is `postgres` with the password you put in the file. ### Backups with PostgreSQL in Docker

You do not need `pg_dump` on the computer. `tposeidon backup`, the panel's backup button and the backup offered by `tposeidon start` after an update work like this:

1. If `pg_dump` is on PATH (or in the usual PostgreSQL folders), it is used.
2. Otherwise tPoseidon asks Docker (`docker ps`) for a running container that publishes the database host and port saved in setup, for example `127.0.0.1:5433->5432/tcp`, and runs `pg_dump --format=custom` inside that container. The dump is streamed to the same file in the same `backups` folder as always, so `pg_restore` restores it the same way. The password is passed to Docker in the environment, not on the command line.

For this to work, Docker must be running and the container started, and its published port must be the one setup used. The container's own `pg_dump` is the same version as the server, which is what you want.

If neither way works, the message says so and what was missing. To start the servers anyway, run `tposeidon start --no-backup` (the panel has Start without a backup). In a run without a keyboard (`-y`, or input from a script) a failed backup stops the start with that message instead of asking, so use `--no-backup` there if you do not want a backup.

## When setup can't connect

Setup tells you what went wrong. The usual ones:

- **Nothing answered on localhost:5432.** PostgreSQL isn't installed, isn't running, or uses another port. Start the service (see above) or give setup the right port.
- **Rejected the password.** Wrong password for that login. On Linux, set one with the `ALTER USER` command above.
- **Refused the login from this computer.** `pg_hba.conf` doesn't allow password logins. See the Fedora note above; the same fix applies anywhere.
- **Too old.** Install PostgreSQL 14 or newer.

## Keep it private

Don't open PostgreSQL's port (5432) to the internet. The game servers talk to it from the same computer; players never need to reach it.
