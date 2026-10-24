# How to report a bug

If we can make the bug happen on our own server, we can usually fix it. For that we need the version, the steps, what you expected, and the server logs.

## Is it new?

Check you're on the latest release (`tposeidon status` prints the version). Then look at the Known issues list in the release notes and search the issues on GitHub and the forum. If someone already reported it, add your details there.

## Can you make it happen again?

Try, and write down every step from somewhere easy to get back to. Something like: log in, first character, warp to town, talk to the shop NPC, buy one potion.

If it only happened once, report it anyway and say so.

## Grab the logs

Each server writes its own log. Copy from a few minutes before the problem to just after it.

In the control panel (Windows):

- Ctrl+L shows the log windows. The "All" view has every server in one list, in order.
- Each server row has a button that opens that server's log file.
- Switch on "Hide personal paths" in the panel settings before you copy anything. It hides your user and install folders.

From the command line, on any system:

- `tposeidon status` shows where the logs are.
- They're in the `log/` folder inside your tPoseidon folder, one per server: `log/Kr-Game1.log`, `log/Kr-Channel.log`, `log/Kr-Master.log` and so on.
- If a server crashed, there's a dump in `log/dmp/`.
- If the problem is about the database (setup can't connect, saving or loading fails), say which PostgreSQL version you run and how you installed it. `tposeidon status` shows whether the database is reachable.

Which ones we need:

| Problem | Send |
|---|---|
| setup or start fails | what `tposeidon setup` or `tposeidon start` printed, and your PostgreSQL version |
| can't log in, empty server or channel list | Kr-Lobby, Kr-Channel, Kr-Master |
| creating, picking or deleting characters | Kr-Channel, Kr-Master |
| anything in game | Kr-Game1 (Kr-Game2 if you were on the second game server), Kr-Master |
| item shop | Kr-Shop, Kr-Game1 |
| a server crashed | its log and the newest file in `log/dmp/` |

Clean them up before you post: real names, passwords, IP addresses, and paths with your user name in them.

## Screenshots

A screenshot of the error or the wrong text saves a lot of back and forth. For movement, animation or timing, a short video is even better.

## Send it

- On GitHub: New issue, then Bug report. The form walks you through it.
- On the forum: paste [the forum template](forum-bug-report-template.md) into a new post and fill it in.

One bug per report, please. Three bugs, three reports.

## Security bugs

If it lets someone crash a server from outside, mess with another player's data, or get into an account without the password, don't post it. See [SECURITY.md](../../SECURITY.md).
