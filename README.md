<p align="center">
  <img src=".github/assets/logo.png" alt="tPoseidon" width="480">
</p>

<p align="center"><strong>A server emulator for the Trickster Online KR 2014 client. Run your own world, the way it used to be.</strong></p>

<p align="center">
  <a href="https://github.com/DeltaForceT/tPoseidon/actions/workflows/ci.yml"><img alt="Build" src="https://github.com/DeltaForceT/tPoseidon/actions/workflows/ci.yml/badge.svg"></a>
  <a href="https://github.com/DeltaForceT/tPoseidon/releases"><img alt="Latest release" src="https://img.shields.io/github/v/release/DeltaForceT/tPoseidon?include_prereleases&label=release"></a>
  <a href="LICENSE"><img alt="License: beta license" src="https://img.shields.io/badge/license-beta%20license-blue"></a>
  <img alt="Platform: Windows" src="https://img.shields.io/badge/platform-Windows-lightgrey">
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
- **Put your version in every bug report**, for example `0.75.0-beta`. `tposeidon status` prints it.
- **Report security problems privately.** See [SECURITY.md](SECURITY.md). Don't post them in issues or on the forum.
- **Be respectful.** Everyone follows the [Code of Conduct](CODE_OF_CONDUCT.md).

## Where we are

You can make a character and play through a good part of the game, but you will hit bugs and missing systems. [docs/beta/SYSTEMS.md](docs/beta/SYSTEMS.md) lists what works, what is built but needs testing, and what isn't built yet. Read it before you start. When something breaks, [How to report a bug](docs/beta/how-to-report-a-bug.md) explains what we need from you.

## Requirements

- **PostgreSQL 14 or newer**, installed by you. It holds accounts and characters. [docs/install/postgresql.md](docs/install/postgresql.md) has short steps for Windows, Linux, macOS and Docker.
- **The KR 2014 game client**, your own copy. tPoseidon reads the game data it needs from your client folder while it runs. With the language on Automatic, Play looks at the client's own game data (`data/libconfig_eng.dat` if the client has it, otherwise `data/libconfig.dat`) and starts the client in Korean mode when that data is Korean (the original client) and in English mode when it is translated. When both files exist and the English one is not Korean, English mode wins, so a client whose `libconfig.dat` is Korean and whose `libconfig_eng.dat` is English runs in English with the English text. The **Client language** choice (the panel's settings menu, the "..." menu and the quick-access bubble, all the same list) sets it yourself. English mode loads `data/libconfig_eng.dat`, `data/libconfig_eng.idx` and `data/tahoma12.fnt`; Korean mode loads `data/libconfig.dat` and `data/libconfig.idx`. You do not need both sets: when the mode that is chosen lacks its files, Play adds them as copies of the other set (`libconfig.dat`, `libconfig.idx` and `gulim12.fnt` for English mode; `libconfig_eng.dat` and `libconfig_eng.idx` for Korean mode). The servers build their world data and read their NPC, monster and quest text from the very file the client loads in that mode, and rebuild it when that file changes. Play writes which mode it chose, and why, to the panel log or the console. Nothing is downloaded, and files the client already has are never replaced, with one exception: the client data has no map rows for the Shadow Sanctuary and Harcon Sanctuary maps (549 to 556 and 562 to 564), so setup and Play add exactly those 22 rows (11 in `MapInfoEx`, 11 in `MonsterScenario`) to the libconfig file of the chosen mode and rewrite its index. The first time, the original `.dat` and `.idx` are kept next to them as `.tposeidon-original`; a client that already has the rows is not touched, and nothing is changed while the client is running. The servers rebuild their world data once, because that file changed. The client also has an old Korean server address built in, which it always connects to; Play writes your server's address into both of the client's built-in addresses in your own `Trickster.bin` before starting it and keeps the original next to it as `Trickster.bin.tposeidon-original`. The client always uses lobby port 9980. Setup scans the client folder as soon as you choose it and lists what is missing or wrong (the client program, the libconfig set, the charts, the map folder, the address, the world data the server builds from the maps and the NPC tables); a **Fix** button repairs what has a known fix and keeps a backup of everything it changes. Run it any time with `tposeidon client check`, and `tposeidon client fix` (add `--restore` to put the originals back).
- Nothing else. The release packages carry their own .NET runtime.

Releases are for Windows (x64) only. Linux and macOS packages will come with version 1.0, after we have tested them ourselves.

## Getting started

1. Install PostgreSQL if you don't have it yet. Note the admin login and its password.
2. Download the Windows package from the [Releases](https://github.com/DeltaForceT/tPoseidon/releases) page and unpack it somewhere you can write to, like your home folder.
3. Run setup in a terminal in that folder: `tposeidon setup`.
4. Setup asks where your client is, who is going to play (this PC, your LAN or the internet), how to reach PostgreSQL, a name and password for your game account, and which account Play logs in with (the one you just made, unless you pick another that already exists). It then reads the game data it needs from your client and creates the database tables. The PostgreSQL admin password is only used for that and isn't saved.
5. Start the servers:

   ```
   tposeidon start
   tposeidon status
   ```

6. Run `tposeidon play` to start your client pointed at your server. It logs in by itself with your Play account. The control panel shows that account next to the Client button, and you can pick another one there, or run `tposeidon play --account <name>`.

### How Play logs in

The original game logged you in through the publisher's website, and the client never knew your password. tPoseidon does the same job itself. When you press Play (or run `tposeidon play`) on the server's computer, it leaves a one-time pass for your Play account in the database and then starts the client. The client logs in with a built-in placeholder password, and the lobby accepts that only if a pass is waiting for that account from that address. A pass works once and runs out after about a minute. Everyone else still needs the real password.

Friends who play from other computers can use the launcher below, or run `tposeidon play --server <your address> --account <their account> --client <their client folder>`. It asks for their password, checks it with your server's web port (the same one as the item shop, 9990 by default), and starts their client pointed at your server. The server must run the item shop for this. Their password travels to your server, so put the web port behind HTTPS (see [Hosting securely](docs/install/hosting-securely.md)) when your server is public and pass the secure address with `--web-url https://play.example.org:9990`.

### Joining a friend's server

Friends do not need the server package, PostgreSQL or the command line. They download the separate launcher, `tposeidon-launcher-<version>-win-x64.zip`, unpack it and open `tposeidon-launcher.cmd`. They need their own KR 2014 client and an account on your server.

The launcher is one window. The top shows the server's banner, name, colour and status (Online, Offline or Maintenance) and its news; the right side is the login card with a large Play button and a progress bar for updates. The gear in the corner opens the settings: client folder, client language (Automatic, Korean or English) and the list of servers.

1. Pick the client folder in the settings. The launcher says what is missing when the folder is not a usable client.
2. Add your server: either type its address (and, for an HTTPS server, its web address), or press **Import file...** and choose the server file you gave them. Opening a server file with the launcher on the command line also imports it.
3. Enter the account name and password and press **Play**.

The button logs in over your server's web port first. Once the login is accepted, the launcher checks your published update (see [Updating your players' clients](docs/install/launcher-updates.md)) and downloads what is missing by itself, with a progress bar. **Play** unlocks only when every file is installed and verified; if the download fails the launcher says why and offers **Retry**, and never starts a half-updated client. Play then writes your server's address into their own client and starts it. The launcher remembers the client folder, the servers, the last account and the language in its own settings file in their user data folder (`tPoseidon/launcher.json` under `%APPDATA%` on Windows). It never stores the password. If something is wrong it says which part: wrong password, server unreachable, web port closed, not the KR 2014 client, client folder not writable, the client already running or an update that failed its checksum.

### Make a launcher for your players

Instead of asking players to add your server by hand, build a launcher that is yours. In the panel's menu (the three dots) choose **Make a launcher for your players...** and go through five steps:

1. **Identity**: your server's name, the text in the launcher's title bar and your logo (a PNG). The shortcut and window icon are made from the logo.
2. **Look**: a background picture (optional) and an accent colour. A live preview on the right shows what your players will see.
3. **Connection**: your public address and web port. **Check connection** fetches `/launcher/manifest.json` from them. A server that only accepts players on this computer is refused, because nobody else could use the launcher.
4. **Links and text**: your website, your Discord, a welcome note and the default client language.
5. **Build**: pick a folder. You get `<Server name> Setup.exe`, an installer to give to your players, and `<server>-launcher-<version>.zip` with the same launcher unpacked.

To try it at once, tick **Use the tPoseidon look (test)** at the top of the wizard. It fills the logo, background, colours, title and icon with tPoseidon's own, so you can build and test a launcher without any pictures and change it later.

What your players get:

- The installer needs no administrator rights. It installs to `%LOCALAPPDATA%\Programs\<Server name>`, adds Start menu and desktop shortcuts with your server's name and icon, and lists the launcher in Windows' installed apps with an uninstaller. It will not install over a launcher that is running.
- The launcher is locked to your server. Players cannot add servers or change the look. They pick their game client folder, log in, and open **Options**.
- **Options** are the settings the original client's setup tool offered: resolution (800 x 600 or 1024 x 768), full screen, effects and map effects, sound off, sample rate, bits, channels, screen shot format (BMP or JPG) and JPG quality. They are saved in the registry under the client's own profile (`Trickster_gsp` for the English mode, `Trickster_NT` for the Korean mode), the same place the original tool writes; the launcher reads where that is from the client itself, and apply the next time the game starts. They cannot be saved while the game is running.
- After login the launcher downloads any update you published before Play unlocks. Updates are required.

- The launcher and the setup program show your icon in Explorer and carry your server's name in their file details.
- Every publish is signed with a key kept in your install folder and copied into your backups. Launchers you build carry the public key, so a hijacked web address cannot push files to your players. The Publish page can change the key without breaking the launchers you handed out.
- After you rebuild the launcher, **Hand it out as an update** (finished page) and **Publish** make your players' launchers update themselves: they download the new files, check them and restart into them, keeping the earlier launcher for one step back. See [Updating your players' clients](docs/install/launcher-updates.md).
- A test launcher (built for this computer only) is marked, and the panel warns if you try to hand it out.

The wizard needs the launcher pieces that come with a released tPoseidon package (the `launcher-kit` folder next to the panel). A launcher without a `branding` folder next to it stays the open launcher described above, for testing.

To make the server file, open the panel's menu (the three dots) and choose **Share a server file for friends...**, or run:

```
tposeidon server-file [--name <text>] [--note <text>] [--color <#RRGGBB>] [--web-url <url>] [--output <file>]
```

The file holds your server's name, its public address from setup, the web address players log in through (`http://<address>:9990` unless you pass another, such as your `https://` one), an optional accent colour and an optional note. It holds no passwords or secrets, so you can post it in a chat. It needs the audience set to LAN or the internet, the item shop on, and the web port reachable from your friends' computers. Because their password travels to that port, use HTTPS for a public server (see [Hosting securely](docs/install/hosting-securely.md)).

To give your players news, a banner and your own content files, drop them in the `updates` folder and press **Publish launcher update** in the panel menu, or run `tposeidon launcher publish`. Only publish your own files, never the original game files. [Updating your players' clients](docs/install/launcher-updates.md) has the details.

`tposeidon stop` shuts the servers down. Your settings are in `tposeidon.json` next to the program, and you can run `tposeidon setup` again whenever you want to change them.

Opening your server to the internet? Read [Hosting securely](docs/install/hosting-securely.md) first: which ports to open, why floods need protection in front of the server, and how the connection guard and `tposeidon security` work.

## The control panel

`tposeidon-panel` (`tposeidon-panel.cmd`) opens a control panel. It does what the commands do (setup, start, stop, backup), shows every server's log, and has a security window for the connection guard.

### The quick-access bubble

Minimising the panel shrinks it into a soap bubble at the centre of the screen. The bubble's ring shows the servers at a glance (green all running, amber some running, grey stopped, red a server failed). Drag it anywhere; it remembers its place. Click it to unfold four groups of round buttons: **Servers** (start all, stop all, restart all, logs), **Game** (game master tools, client language, launch the client), **Players** (share a server file, make a launcher, edit launcher news and updates, publish launcher update) and **Panel** (open the panel, security, back up the database, open the data folder, exit). The panel's "..." menu keeps **Client language** while the bubble is on; it is the same list as the bubble's, with the same current choice ticked. Each tool opens in its own window, and pressing the button again brings that window to the front. **Open the panel**, or the panel's taskbar button, brings the panel back and the bubble goes away. When a server stops unexpectedly or fails to start, the bubble pulses and shows a short note; click the note to open the logs. The bubble never appears during first-time setup, and closing the panel closes it as usual.

The **Bubble** toggle in the panel's top bar turns the bubble on or off (on by default). While it is on, the panel keeps start, stop and restart all, the game master tools and the client launcher, and leaves the other tools to the bubble. Turn it off and they come back to the panel. The launcher tools (make a launcher, edit launcher news and updates, publish launcher update) are always in the **Launcher** entry of the panel's menu (the three dots), whether the bubble is on or off.

## Game master tools

Make a character your GM character with `tposeidon gm --character <name>`. After that you can run the GM commands from the command line (`tposeidon gm <command>`), from the panel's **GM** button, or by typing the GM chat commands in game. The KR 2014 client has no GM chat of its own, so the server reads them from normal chat lines that start with `@` (`@login <password>`, `@kick`, `@speed`, and so on; the panel's Help tab lists them). `tposeidon gm game-flag on` (or the panel's GM accounts tab) turns the GM flag and the GM eagle on without typing anything, `tposeidon gm fly on` gives the eagle its flying form, and `tposeidon gm learn-skill` teaches a character a skill.

The GM window finds a player and lets you kick, block, sanction, move, penalize or give items. It spawns monsters, NPCs and items by name from pickers, sends notices, runs OX quizzes, plans EXP and ability events, looks after guilds and registers GM characters. Every action shows what the server answered, and the ones you can't take back ask first. Its Help tab lists the `@` chat commands.

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
```

During the beta the source is here to read and build, and to prepare bug reports; see [CONTRIBUTING.md](CONTRIBUTING.md).

## How it was made

tPoseidon was built through reverse engineering and our own packet captures. No original code, binaries or game files are included in this repository or in the release packages.

## License

During the beta, tPoseidon uses its own [beta license](LICENSE). It's free to download, run and host for yourself and your community. What you can't do yet is publish modified versions, forks or other emulators built from it, or re-upload the source or the packages somewhere else. We plan to move to an open-source license (AGPL) at 1.0.

Trickster Online, its client, art, music, data and trademarks belong to their owners. They aren't part of this project and the license doesn't cover them; see [NOTICE](NOTICE). tPoseidon is a fan project and has nothing to do with the game's owners.
