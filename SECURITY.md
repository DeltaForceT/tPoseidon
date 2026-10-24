# Security

People open tPoseidon servers to the internet, so we take security bugs seriously. Please tell us privately and give us time to fix it before anyone else hears about it.

## Reporting

Go to the repo's Security tab and press "Report a vulnerability". Tell us what you found, which version, and how to reproduce it.

Please don't open a public issue, forum thread or pull request for it.

## What counts

- Crashing a server from outside, or with a normal game client.
- Reading or changing another account's data.
- Logging in without the right password.
- Running code on the host.
- Getting around the connection or packet limits.
- Passwords or other secrets ending up in logs, files or network traffic.
- The database login tPoseidon creates getting more rights than its own database.

Plain gameplay exploits, like duplicating items or skipping a quest step, are normal bugs. Use the Bug report form for those, unless they also crash the server or mess up other players' data.

## What happens next

We'll reply within a week to say we got it. Then we'll tell you whether we could reproduce it and how we plan to fix it. When the fix ships we publish an advisory, with your name on it if you want.

During the beta, only the latest release gets fixes, so please check you're on it before you report.

## If you host a server

- Don't open PostgreSQL's port (5432) to the network. The servers reach it from the same machine.
- Only open the game ports `tposeidon setup` tells you about.
- Keep `tposeidon.json` to yourself. It has the database password in it.
