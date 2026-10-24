<p align="center">
  <img src=".github/assets/logo.png" alt="tPoseidon" width="480">
</p>

<p align="center"><strong>A server emulator for the Trickster Online KR 2014 client. Run your own world, the way it used to be.</strong></p>

<p align="center">
  <a href="https://github.com/DeltaForceT/tPoseidon/actions/workflows/ci.yml"><img alt="Build" src="https://github.com/DeltaForceT/tPoseidon/actions/workflows/ci.yml/badge.svg"></a>
  <a href="https://github.com/DeltaForceT/tPoseidon/releases"><img alt="Latest release" src="https://img.shields.io/github/v/release/DeltaForceT/tPoseidon?include_prereleases&label=release"></a>
  <a href="LICENSE"><img alt="License: beta license" src="https://img.shields.io/badge/license-beta%20license-blue"></a>
  <img alt="Platforms: Windows, Linux, macOS" src="https://img.shields.io/badge/platforms-Windows%20%7C%20Linux%20%7C%20macOS-lightgrey">
</p>

<p align="center">
  <a href="https://github.com/DeltaForceT/tPoseidon/releases">Download</a> &middot;
  <a href="#getting-started">Getting started</a> &middot;
  <a href="docs/beta/SYSTEMS.md">Systems status</a> &middot;
  <a href="https://github.com/DeltaForceT/tPoseidon/issues/new?template=bug_report.yml">Report a bug</a> &middot;
  <a href="https://github.com/DeltaForceT/tPoseidon/discussions">Discussions</a>
</p>

tPoseidon runs every server the game needs (login, channel, master, system, game) on your own machine, so you can play again and host for your friends. We aim for the original game: same screens, same flow, same mechanics, 1x rates out of the box.

## Beta rules

tPoseidon is in beta. These are the rules that matter. The full text is in [BETA.md](docs/beta/BETA.md) and the [LICENSE](LICENSE).

- **Downloads, bug reports and suggestions only.** During the beta the project offers the release packages to download and takes bug reports and suggestions. Code contributions and forks open at 1.0.
- **No forks and no redistribution of modified copies during the beta.** Download it, run it, host it for yourself and your community. Don't publish modified builds, forks or other emulators based on it, and don't re-upload the packages.
- **No wipes.** Your accounts and characters live in your own database. Updates migrate it. Back up before every update.
- **Bring your own KR 2014 client.** We don't ship it. Never share client or game files in this project's spaces.
- **Put your version in every bug report**, for example `0.55.0-beta`. `tposeidon status` prints it.
- **Report security problems privately.** See [SECURITY.md](SECURITY.md). Don't post them in issues or on the forum.
- **Be respectful.** Everyone follows the [Code of Conduct](CODE_OF_CONDUCT.md).

## Where we are

You can make a character and play through a good part of the game, but you will hit bugs and missing systems. [docs/beta/SYSTEMS.md](docs/beta/SYSTEMS.md) lists what works, what is built but needs testing, and what isn't built yet. Read it before you start. When something breaks, [How to report a bug](docs/beta/how-to-report-a-bug.md) explains what we need from you.

## Requirements

- **PostgreSQL 14 or newer**, installed by you. It holds accounts and characters. [docs/install/postgresql.md](docs/install/postgresql.md) has short steps for Windows, Linux, macOS and Docker.
- **The KR 2014 game client**, your own copy. tPoseidon reads the game data it needs from your client folder while it runs.
- Nothing else. The release packages carry their own .NET runtime.

| | Windows | Linux | macOS |
|---|---|---|---|
| Servers | x64 | x64, arm64 | Intel, Apple silicon |
| `tposeidon` command line | yes | yes | yes |
| Control panel | yes | packaged, untested | packaged, untested |
| Game client | yes | Wine | Wine or CrossOver |

Most of our testing happens on Windows. The Linux and macOS builds come from the same code, but fewer people have run them, so reports from those systems help a lot.

## Getting started

1. Install PostgreSQL if you don't have it yet. Note the admin login and its password.
2. Download the package for your system from the [Releases](https://github.com/DeltaForceT/tPoseidon/releases) page and unpack it somewhere you can write to, like your home folder.
3. Run setup in a terminal in that folder: `tposeidon setup` (`./tposeidon setup` on Linux and macOS).
4. Setup asks where your client is, who is going to play (this PC, your LAN or the internet), how to reach PostgreSQL, a name and password for your game account, and which account Play logs in with (the one you just made, unless you pick another that already exists). It then reads the game data it needs from your client and creates the database tables. The PostgreSQL admin password is only used for that and isn't saved.
5. Start the servers:

   ```
   tposeidon start
   tposeidon status
   ```

6. Run `tposeidon play` to start your client pointed at your server. It logs in by itself with your Play account. The control panel shows that account next to the Client button, and you can pick another one there, or run `tposeidon play --account <name>`.

### How Play logs in

The original game logged you in through the publisher's website, and the client never knew your password. tPoseidon does the same job itself. When you press Play (or run `tposeidon play`) on the server's computer, it leaves a one-time pass for your Play account in the database and then starts the client. The client logs in with a built-in placeholder password, and the lobby accepts that only if a pass is waiting for that account from that address. A pass works once and runs out after about a minute. Everyone else still needs the real password.

Friends who play from other computers run `tposeidon play --server <your address> --account <their account> --client <their client folder>`. It asks for their password, checks it with your server's web port (the same one as the item shop, 9990 by default), and starts their client pointed at your server. The server must run the item shop for this. Their password travels to your server, so put the web port behind HTTPS (see [Hosting securely](docs/install/hosting-securely.md)) when your server is public and pass the secure address with `--web-url https://play.example.org:9990`.

`tposeidon stop` shuts the servers down. Your settings are in `tposeidon.json` next to the program, and you can run `tposeidon setup` again whenever you want to change them.

Opening your server to the internet? Read [Hosting securely](docs/install/hosting-securely.md) first: which ports to open, why floods need protection in front of the server, and how the connection guard and `tposeidon security` work.

## The control panel

`tposeidon-panel` (`tposeidon-panel.cmd` on Windows) opens a control panel. It is packaged for Windows, Linux and macOS, but so far it has only been tried on Windows; the `tposeidon` command works everywhere. It does what the commands do (setup, start, stop, backup), shows every server's log, and has a security window for the connection guard.

## Game master tools

Make a character your GM character with `tposeidon gm --character <name>`. After that you can run the GM commands from the command line (`tposeidon gm <command>`), from the panel's **GM** button, or by typing the GM chat commands in game.

The GM window finds a player and lets you kick, block, sanction, move, penalize or give items. It spawns monsters, NPCs and items by name from pickers, sends notices, runs OX quizzes, plans EXP and ability events, looks after guilds and registers GM characters. Every action shows what the server answered, and the ones you can't take back ask first. Its Help tab lists the chat commands.

## NPC scripting

You can add NPCs, quests and events without touching C#. Scripts are plain text files (`.tscript`) that the game server loads at start-up and reloads while it runs. Check them with `tposeidon scripts check` and reload with `tposeidon scripts reload`.

The [scripting reference](docs/scripting/README.md) has the language, and [four examples](docs/scripting/examples) show a greeter, a daily login reward, a fetch quest and a GM event.

## Reporting bugs and ideas

- **Bug:** use the [Bug report form](https://github.com/DeltaForceT/tPoseidon/issues/new?template=bug_report.yml), or post on the forum with [our template](docs/beta/forum-bug-report-template.md). Include your version, your system, the steps and the server logs.
- **Idea:** use the [Feature request form](https://github.com/DeltaForceT/tPoseidon/issues/new?template=feature_request.yml).
- **Stuck on setup?** Ask on the forum or in [Discussions](https://github.com/DeltaForceT/tPoseidon/discussions). The issue tracker is for bugs.
- **Security problem?** See [SECURITY.md](SECURITY.md).

## Building from source

You need the .NET 8 SDK.

```
dotnet build tPoseidon.sln
dotnet test tPoseidon.sln
```

A few tests read local reference data that isn't in the repository. They skip themselves, with a reason, when it's missing. During the beta the source is here to read, build and test, and to prepare bug reports; see [CONTRIBUTING.md](CONTRIBUTING.md).

## How it was made

tPoseidon was built through reverse engineering and our own packet captures. No original code, binaries or game files are included in this repository or in the release packages.

## License

During the beta, tPoseidon uses its own [beta license](LICENSE). It's free to download, run and host for yourself and your community. What you can't do yet is publish modified versions, forks or other emulators built from it, or re-upload the source or the packages somewhere else. We plan to move to an open-source license (AGPL) at 1.0.

Trickster Online, its client, art, music, data and trademarks belong to their owners. They aren't part of this project and the license doesn't cover them; see [NOTICE](NOTICE). tPoseidon is a fan project and has nothing to do with the game's owners.
