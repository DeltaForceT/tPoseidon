# Changelog

Every release is listed here, newest first. Versions look like `0.MINOR.PATCH-beta`; [BETA.md](docs/beta/BETA.md) explains what the numbers mean. Back up your database before every update. A release that changes the database says so at the top.

## 0.60.0-beta

**This release changes the database.** It adds the guild depot, the guild shop and the first-kill date of bosses. The changes apply on the first start, once; `tposeidon start` warns first and offers a backup. Back up your database before you update. The first start also rebuilds the server's world data, so it takes a little longer.

### Fixes from your reports

- Korean text: NPC names and dialogue, monster and character names and quest names reach a Korean client as Korean instead of question marks or garbled letters. Korean game data is also read correctly when its file says UTF-8 but holds Korean text.
- Galder coupons work: using one adds its galder, up to the server's galder limit.
- Master Mong appears in the Coral Beach boss room. Boss rooms place their bosses from the game's own boss tables.
- Leaving the Coral Beach boss room no longer traps your character. Warps use the original arrival rule, and a character that logs in where it cannot stand is moved to a safe place.
- Game master window: players are looked up through the game servers, so online players are found and teleports work. Give item says what really happened, and `@item` gives an item in game.
- Monsters keep their place when you leave a map and come back, instead of jumping back to their spawn points.
- NPCs no longer stand on top of each other in shops such as Paradise Shop.
- Monsters in the job change zone with the four Skill Masters no longer get stuck: a monster may step diagonally between two blocked cells, as in the original.
- Clicking a portal stops your attack, and hits use the target's real position.

### New

- Guild depot: a shared storage with tabs and a history of deposits and withdrawals. `tposeidon guild-depot` sets a guild's number of tabs.
- Guild shop: guilds spend guild coins on member items and depot tabs. `tposeidon guild-coins` shows a guild's coins or adds some.
- Guild games: Monster Match, team respawn regions and watching a match.
- My Camp opens with its items, and the camp tab of My Shop lists them. `tposeidon camp` lists or adds My Camp items.
- Inspecting a player shows their boss kill list (when they share it) and their cover letter. Your own boss history window lists your boss kills with the date of the first kill. You can write a cover letter of up to 200 characters.
- Collect all 16 Secret Cards and exchange them for the secret map in the secret tab of My View.
- The game master window is grouped into tabs, and its item, monster and NPC pickers show icons, categories and pictures from your client.
- New player launcher (`tposeidon-launcher-<version>-win-x64.zip`) so friends can join a server without the server package: pick the client, add a server by address or server file, log in and press Play. Make the server file in the panel (**Share a server file for friends...**) or with `tposeidon server-file`.
- The launcher shows the server's banner, name, colour, status and news. After login it downloads the server owner's own files with a progress bar, resumes, checks them and keeps a backup you can undo; **Play** unlocks when everything is installed, and a failed download shows **Retry**. Owners put files in the `updates` folder and publish them from the panel or with `tposeidon launcher publish`. See `docs/install/launcher-updates.md`.
- Build your own launcher: **Launcher > Make a launcher for your players...** in the panel menu. The wizard (identity, look with a live preview, connection check, links and text, build) makes `<Server name> Setup.exe`, an installer with shortcuts and an uninstaller, and a zip of the same launcher. The launcher is locked to your server, carries your name, logo, colours and links, and lets players log in and open **Options** with the settings the original client setup offered. **Use the tPoseidon look** fills everything in so you can try it at once. A progress bar shows the build, and a finished page opens the folder. For a server that only accepts players on this computer, the wizard lets you open it to other players, or build a test launcher marked `-test-this-pc` that only works on your PC.
- **Launcher > Edit launcher news and updates...** in the panel menu: write news posts with pictures, pins and order, set the banner and a maintenance message, add the files players receive, and publish with a summary first. Every publish is kept in a history.
- Quick-access bubble: minimise the panel and it shrinks into a bubble in the middle of the screen. The bubble shows the server state, pulses when a server fails, and opens grouped buttons for the servers, the game, your players and the panel; each tool opens in its own window. **Open the panel** brings the panel back. Turn it on or off with the **Bubble** button in the panel's top bar. While it is on, the panel keeps start, stop and restart, the game master tools and the client button, and leaves the other tools to the bubble.
- When the panel is too narrow for every button, the top bar shows icons only, with the names in the tooltips.
- The server rebuilds its world data by itself when the data was built by an older version.

## 0.56.0-beta

No database changes.

- Korean clients now show Korean text. Play starts the client in its own Korean mode when the client's game data is Korean, and in English mode when it is translated. Before, every client started in English mode, which draws Korean text as garbled Latin letters.
- The panel's settings menu has a **Client language** choice: automatic (the default), Korean or English. Existing installs move to automatic.
- Play now writes the server address into both of the client's built-in addresses, the English one and the Korean one.

## 0.55.2-beta

A fix release for clients that never reached the server. No database changes.

- Fixed: the KR 2014 client ignores the server address it is started with and always connects to an old Korean server address built into `Trickster.bin`, so it stopped after the loading bar on every PC that did not redirect that address. Play now writes the server's address into the player's own client before starting it (the local server, or the host given to `tposeidon play --server`). The original is kept as `Trickster.bin.tposeidon-original`.
- The client always connects to lobby port 9980. Play now says so instead of starting a client that cannot connect when the lobby port is changed.

## 0.55.1-beta

A fix release for clients that would not start. No database changes.

- Fixed: clients without the English-mode files (`data/libconfig_eng.dat`, `data/libconfig_eng.idx`, `data/tahoma12.fnt`) crashed or closed right after Play. Setup and Play now add them as copies of the client's own `libconfig.dat`, `libconfig.idx` and `gulim12.fnt`.
- The one-time login pass that Play gives the client now lasts five minutes instead of one, so a client that takes long to load still logs in.

## 0.55.0-beta

The first public beta, released as v0.55 Beta. It targets the KR 2014 client. [docs/beta/SYSTEMS.md](docs/beta/SYSTEMS.md) has the full list of what works, what needs testing and what is missing.

### Setup and hosting

- Release packages for Windows (x64), Linux (x64 and arm64) and macOS (Intel and Apple silicon). They carry their own .NET runtime; you only install PostgreSQL.
- `tposeidon` command line: `setup`, `start`, `stop`, `status`, `backup`, `play` and `gm`. Setup asks where your client is, creates the database and reads the game data it needs from your client. Nothing from the game is shipped.
- A control panel (Windows) with setup, start and stop, backups, a log view for every server and a security window.
- Setup creates your first account and sets it as the account Play logs in with. Play gives the client a one-time login ticket, so the client's automatic login only works for the computer that pressed Play.
- Connection guard for servers open to the internet: connection and packet limits, timeouts, login throttling and temporary bans. See [Hosting securely](docs/install/hosting-securely.md).
- Every package comes with a manifest and SHA-256 hashes, and the build version shows in `tposeidon status` and the panel.
- Database changes apply on start, each one only once. `tposeidon start` warns first and offers a backup.

### Accounts and characters

- Login, server list, channel list, character creation, selection and deletion.
- The channel checks the client version and the login that came from the lobby, and refuses others with the original error codes. Blocked, penalized and already-logged-in accounts get the original refusal messages.
- Chat, trade and move blocks that are still running are handed to the client at login.
- Looking at another player's profile and equipment, and the options that hide them.

### World and combat

- Walking, running, resting, emotes and the posture shortcuts, portals and map changes, with other players, NPCs and monsters appearing around you. The world map window.
- Combat with melee, hammers and guns, damage, monster deaths, drops and pick-ups, experience and level-ups, death and restart in town.
- No caps on the server: damage, HP, MP, EXP, TM EXP and Galder are 64-bit on the server and in the database. The client shows at most 2,147,483,647 of any value, and the server sends that maximum instead of letting the number wrap. Optional limits are in `data/server_rates.json`, where `0` means no limit.
- Player versus player in PK zones, and 1v1 duels.
- Skills: learning from cards, the skill shop, mastery and job change.

### Items and economy

- Bag, equipment, NPC shops, bank, warehouse, mail and gifts.
- Refining, compounding, reforming, melting, composing and decomposing, slot extension, recycling and coupon exchange.
- Special-use items, key boxes with their window, gift boxes and the microphone.
- Unique and absolute unique items and their duty rules.
- Player shops with search, the find-shop helper, visitor counters and stock updates for every visitor, and My Camp.
- Cash shop through a small built-in web shop (the catalogue starts empty).

### NPCs and quests

- Talking to NPCs, shops and quests: take, progress and turn in. Party quests. Monster quests with a weekly ranking.
- A large part of the original NPC functions, including warp gates, limited teleports, summons and daily checks.
- NPC scripting: write your own NPCs, quests and events in plain text files, with `tposeidon scripts check` and `tposeidon scripts reload`. See the [scripting reference](docs/scripting/README.md).

### Social

- Parties, the party recruit board and friends across game servers, including where a friend is. Guilds, instant chat rooms, whispers and notices.
- Guild games and the guild ladder.
- Couples, the wedding hall, the wedding board, marriage options and divorce.
- Angels (the newbie helper system).

### Progression and minigames

- Drilling and auto drilling, surprise spots and treasure maps.
- Fortune cards, the card identify game and card battles with their rewards.
- Soul seeds, the soul system and honor titles.
- Fiesta, memory ports and puzzle maps.
- Harcon Defence.
- Pets, pet training and collection, and guardians with their cards. Guardians grow and cast their card skills.

### Game master tools

- GM chat commands start with `@` (`@login <password>`, `@kick`, ...), because the KR 2014 client keeps every `/` line for itself. A GM line is never shown to other players, and only the registered GM character with its account password from a permitted address can log in. The GM eagle can be switched on and off from the panel or with `tposeidon gm game-flag on|off`.
- Teleport, kick (with a reason), blocks by type and sanctions, penalties, memos and notices.
- Spawn monsters, NPCs and items, on the GM, on a map or on chosen coordinates, summon, hide and show NPCs, set the music.
- OX quizzes, EXP and ability events, guild tools and GM character registration.
- Everything is available as `tposeidon gm <command>` and in the panel's GM window.

### Known gaps

- Some systems are built but have not been played live yet. They are marked in SYSTEMS.md.
- A few original features are not built yet, among them party follow, Shadow World, the guild depot, the guild Monster Match and watching a guild game. SYSTEMS.md says which and why.
- The control panel has only been tried on Windows.
- Linux and macOS packages are built the same way but have been tested less.
