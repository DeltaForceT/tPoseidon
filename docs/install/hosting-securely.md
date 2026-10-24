# Hosting securely

If only you and your LAN play, you can skip this page. Once your server is open to the internet, people you don't know can reach it, and a few of them will try to knock it over. This page is about keeping it up.

## What tPoseidon handles, and what it can't

Every tPoseidon server runs a **connection guard**. It watches each address that connects and cuts off the ones that misbehave: too many connections, connections that never log in, junk or oversized packets, packet floods, password guessing. Addresses that keep doing it get banned for a while. Normal players never notice it; the limits sit well above what the game client does.

What the guard can't do is stop a volumetric DDoS. That's when someone points a botnet at your address and fills your network link with garbage. The traffic never needs to reach tPoseidon to hurt you; your connection is simply full. No program on the machine can fix that, because the damage happens on the wire before the machine sees anything.

The only real answer is protection upstream, in front of your server:

- **A host with DDoS filtering.** Many hosting companies that rent servers for game hosting filter floods at their network edge as part of the package. This is the simplest option and what we'd pick first.
- **A TCP proxy service.** Some services take the traffic on their network, filter it, and pass the clean connections on to your server. Your real address stays hidden. Read the "Behind a proxy" section below if you go this way.

## Don't host from your home connection

Your home IP ends up in every player's client, so anyone can find it. If someone gets annoyed and floods it, your whole household loses its internet, your ISP may not be happy about it, and there's nothing you can do from your side until it stops. A home address also tends to change, which breaks things for players.

Rent a small server or VPS instead. tPoseidon runs on Windows and Linux, and a modest machine is enough for a community server.

## Open only the client ports

Players need these ports, and nothing else:

| Port | Server |
|---|---|
| 9980 | Lobby (login) |
| 10006 | Channel (character select) |
| 13336 | System (chat, friends, party, guild) |
| 22006 | Game server 1 |
| 23006 | Game server 2 |
| 9990 | Item shop, only if you turned the shop on |

These are the defaults; `tposeidon status` shows the ones you use. In your firewall, allow incoming TCP on these ports and block everything else. The Master server (16646) and the links between the servers already listen on 127.0.0.1 only, so they're not reachable from outside even without a firewall, but there's no reason to open them.

On Windows, Windows Defender Firewall asks the first time a server starts listening; allow the tPoseidon programs and nothing more. On Linux with `ufw`, for example:

```
sudo ufw default deny incoming
sudo ufw allow 22/tcp
sudo ufw allow 9980,10006,13336,22006,23006/tcp
sudo ufw enable
```

(Leave 22 open only if that's how you reach the machine, and add 9990 if you run the shop.)

## Keep PostgreSQL local

The database should only ever be reachable from the machine itself. A fresh PostgreSQL install already listens on localhost only, so mostly this is about not changing that:

- Keep `listen_addresses = 'localhost'` in `postgresql.conf`.
- Don't add `host ... 0.0.0.0/0` lines to `pg_hba.conf`.
- Never open port 5432 in your firewall.
- Use a long password for the `postgres` admin login. tPoseidon's own login can only touch its database, which is how `tposeidon setup` creates it.

If you need to look at the database from another computer, use an SSH tunnel instead of opening the port.

## The connection guard

The guard is on by default. Its settings live in the `security` section of `tposeidon.json`, next to the program. tPoseidon writes the whole section, with every default filled in, each time it saves that file (setup does, and so does every start), so you can see and change them all there. After changing a setting, restart the servers (`tposeidon stop`, then `tposeidon start`). If a value can't work, like a limit of zero or a range that isn't a range, `tposeidon start` refuses and tells you which one.

Loopback (127.0.0.1) is always allowed and never limited, so your own client and the tools on the machine are never affected. Add other addresses you trust, like an admin's fixed IP or your own LAN, to `trustedAddresses`.

### What it limits

| Setting | Default | What it does |
|---|---|---|
| `enabled` | `true` | Turns the whole guard on or off. |
| `trustedAddresses` | `[]` | Addresses or ranges (like `"10.0.0.0/8"`) that are never limited or banned. |
| `maxConnectionsPerAddress` | 32 | Open connections one address may hold on one server. |
| `newConnectionsPerAddressBurst` | 20 | New connections one address may open at once... |
| `newConnectionsPerAddressPerSecond` | 1 | ...and how fast that allowance comes back. |
| `maxConnections` | 4000 | Connections per server in total. |
| `maxPendingHandshakes` | 512 | Connections per server that haven't logged in yet. |
| `handshakeTimeoutSeconds` | 30 | A connection that sends nothing valid in this time is closed. The client sends its first packet within a fraction of a second. |
| `loginTimeoutSeconds` | 120 | A connection that hasn't logged in by then is closed. |
| `idleTimeoutSeconds` | 600 | A logged-in channel connection that goes silent this long is closed. The client keeps the channel alive every few seconds. Game connections have no idle limit, because a player standing still in town sends nothing for minutes. `0` turns it off. |
| `partialFrameTimeoutSeconds` | 15 | A connection that leaves a packet half-sent this long is closed (the "slowloris" trick). |
| `maxFrameBytes` | 16384 | Largest packet a client may send. Real ones are under 1 KB. |
| `packetBurst`, `packetsPerSecond` | 400, 60 | Packets per connection at once, and the steady rate. |
| `opcodeBurst`, `opcodePacketsPerSecond` | 60, 10 | The same for any single kind of packet. |
| `opcodeAllowances` | movement, NPC dialog, chat | Packet kinds that players send a lot get more: movement 240 at once and 40/s, dialog and chat 120 and 20/s. |

A connection that goes past these limits, or sends a packet that doesn't decrypt, is closed on the spot.

### Logins

Wrong passwords are free at first. After 3 failures in a row, the server waits before answering: 1 second, then 2, 4, 8 and at most 16 (`loginFailuresBeforeDelay`, `loginDelayMaxSeconds`). 10 failures on one account (`accountLockoutFailures`) or 30 from one address (`addressLockoutFailures`) within 15 minutes (`loginFailureWindowMinutes`) lock that account or address for 15 minutes (`lockoutMinutes`). While locked, the login is refused without checking the password. The client sees the same thing it sees for a wrong password, so there's nothing new for players to learn.

### Bans

Every time an address breaks a limit it gets strikes: 1 for things that can happen by accident, like a slow connection that times out, and 5 for things a real client never does, like an oversized or broken packet or a flood. 10 strikes within 60 seconds (`strikesBeforeBan`, `strikeWindowSeconds`) ban the address for 5 minutes (`banMinutes`). Each ban after that doubles, up to a day (`banMaxMinutes`), and a day without a ban (`banForgetHours`) starts the count over.

A ban refuses new connections from that address on every server and closes its connections that haven't logged in. Players who are already in the game stay in.

### The item shop

The shop is a web page, so it gets its own limits: 120 requests at once and 20 per second per address (`requestBurst`, `requestsPerSecond`), and request bodies up to 1 MB (`maxRequestBodyBytes`). It shares bans and the allow list with the game servers.

### Play logins from other computers

`tposeidon play --server <address>` sends the player's account name and password to the item shop's web port (`/play-ticket`). On success the server leaves a one-time login pass for that player's address, which the lobby accepts for about a minute. Wrong passwords are throttled and locked out like lobby logins. Because the password travels over that connection, serve the web port over HTTPS when the server is public: give the item shop an `https` address in `MT_WEBSHOP_URLS` together with a certificate (the standard ASP.NET Core settings `ASPNETCORE_Kestrel__Certificates__Default__Path` and `...__Password`), and have players pass `--web-url https://your.domain:9990`. Over plain HTTP anyone on the path can read the password. The pass is tied to the address the web port sees, so a proxy that hides the player's real address will make the lobby refuse the pass.

### Logs

Each incident gets one short line in that server's log, at most once per address and kind every 30 seconds and 20 lines every 10 seconds (`incidentLogIntervalSeconds`, `incidentLogLinesPerTenSeconds`), so an attack can't fill your disk. When lines are skipped, a summary says how many.

## The `tposeidon security` commands

```
tposeidon security
```

shows what each server's guard sees: connections, what it refused or closed and why, the blocked addresses with how long they're blocked, the allow list and the latest incidents.

```
tposeidon security ban 203.0.113.9
tposeidon security ban 203.0.113.0/24 --minutes 60 --reason "spam bots"
tposeidon security unban 203.0.113.9
tposeidon security allow 198.51.100.4 --reason "admin at home"
tposeidon security disallow 198.51.100.4
```

`ban` blocks an address or a range on every server, until you unban it or for the minutes you give. `unban` lifts manual and automatic bans on that address and forgets its strikes and login lockouts. `allow` puts an address on the allow list, which works like `trustedAddresses`. Running servers pick these up within a few seconds, without a restart.

The control panel has the same thing under the **Security** button: blocked addresses with an Unban button each, a box to ban an address, and the recent incidents.

The lists are kept in `run/security/bans.json`, and each server writes its status next to it.

## Behind a proxy

If players connect through a TCP proxy service, every connection reaches tPoseidon from the proxy's addresses rather than the players'. The guard then sees a handful of addresses with hundreds of players behind each, and its per-address limits would hit real players. tPoseidon doesn't read the original address from the proxy, so you have two options:

- Raise `maxConnectionsPerAddress`, `newConnectionsPerAddressBurst`, `maxPendingHandshakes` and `addressLockoutFailures` well above your player count. The guard keeps checking packets, timeouts and account lockouts on every connection, but it can no longer tell one player's address from another, so be careful with bans: banning the proxy's address shuts everyone out.
- Or put the proxy's address ranges in `trustedAddresses`. Trusted connections skip the guard completely, so this only makes sense if the proxy service does its own per-player filtering.

Either way, set the public address in setup to the proxy's address, because the servers hand that address to the client when it moves from the lobby to the channel and game servers. The proxy has to forward every client port in the table above.
