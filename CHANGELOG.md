# Changelog

Every release is listed here, newest first. Versions look like `0.MINOR.PATCH-beta`; [BETA.md](docs/beta/BETA.md) explains what the numbers mean. Back up your database before every update. A release that changes the database says so at the top.

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
