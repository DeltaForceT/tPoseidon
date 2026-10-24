# tPoseidon beta: system status

Last updated: 2026-10-24

This is where we say what works and what doesn't. The target is the KR 2014 "Original" variant: the aim is to behave like the original server, not to invent new rules.

Three lists:

1. **Working** - built and confirmed in live play with a real client.
2. **Built, needs testing** - built and passing our own tests, but nobody has played it live yet. Expect rough edges. These are the ones we most want you to try.
3. **Not built yet / partial** - known gaps. No need to report these as bugs.

Three systems are marked **reconstructed**: Harcon Defence, the slide-puzzle shuffle and pet collection. The original server code for these isn't available, so behaviour may differ from the original.

How to report a problem: see [how-to-report-a-bug.md](how-to-report-a-bug.md).

---

## 1. Working

### Account and characters
- Login, server select, character list, create, delete and select
- Entering the world from the character screen

### World and movement
- Walking, running and click-to-move
- Portals and map changes
- Other players, NPCs and monsters appearing and disappearing around you
- Map chat

### Combat and skills
- Melee combat basics: attacking monsters, damage, monster death
- Experience from kills and level-up
- Dying and restarting in town

### Items and economy
- Opening gift boxes
- Maturing compound
- Picking up drops

### NPC services
- Talking to NPCs
- NPC shops (buy and sell)
- Quests (take, progress, turn in)

---

## 2. Built, needs testing

A short "try" hint is given where one is obvious.

### Account and characters
- Channel login check: the character list is only sent to a client that logged in on the lobby and picked a server; the channel checks the lobby's authcode as the original servers do and refuses others with the original error codes (60100 unknown account, 60101 wrong authcode, 60102 no server picked). A second login of the same account on the channel closes the first one - try logging in twice from two clients. The channel also checks the client version as the original LoginServer does (the KR 2014 client's version by default, `Version = major.minor` in LoginServer.ini for another client) and refuses a missing or different version - start an older or newer client against the channel
- Waiting at character select: the channel pings the client every 60 seconds like the original LoginServer; a client that stops answering is disconnected - leave a client at character select for 15 minutes
- Lobby login refusals: a wrong name or password, a blocked account and a withdrawn account get the original refusal code instead of a dropped connection; a penalized account sees the penalty name and its release time and cannot log in until the penalty ends (operators add penalties with `tposeidon gm penalty-kind`, `penalty-time`, `penalty`, `penalty-release` and `penalty-list`); an account that is still logged in elsewhere is refused (60013), as on the original servers - log in twice with the same account, and penalize a test account
- Account login states follow the original servers: the lobby clears states left by a crash when it starts and when a player leaves the lobby before picking a server, the channel login moves the account to "in game", and a channel restart or leaving the channel before choosing a character logs the account out
- Blocks at login: a chat, trade or move block that is still running when the character is selected is handed to the client with its minutes left, as on the original servers, and the client keeps counting it down - block a character for chat, relog, and try to chat
- Stat point spending - spend points after a level-up, then relog to check they stuck
- Relogging with HP and TM exp saved - log out mid-hunt and log back in

### World and movement
- Zone changes between game servers - walk through a portal that leaves the current server
- Teleport NPCs and the quest return warp
- Sector visibility (3x3) - crowded maps, check nothing pops in late
- Sitting and lying down shown to other players - sit while a friend watches
- Time-of-day map colours

### Combat and skills
- Class skill sets: first-tier job skills, buffs, debuffs, heals, shields, party auras, scroll, thrown, gun, field, ground-target and chain-hit skills - try your class skills on a monster and tell us which misbehave
- Polar bear skills: hammer gauge, Crazy mode, buffs - play a polar bear
- Gun normal attack and the common slot (bullets, throw items) - equip a gun and shoot
- Knockbacks, status effects (stun, bind, fear and so on) and skill riders
- Monster skills, monsters fighting back, monster debuff resist
- Skill level-up from the skill window, and learning a skill from a skill card
- Critical hit effect, sound and screen shake
- Player vs player: PK zones, skirmish and peace toggle, PK score and kill counts - try with two characters in a PK zone
- Restart in place with a rebirth item, and rebirth item on another player

### Items and economy
- Equip and release, set item bonuses, item properties, element and resist slots
- Potions and stat scrolls
- Throwing items onto the ground
- Account bank and character warehouse - deposit, withdraw, move between tabs
- Refine, mix reset, compound, exchange shop, VIP (egg) shop
- Repair of broken equipment in the refine window: repair powder, as many as the refine level (at least one)
- Phantom academy refine (NPC function 270): the refine window of the phantom academy NPC also repairs Neo equipment up to level 185 with one phantom repair powder (**reconstructed**: the original servers have no code for it; the rules follow the NPC's own explanation)
- Equipment reform, composing, slot extend, melting, tempering - bring spare gear
- Chaos equipment fusion (NPC function 256): two of the same chaos, requiem or other-world item (refine index 35, 54 or 64) fuse for twice the item's value in gelder with a 70 % chance (plus 30 % with Williel's gauge); on success the stats of the two are mixed (refine index 64 keeps the better of each), on failure the second item is lost and the first stays as it was; the refine level and the look of the first item are kept, as on the original servers
- Equipment decomposing (NPC function 255): with one Craftsman's Flame a composed item gives its look back as a new item (the look's own item when it may come back, otherwise the dummy skin of its kind from DecompoundDummyItem carrying the look) and keeps its other properties
- Pet composing (NPC function 212): the first pet keeps its stats and takes the look of the second pet, which is used up together with one pet composing book; the second pet's level must be at least the first one's; the gift item of PetComposingPresentInfo is given afterwards, as on the original servers
- Ghost exchange, growth stones, Neo equipment
- Recycle coupon exchange
- Equipment Recycling (NPC service 206): items to recycle coupons
- My Camp Recycling (NPC service 207): camp items to recycle coupons, one coupon each
- Timed items counting down and expiring
- Cash item box (list and take), level-up gift boxes arriving in the mail
- Unique and absolute unique items, boss seals, duty expiry notices
- Monster money and item drops, legend monster drops
- Cash item and episode/event item catalogs (for example the Wooden Sword)
- Gifts: send, receive, pending and sent lists - send one to a friend
- Player-to-player trade (items marked not exchangeable should be refused)
- Player shop, shop signs, remote search and buy
- Memory port: save, delete, teleport

### NPC services
- Look change and disguise kits
- Skill shop, skill masters, job change and job-change quests
- NPC movement, job checks and script functions; the day and night talk of event NPCs (script function 87) follows the 60-minute game day of the client's DailyTime table (**reconstructed**: the original servers' clock start is not known, the day here starts at midnight UTC)
- NPC script functions of the guild NPCs: the guild-making check (function 53: level 35 and the GuildSetupInfo money, MainServer row) and the guild windows (functions 54-58 and 79-81: dissolve, the guild list, the recruit board, cancel an application, leave, change master, password and name) open as on the original servers
- Event talk and event scene NPCs (functions 223 and 254): the client plays the NPC's event and the NPC answers with the message of the script when it ends
- The soul seed check of NPCs (function 224) and the card battle rank notice (function 21)
- Limited teleports (functions 88, 110 and 227): the NPC moves the player to the chosen destination of its teleport list for its fee only while that zone holds fewer than 300 / 50 / 30 players, as on the original servers; the count comes from the game server of that zone through the MasterServer
- Party zone entrance NPCs (function 218): a player who killed one of the zone's bosses less than the zone's limitMinutesReEnter (PartyZoneInfo) ago is refused and told how many minutes are left, as on the original servers; the boss kills come from the database through the MasterServer
- NPC monster summons (function 219, the Trickster NPCs of the forest and office dungeons): the NPC calls the monsters of its NpcSummonMonsterTable row at the player's position while the map has fewer monsters than its MonsterAvrNum, then shows its message, as on the original servers ; on maps of class 2 the summoned monsters attack the player who summoned them, as on the original servers
- The warp gates of the Path of Tribulation NPC: function 117 sends an advanced job to the Room of Guardian of its job (zones 239-246), as on the original servers; function 257 sends it to the guardian room of its character type (zones 628-635, the polar bear to 754) (**reconstructed**: the original servers have no code for 257; the rooms follow their names in the client's map data and the job check of 117 is kept)
- Item time recharge (NPC function 78): recharge a timed cash item with recharge coupons; the choices come from the client's recharge list (libcmgds_rch.dat), at most 4320 hours left after the recharge, as on the original servers (the meaning of two fields of the result frame is **reconstructed** from the client's timed-item record) - bring a timed item and a coupon
- Pet training, retrain and level-up
- Pet reinforce (NPC service 199): add a stat to a free pet slot with a material and up to two aid items
- Pet reinforce reset (NPC service 200): clear one pet slot with the reset material
- Character pet reinforce (in the pet level-up window): a reinforce item turns a character pet into its stronger form
- Pet evolve and merge (in the pet level-up window): evolve a pet into its next form with the evolve material, or merge two of the same pet into one with the better stats

### Social
- Friend list: requests, accept, refuse, delete, online notes
- Party: create, invite, leave, type bonus, exp share, party quests
- Chat lobby and whispers
- Instant chat rooms (right-click your character, Instant Chatting): make a room with a subject, a member limit and an optional password, others join by clicking the sign above your head (with the password when set), chat and whisper inside the room, the room master changes the subject, limit or password and removes members, and the room closes when the master leaves; while in a room a character does not walk, attack or pick up items, as on the original servers; level limits from SystemServer.ini (ChatNoticeMakeLimitLevel, default 20; ChatNoticeJoinLimitLevel, default 10), checked against the level on the game server; a chat or chat-room block stops the client from opening one, as on the original servers - make a room, join it from a second client, chat, kick, close
- Mail box: list, read, compose, count
- Guild: create, apply, answer, members, leave, force out, dissolve, details
- Guild game: rooms, Monster Match, notices; the room list shows each side's ladder rank, member count and average level and the room's team size, title and the original caption; the start notice shows the ladder rank
- Guild game rooms in the 2014 window (**reconstructed**: the original servers have no code for this window): make a room (friendly, league or event match, team size, map, password), join it with the password, invite guild members from the lobby (they can accept or refuse the invitation), remove participants, leave the room; the host starts the match when both guilds have at least 3 participants; only league matches change the ladder score
- Guild league window: the ranking of the top 20 guilds by ladder score (matches, wins, losses, points); the playoff bracket says there is no league match
- Guild game results are saved: battles, wins, losses, draws and disconnects, the guild ladder score (both guilds start at 1000; the winner gains and the loser loses points by the ladder formula of the original servers; after six matches between the same two guilds in one day the score no longer changes) and guild points for the winner; the result window shows each guild's record after the match, the ladder points won or lost (game score) and the new ladder score (league points)
- Angel system: call, accept, warp, graduation at level 31
- Couple and wedding: couple making, closeness points, divorce, wedding hall, wedding board, the lover's position and moving to the lover (married couples, also across game servers; refused while the lover is on a map the original servers close to it)
- Report a player to the GMs

### Progression
- Honor titles and quest-completion grants
- Fortune system: card house round, awards, Star Tears
- Soul system: soul seed, awakening, charge, craft, reinforce
- Tutorial steps and the guide page on first level-up
- Territory missions
- Monster quest ranking (weekly ranking and the rank window)

### Minigames and events
- Drilling: manual and auto, drill life, prizes - dig in a drill map
- Surprise spots: dig spots, treasure maps, crevice maps
- Memory puzzle map - watch and repeat the statue sequence
- Slide puzzle map (**reconstructed**)
- Card identify game, server side
- Card battle: invite, rules, deck, drops, winner copy
- Card battle rewards: EXP and the same amount of TM EXP by the original formulas (the winner by wins, score and level, the loser a quarter of that; a special battle raises it, a too-far battle gives the winner nothing), money for a perfect win, the present item and the won card; the result window shows them
- Card battle special sets (Card_SpecialSet): a deck that holds every card of a set gets that set's bonus (squared streak points, the EXP multiplier, a present item); a winner takes a card of the loser's set out of the loser's bag, and takes the chance card the loser played, and the chance command (play the type 5 card once while behind) works. The present item odds are inferred as one slot per percent of the set's item rates
- NPC skill of the Leviathan quest (function 258, `NpcSkillStart,21000`): shows the NPC buffer effect on the player for the seconds in the skill table, using the effect layout the 2014 client reads; the effect has no stat change
- Fiesta ticket and portal opening
- Harcon Defence (**reconstructed**): room lobby, rooms (join from the room list, the host kicks a member from the member list), start and ready, play maps, HP gauges, boss stage, the record of each stage (plays, wins, losses, total score, average contribution, and the best game's rank, contribution, deaths, tower score and towers built; potions, assists and solo / multi battles are not counted and stay 0); wave monsters walk to the nearest Harcon and attack it (the stage is lost when it falls); a built turret fires its skill at the nearest wave monster in reach once a second, a wave monster that is hit turns on the turret, and a turret's kills count for the player who built it
- Harcon coin shop (NPC function 272, the coin shop NPCs of the Harcon waiting rooms): the window opens with the NPC's shop table (HarconShop_1 or HarconShop_2 from the client's NPC data) and sells its items for Harcon coins, the price being the coins of the row times its item count, as the client checks (**reconstructed**: the original servers have no code for this shop)
- Event emotes, anniversary events, GM buffs

### Pets and guardians
- Pet spawn, follow, emote and recovery
- Guardian rename
- Guardian card-skill order list
- Guardian card save: the cards set in the guardian's skill slots are kept
- Pet collection (**reconstructed**): a 3-star or better pet in the bag eats its food and collects an item from the current map every few seconds while you are online; deeper drilling maps lower the chance; mine maps need a pet and food with the special dig mark (no item in the KR data has it, so mines refuse with the original message); take the items to your bag or throw them away (the pet's items are kept after logout and sent again at login); the filter check boxes of the window are not sent by the client, so they change nothing

### GM and hosting
- GM login, logout, teleport and kick from chat and from the command line tool
- GM kick with a reason: the player sees the reason and is disconnected when his client confirms it 10 seconds later, and his account is marked sanctioned, as on the original servers; GM account sanction on or off by character name (command line tool)
- GM chat commands for the rest of the client's GM keywords: notice to the chat lobby, block a character for a number of days (with or without a reason), chat / move / trade / My Camp / shop / chat room blocks in minutes (0 lifts the block, -1 blocks for 15 days), the chat, move and trade block lists, summon monster, and the command help list
- GM character count of every zone, close a My Camp by character id, and summon an NPC at the GM's position (command line tool)
- GM close of a player's instant chat room, from chat (`/rmvmychat name`) and the command line tool (`remove-chat`); a room member gets the original "master only" refusal instead
- GM summon monster (at the GM position), set music, remove monsters or one monster, close a personal shop or My Camp (command line tool; shop and camp also from chat)
- GM character count of a zone, character info, and a notice to the chat lobby (command line tool); summon, music and remove replies are shown by the tool
- GM hide or show an NPC on a map, list the hidden NPCs, and show them again by zone (command line tool; hidden NPCs stay hidden for players entering the zone until shown again or the server restarts)
- GM issue item: an NPC on the GM's map hands out an item, one per player, until the set count runs out (command line tool)
- GM OX give item: on an OX quiz map, hand items from the GM's bag to every other character on the map (command line tool)
- GM block and unblock a character by type (chat, trade, move, My Camp, personal shop, chat room, normal; every type but normal also works when the character is offline), block with a cause (the player sees the cause and is disconnected when he confirms it), and list the blocks (command line tool); blocks are saved in the database through the MasterServer, as on the original servers (directly when the MasterServer is not connected)
- GM guild tools: guild info, memo to members up to a grade, block a guild's online members, dissolve a guild (command line tool)
- GM guild game switch: close or open the making and joining of guild game rooms (closed lists refuse with the original error 12), and read or set a room's GM flag (command line tool); the lists start open, as on the original servers
- GM EXP events: plan a time window in which every player gets extra EXP and TM EXP from monsters, drilling and skills (quest, party quest and card game rewards are not raised, as on the original servers), list and delete the planned and running windows, or set the extra percentages at once (command line tool); the windows are checked every 5 seconds and are kept in the database, so they survive a SystemServer restart
- GM ability events: plan a time window in which one stat of every player (AP, AC, DX, MP, MA, MD, WT, DA, LK, HP, DP, HV) is raised by a percentage of the value from his stat points, list and delete the windows (command line tool); a window deleted while it runs stays applied, as on the original servers, until another window of the same stat ends; the windows are checked every 5 seconds, kept in the database (they survive a SystemServer restart) and every login receives the current percentages
- Registering a GM character by name
- A game master window in the control panel (the GM button) with every GM tool command: players (search, look up, kick, timed and normal blocks, account sanction, account penalties, move, give items, memos, closing camps, shops and chat rooms), spawning monsters, NPCs and items picked by name from the game data, notices, OX quiz, block lists, map tools, EXP and ability events, guild tools, penalty kinds, and registering, listing and removing GM characters (also `tposeidon gm unregister` and `tposeidon gm list`); it says why it can't connect, asks before anything that can't be undone, and lists the in-game GM chat commands
- Parties and friends across game servers


---

## 3. Not built yet / partial

### NPC services
- The Leviathan quest NPC skill (function 258) only shows its effect: the original servers have no code for it, so the rule that lets the buffed player hurt the quest monster is not known (monsters are hurtable by anyone here)

### Items and economy
- Not included: purchase events, coupons and the Drill Egg gacha; the web shop catalogue starts empty and is up to each server owner

### GM and hosting
- Watching a guild game room: the 2014 client sends the watch request, but it has no reader for the original answers (accepted / refused) nor for the observer notice the original game server sends to the map, so a watcher cannot be shown; not built
- The block list of the command line tool starts empty after a server restart (the saved blocks stay in the database)
- GM spawns at chosen coordinates, on another map without moving the GM there, spread over an area, or removing only what a GM spawned: the server only spawns on the spot where the GM character stands and removes all monsters of a map or one by id (the original order's map field is not understood yet)
- The GM chat keywords move notice, chat colour and monster search: the original servers have no command for them (they were actions of the original GM client, which is not available), so they stay normal chat
- Control panel on Linux and macOS: the panel is packaged for Windows, Linux and macOS, but it has only been tried on Windows so far

## 4. Value limits

The server does not cap game values. Damage, monster HP, player HP and MP (current and maximum), Galder carried and in the bank, EXP, TM EXP and the pools of shields and effects are calculated and stored as 64-bit numbers, and the database columns for them are 64-bit. A hit of 9,000,000,000 takes 9,000,000,000 HP off the target, and a wallet can hold more than 2,147,483,647 Galder.

What limits a number is the client, which cannot be changed in the Original variant. Whatever the server holds, the value sent to the client is cut at the largest number the client reads and never wraps around:

- Every number the client reads as a 32-bit value (damage in the hit result, monster HP, player HP and MP, EXP, TM EXP, Galder in the wallet and in the bank window, quest rewards) is read as a signed number, so the largest value it can show is 2,147,483,647. Above that, the server sends 2,147,483,647 and keeps the real value.
- The attack packet (0x001F) carries a 16-bit damage field. The client never reads it, so it is cut at 32,767 without any visible effect.
- The client works out maximum HP and MP itself from the stat points and clamps the current HP and MP it is sent to that maximum. A monster's HP is clamped to the `MaxHpLv` of its entry in the client's own MonsterParam table, so an HP bar above that value needs the client's table changed too.
- Stat points are 16-bit in the client (65,535 per stat), item stacks are 16-bit (65,535), and the client's level and EXP tables stop at level 400 (TM level 500). The server keeps these limits.
- The client refuses actions above 2,100,000,000 Galder in its own windows (Gelder coupon, selling to an NPC shop, mail attachment, number input). Trades and player shops settle at most 2,147,483,647 Galder because the amount is sent to the client.

Optional limits are in `data/server_rates.json`, section `Caps`: `MaxDamagePerHit`, `MaxHpPool`, `MaxMpPool`, `MaxGalder` (carried) and `MaxBankGalder`. `0` means no limit and is the default. Files written by older builds contain explicit limits (65535 for damage, 4294967295 for the pools), so set those to `0` to remove them; the control panel's Caps group does the same.

The update adds database migration 075, which widens `characters.hp`, `characters.mp` and `characters.tm_exp` to 64-bit and replaces the upper bound on `characters.money` with a plain "not negative" check. No stored value changes. Back up first, as with every update.

---

## How to report

See [how-to-report-a-bug.md](how-to-report-a-bug.md), and the [forum template](forum-bug-report-template.md).
