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

In setup, the admin login is `postgres` with the password you put in the file. For `tposeidon backup` you'll also need `pg_dump` on the computer itself (install the PostgreSQL client tools, e.g. `sudo apt install postgresql-client`).

## When setup can't connect

Setup tells you what went wrong. The usual ones:

- **Nothing answered on localhost:5432.** PostgreSQL isn't installed, isn't running, or uses another port. Start the service (see above) or give setup the right port.
- **Rejected the password.** Wrong password for that login. On Linux, set one with the `ALTER USER` command above.
- **Refused the login from this computer.** `pg_hba.conf` doesn't allow password logins. See the Fedora note above; the same fix applies anywhere.
- **Too old.** Install PostgreSQL 14 or newer.

## Keep it private

Don't open PostgreSQL's port (5432) to the internet. The game servers talk to it from the same computer; players never need to reach it.
