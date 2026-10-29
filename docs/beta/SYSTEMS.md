# tPoseidon beta: system status

Last updated: 2026-10-25

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
- Selecting a character that is not one of the account's slots answers the client with the original refusal code instead of leaving it waiting on the connecting screen

### World and movement
- Zone changes between game servers - walk through a portal that leaves the current server
- Teleport NPCs and the quest return warp
- Arrival point of a portal warp, as the original picks it: the arrival point of the destination map that belongs to the map you came from, otherwise a random entry point of the destination map, otherwise (500, 400); a warp is never dropped, so leaving the Coral Beach boss room (and the 154 other portal pairs the client maps have no arrival row for) lands on the Dark Cave entry points - test: leave map 216 by its portal and check you stand in the Dark Cave
- Login position: a character saved at a position below 1, outside the map or on a blocked cell starts on a restart point of its map (otherwise an entry point, otherwise (500, 400)) as the original does (the original's two further tile checks are not built)
- Boss spawns from the BossMon tables (**reconstructed**, no notice broadcast yet): every minute each boss row counts up; once the counter has reached the row's check time, and at least 20 seconds after the last spawn, the missing bosses (count minus alive) appear on the region's points or the map's spawn points and the counter restarts - Master Mong (map 216, 6 bosses, check time 10) appears about 11 minutes after the game server started and again 10 minutes after the last one was killed (**INFERRED**: a killed boss counts as gone)
- Sector visibility (3x3) - crowded maps, check nothing pops in late
- Sitting and lying down shown to other players - sit while a friend watches
- Time-of-day map colours
- Portal region click (the client sends 0x04B1 for a portal on a Fiesta map): the server checks the map range, the Fiesta window and its banquet hall portal, the level band of the portal table and the two-entries-a-day limit, answers the original codes (accepted, closed, level, daily limit) and walks the character to the portal like the ordinary portal click (the dead are not walked, **inferred**)
- World map window (the map button or hotkey): opens for zones of the opened regions of the world map table, refuses with the invalid-zone or invalid-state message otherwise, and closes again when the window is closed (the zone region check of the original is not applied)

### Combat and skills
- Class skill sets: first-tier job skills, buffs, debuffs, heals, shields, party auras, scroll, thrown, gun, field, ground-target and chain-hit skills - try your class skills on a monster and tell us which misbehave
- Polar bear skills: hammer gauge, Crazy mode, buffs - play a polar bear
- Polar bear skills built from the 2014 client data, with the parts below inferred where neither the client nor the data settles them (marked INFERRED):
  - Double Crazy: while the Crazy mode runs, the move speed is raised by the skill's RunSpeed column (1.35 at level 1 up to 1.75 at the master level, as the skill tooltip says), sent to the client as a speed change at the start and end of the mode (INFERRED: the factor applies to walking and running alike)
  - The Crazy mode start costs no MP: the skill is a passive, its tooltip shows no MP cost and the mode starts by itself when the gauge is full (INFERRED; the MpCon column of its table is not charged)
  - Quake Hammer (Earthquake Hammer) after-effect: every monster the shock hits can be paralysed for PSeconds seconds with the chance PRatio (INFERRED: the skill row carries the Paralysis debuff class, the client has a timed effect for it, and the table columns are named P for paralysis; a paralysed monster stands still but can still fight back when you are in its reach)
  - Wearing a hammer needs the Hammer Equip passive (PSAbility 13, INFERRED by the same rule the original servers use for guns); the gun check (PSAbility 1, the Gun Carrier passive) is the original servers' equip check number 12 and now applies too
- Gun normal attack and the common slot (bullets, throw items) - equip a gun and shoot
- Knockbacks, status effects (stun, bind, fear and so on) and skill riders
- Monster skills, monsters fighting back, monster debuff resist
- Monsters keep their place when you leave a map and come back: a monster that has wandered is shown where it stands (and where it is heading if it is still walking), not at its spawn point, as in the original; the map's monsters are not reset or frozen by leaving. Hits and skills now name the monster's real position instead of the end of its walk, so your character no longer steps toward where a moving monster is going. Clicking a portal while auto-attacking now stops the attack at once, as walking does (try: leave a map with wandered monsters and return; attack a walking monster; click away or onto a portal during auto-attack and say if the attack animation still lingers)
- Skill level-up from the skill window, and learning a skill from a skill card
- Critical hit effect, sound and screen shake
- Player vs player: PK zones, skirmish and peace toggle, PK score and kill counts - try with two characters in a PK zone
- 1-on-1 duel: challenge another player on the same map from the player menu, they accept or refuse, the challenger starts it, 5 seconds of preparation, then up to 10 minutes of battle; the game ends when one player is defeated (the loser rises again with 30% HP and the duel ladder moves by the Elo-style table of the original servers, 15 points between equal ratings), when a player leaves the map or disconnects (the other wins, the leaver gets a disconnect on the record), or when the 10 minutes pass (the challenger loses); a result window shows the new record, the ladder points, the time and the outcome; no exp is lost in a duel and nothing is rewarded (the original servers pay nothing); try with two clients, including a refusal, a cancel before the start, a defeat, leaving mid-fight and waiting out the clock
- 1-on-1 duel, additions of ours (**inferred**): a duel can only be started after the other player accepted, a player already in a duel answers "busy" to new challenges, a duelist can only hurt the opponent and nobody else can hurt a duelist, and after the duel a player gets back the PvP mode they had before it (the original servers set mode 0)
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
- Player shop, shop signs, remote search and buy; the shop window counters (visits since the shop was started and visitors now)
- Inspect another player (click a player, open the profile): name, level, job, guild, kills and deaths, profile title and the four growth weights, and, when he shares his equipment, stats with their bonuses, resists and worn items; what is shared follows the options window checkboxes (public info, public equipment), shared by default (**inferred**: the default of the original comes from the fixed value of its enter-world batch). A player who left the map is answered with the original's 'unknown player' reply The inspect window also lists the boss kill history of the player when he shares the third option of the options window: the monster, the number of kills and the date of the first kill, up to nine bosses are drawn by the client. The same list is sent at login and again after each boss kill, for the boss history window of the player. The cover letter text a player writes in his profile is stored (an apostrophe is stored as a backtick, at most 200 bytes, a longer text is refused with the original's refusal), shown to others in the inspect window and sent at login; an empty letter brings the default letter back
- Target player health: selecting another player with a skill asks his current and maximum HP, the answer fills his gauge
- Memory port: save, delete, teleport
- Special use items, built from the original game server's item helper and the 2014 client data. Each is consumed only when its effect was stored; every refusal keeps the item:
- Remove all equipment button (equipment window): releases every listed worn item at once, or refuses the whole list when one item cannot be released; at most 30 items in one request (**inferred**)
- Ending the card identification game from the client's end question (**inferred** from the client text; the original server has no handler for it)
- Secret map (card identification game rewards): the sixteen secret cards (items 26500 to 26515) are won in the card identification game as special results. The My View secret tab of the client shows how many of each card the bag holds; the server takes one of each card and gives the secret map (item 64130, used like the other treasure maps). The request is refused without a reply unless it holds each of the sixteen cards once (**inferred**: the original server only checks that the listed items are in the bag, and the client reads exactly sixteen cards in the answer)
  - Hair dye (effect 15): the colour sent with the use request (0 to 20) is stored for the hair of the shown job tier, then the user is told and everyone on the map sees the new colour
  - Gender change (effect 21): the type turns into its pair (1 and 2, 3 and 4, 5 and 6, 7 and 8), the hair is cleared, worn items go to the bag, the new species' starter ear and tail are given; refused after a job change, for the polar bear, and when the bag cannot take the worn items plus the two new ones (INFERRED: the original tests the worn items and two free slots separately)
  - Skill reset (effect 23): removes one skill and returns its TM points (affection points plus the levels gained, plus the mastery cost less one at level 11); refused for skills of 5000 and above, for a skill another owned skill needs as a prerequisite, and for a skill not owned
  - Point Back 40 (effect 24): takes 1 to 40 stat points back into the bonus points, never below the level baseline of any stat; Point Back All (effect 26) takes everything above the baseline; Build Graph Reset (effect 25): four new growth weights from 1 to 4 adding up to 10 with 4 on the main group, every stat is set to its baseline and the rest becomes bonus points
  - Storage permit (effect 27): eight more warehouse slots, up to 800
  - Rename (effect 31): the new name must be 4 to 16 bytes and pass the original name rule, must not exist and must not contain a forbidden word (table `forbidden_name`, empty until the server owner fills it); the refusal codes 1019 (not allowed) and 1023 (taken) are the ones the 2014 client's name-change window has texts for; the old and new names are kept in `char_name_history`; the ranking, mail and GM lists, the party, the guild and the social lists take the new name
  - Megaphone (effect 32): needs a microphone in the bag, shouts the text (at most 161 bytes) to everyone connected through the system server and consumes the megaphone but not the microphone (INFERRED: the shout shows `name : text`, and no queue is kept, so the queue length answered is always 0)
  - Portable port (14, zones 1 to 7), emergency port (19, back to the return map), portable port AD (20, TeleportAdvInfo zones), broomstick (28, a weighted random zone from RandomPortParam with its notice) and friend finder (39, the friend's map and position); refused on maps that forbid teleporting, and for the friend finder on destinations that forbid arrivals and while busy
  - Documents, the microphone and other items with no server action are consumed with only the use answer
  - Gift boxes of the newer effect 47: the box is opened as a gift box, and when the roll holds the selected item of BonusPresentItemInfo the pick bonus box is rolled too, otherwise the unpick bonus box (INFERRED from the table names and the rows)
  - Key boxes (effect 49), through the key box window of the 2014 client: putting a box in the window checks that it is a key box in the bag (and, for boxes that ask for it, that you stand in a safe zone type 1, 2 or 5); confirming with the key takes one box and one key and gives the present of the rare table, the second rare present when a roll of 1 to 99 is at most the box's percent plus the key's KeyItemRateInfo bonus. Refusals use the original codes: not a key box, key needed, wrong key, too heavy, bag full, other
  - TM exp booster potions (effect 51): the exp and TM gains are multiplied by the Item_ExpParam advantage for the potion's Life in minutes (INFERRED: the unit; the rate packets 0x0376 and 0x0377 are not sent, and the boost ends at logout)
  - Imprints (effect 52, ImprintSkillInfo): the first twelve types add their value to the twelve stats in order for the table's hours, types 20 and 21 add that percent to exp and TM gains; an imprint of the same type replaces the old one and is kept across logout (INFERRED: the original servers predate it; the client's reaction to a drunk imprint is not known, so nothing is sent beyond the use answer, and types 12 to 19 and 22 to 23 are stored but not applied)
  - Pet piercings (effect 48) are applied through the character pet window and are refused (not consumed) when double-clicked; the seeds of effect 42 use the soul seed system
- Player shop visitors: other visitors see the stock fall after a sale, and the shop window of every visitor closes with the closed-by-keeper or sold-out message when the keeper closes or restarts selling, or the last row is sold; a game master closing a shop shows the keeper the game master message (not covered: the keeper disconnecting)
- Find-shop helper (the chat helper that lists open shops with the searched item name): lists item, keeper, shop title, price and place of the open shops on your map (**inferred**: the original scans the requester's shell), as many as fit in one packet
- Market price statistics: the price slash command answers the minimum, maximum and average of the prices clients reported after their purchases (kept in memory, lost at a restart)
- Level-up gifts: a chat line names the gift items when a level-up gift mail is sent (**inferred**: the item ids of the gifts)
- The timer item clock is sent before a My Camp owner's item list, so timer item notices count from the server time
- My Camp opens in the original order: the item list goes to the owner first (clock, count, item chunks, end), and only when the client confirms it has the list does the server send the camp entry packet; the client reads that list while it loads the camp scene, so the old order (entry packet first) crashed the client on the first camp of a session
- The My Shop camp tab sends the camp item list before its entry answer, as the original does; before, the answer came with no list and the client crashed when it read it
- A finished item list transfer is confirmed by the client with the `0x0285` packet (the original counts it as success); guests entering a camp and the recycling list were refused because it was read as an abort
- `tposeidon camp --character <name> [--give <camp item id> [--count <n>]]` lists or adds My Camp items on the character's account (new items; the player moves them into the camp from the camp window)

### NPC services
- Look change and disguise kits
- Skill shop, skill masters, job change and job-change quests
- NPC movement, job checks and script functions; the day and night talk of event NPCs (script function 87) follows the 60-minute game day of the client's DailyTime table (**reconstructed**: the original servers' clock start is not known, the day here starts at midnight UTC)
- NPC script functions of the guild NPCs: the guild-making check (function 53: level 35 and the GuildSetupInfo money, MainServer row) and the guild windows (functions 54-58 and 79-81: dissolve, the guild list, the recruit board, cancel an application, leave, change master, password and name) open as on the original servers
- Event talk and event scene NPCs (functions 223 and 254): the client plays the NPC's event and the NPC answers with the message of the script when it ends
- The soul seed check of NPCs (function 224) and the card battle rank notice (function 21)
- NPC placement: service NPCs stand on the map file's NPC points (type 4) and shops on its shop points (type 5), which are separate id lists, so a banker and a shop that share a point number no longer stand on the same spot; an NPC whose point is missing stands at (600, 600), as on the original servers
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
- Guild depot (the shared storage window of the 2014 client, **reconstructed**: the original servers have no code for it, so every rule comes from the client): the guild window shows the depot button once the guild owns at least one tab. A tab holds 80 entries, a guild owns up to 10 tabs (bought in the guild shop, see below), and there is no money in the depot. The master always may use it; other members need the "guild depot manage" right that the master gives in the member window. Deposit a stack or part of it from the bag into the open tab, or take it back; stackable items of the same kind and state join the stack already in the tab (up to 32000), other items take one entry each. Taking items is refused when the bag is full (300 entries) or the load would be too heavy. The history button lists the latest 20 moves (name, item, count, date). Two members moving items at the same moment are served one after the other: an item is never in two places and an entry is never taken twice. A guild that still has items in its depot cannot be dissolved. **Inferred** (the client gives no more): that history entry action 0 is a deposit and 1 a withdrawal, that new entries go to the end of the tab, that merging follows the personal warehouse, and that items with the "cannot be stored" option are refused as in the personal warehouse. The area check of the client (towns only) is done by the client alone. Live test not done yet
- Guild shop (the guild window's shop of the 2014 client, **reconstructed**: the original servers have no code for it, so the goods, prices and messages come from the client's own tables `GuildShopGoodsTable` and `GuildShopUIStrings`): a guild spends its guild coins, shown in the shop window and kept in the guild state. Only a member with the guild shop manage right (the master always; the member right bit 0x20, the sixth entry of the grade rights table) may buy; the others get the client's 'no purchase right' message. Goods that work: guild members +16 up to 128 for 1000 coins (starting from 64), the guild depot for 1500 coins (it opens the first tab; it can be bought once), and a depot tab for 1500 coins each (+80 slots, up to 10 tabs, only after the depot was bought). A purchase shows the client's purchase message to every online member, takes the coins off at once, and sends the new member limit or the new guild state (tab count) to every online member. The purchase history button lists the latest 10 purchases (date, buyer, goods, coins), newest first. Two members buying at the same moment are served one after the other and the coins are never spent twice; the client's 'another member is buying' message is shown to the one who comes second at the very same moment. The owner can set the depot tabs by hand with `tposeidon guild-depot --guild <name> --tabs <0-10>` and give or take coins with `tposeidon guild-coins --guild <name> --add <amount>`. **Inferred** (the client gives no more): that the member right bit for the shop is 0x20 (same position rule as the depot right), that the purchase message goes to every online member, that the history is newest first, the order of the refusals (goods, limit, then coins), and the first depot purchase giving one 80 slot tab. Live test not done yet
- Guild game: rooms, Monster Match, notices; the room list shows each side's ladder rank, member count and average level and the room's team size, title and the original caption; the start notice shows the ladder rank
- Guild game rooms in the 2014 window (**reconstructed**: the original servers have no code for this window): make a room (friendly, league or event match, team size, map, password), join it with the password, invite guild members from the lobby (they can accept or refuse the invitation), remove participants, leave the room; the host starts the match when both guilds have at least 3 participants; only league matches change the ladder score
- Guild league window: the ranking of the top 20 guilds by ladder score (matches, wins, losses, points); the playoff bracket says there is no league match
- Guild game results are saved: battles, wins, losses, draws and disconnects, the guild ladder score (both guilds start at 1000; the winner gains and the loser loses points by the ladder formula of the original servers; after six matches between the same two guilds in one day the score no longer changes) and guild points for the winner; the result window shows each guild's record after the match, the ladder points won or lost (game score) and the new ladder score (league points)
- Guild game, Monster Match (**partly inferred**): pick Monster Match as the match type when making a room (it uses the arena Flame Arena M, and the Defeat number of the make window is the score goal, 0 means no goal); two seconds after the battle starts the first monster appears for both teams, and later a fighter who stands about 2.5 seconds in one of the five trigger zones at the north edge of the arena calls the next one while none is alive; each call puts one monster for each team at that team's spawn point, they live 300 seconds, a kill of the monster of your own team adds its points (Tutankhamen G 1, Tombbeth G 3, Count Blad G 7, Predetor G 14, Queen Odinea G 20) to you and your team, a kill of the other team's monster counts as a wrong kill and gives nothing; the match ends when a team reaches the score goal, when a side has no fighters left, or at the time limit (higher score, then more player kills, then fewer deaths, else a draw); the score board is sent every second with the scores, the boss tally of each team and the live monsters with their time left, and the kill notice shows monster kills; defeated players rise with 30% HP at the team's spawn region like in Team Match
- Guild game, Team Match respawn point: a defeated player rises in a random spot of one of the arena's team spawn regions (the first team uses the odd numbered regions, the second team the even ones), not where they fell
- Watching a guild game as a spectator (**partly inferred**): the waiting room has the Watch button and the password window; the server checks the room (open list, watchers allowed, password) and moves the watcher from the guild game lobby to the arena where they see the match frames (start, kills, score board, summons, result); the watcher leaves with the usual leave buttons and is moved back when the match closes. Not built from the original: the Galder fee of the newer client, and the "watching" chat tag of the original (the 2014 client shows it for received observer chat, no server order for it was found)
- Guild game, during Team Match and Team Deathmatch (**partly inferred**): the score board opens when the battle starts, every defeat shows the kill notice to both teams and updates the kill and defeat counts, in Team Match a defeated player rises with 30% HP at their team's spawn region at the next 30-second mark of the remaining time (3 to 5 seconds later when the mark is less than 3 seconds away, as the respawn message of the client shows), and in Team Deathmatch a defeated player stays down; try with two guilds of three players and check the counts, the respawn countdown message and the end of the match
- Angel system: call, accept, warp, graduation at level 31
- Couple and wedding: marriage options (ring, dress and title) are checked in the original order and a refusal closes the exchange window with the original code (not enough money and 'already chosen' show their messages); a request without a dress is accepted and takes no fee, as in the original; couple making, closeness points, divorce, wedding hall, wedding board, the lover's position and moving to the lover (married couples, also across game servers; refused while the lover is on a map the original servers close to it)
- Report a player to the GMs
- Friend list replies: the request dialog signals relayed to the requester, the request-shown notice, the request-in-flight refusal, the add-pair mismatch, deleting an offline friend, a friend's job and name changes shown to his online friends; the position request answers the zone, answers both zones while the friend changes maps (**inferred**: the pair is the zone left and the zone entered), and when you are on the same map as the friend it also makes the friend's game server send you his map cell twice, now and three seconds later; move-to-friend answers 'moving' while the friend changes maps, 'same zone' on the same map, otherwise asks the friend's game server for his exact spot and answers with it, or with 'not reachable' for zone 0 or above 999 (**inferred**: a pending lookup is dropped after 30 seconds)
- A friend request to a game master is refused as not available, and reporting a game master is refused with the original reply
- Guild-to-guild SMS (guild window): a message to another guild goes to its master at once, or into the master's message box when he is offline; sending to your own guild is refused with the original reply
- Angel graduation tells each of the newbie's angels with a memo (online at once; offline it is stored as a game master memo, **inferred**), and a game master memo to an online character is shown at once
- Head message, card battle title and fortune sign share one sign slot as on the original servers: setting one removes the others, the head message and the card battle title disappear when the character moves, and the card battle title can be ended; there is no region refusal and newcomers do not see an existing sign (**inferred**)
- Chat bar of a dead character, which the client sends on its own channel: shown to the map as the original line (**inferred**: the client switches channel on the flag the death routine sets)
- Party recruit board (party window): a party member posts a recruit note for the party (at most 34 bytes), a player without a party posts a seeking note, and both boards list eight posts a page with name search, a level search (three levels up or down) and sorting. A post shows the master's name, class, the party type and average level, and the detail window shows the master's level and map, the member count, the exp division, the item share and the note; the posting master (or the seeking player) also gets the note as a sign above the head. Rules as on the original servers: a party that is full cannot post, one post per party and per player, the board holds 1000 party posts and 1500 seeking posts, only the master removes the party post, a post disappears when the party breaks or fills up or its last online member leaves, a seeking post disappears when the player joins a party or logs out, and the average level, member count, type, division and master follow the party. **Inferred** where the original code could not be read to the end: whether the order of a sorted list is highest first (the switch on) or lowest first, the order of a level-filtered list, the "type 7" sort of the seeking board being the default list, the sign moving to the new master when the master changes, the zone shown in the seeking player's detail window, and the capacity checks (the original tests the party count against the seeking board's counter); the sign above the head is not shown to players who arrive later. Not live-tested - post from two clients, open the list from a third, and check the pages, the searches and the sign
- Couple making: the proposer is told when the target is busy or already has a proposal, and the making window leaves its waiting state with the original reason code when a side refuses, quits, is of the same account, is too low or gone (the success code is not sent: the original sends it on the SystemServer reply, which our link does not carry); the couple skill learned shows its message; the wedding control window closes when the ceremony ends (not covered: a member leaving the hall during the ceremony)
- Divorce question: the partner who is asked sees the divorce confirmation box (it was sent with an opcode the client ignores before), also again when he logs in while the request is open
- Angel graduation: the graduating newbie gets the graduation message as well as his angels
- Microphone: a Microphone item in the bag lets a player say a line to the whole zone (chat line) and shows it as a bubble over his head for others until he acts (moves, fights, picks up, warps, rests); the 10 second delay is the client's
- Card identify: other players see the identify marker over a player who is identifying

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
- Fortune house: playing again or entering without enough money for the next fee shows the fortune money warning
- Soul seed: the stat buff ends after its 10 minutes and the client drops it

### Pets and guardians
- Pet spawn, follow, emote and recovery
- Guardian rename
- Guardian card-skill order list
- Guardian card save: the cards set in the guardian's skill slots are kept
- Guardian in the world: the equipped guardian appears next to its owner at login, on warps and when another guardian is equipped, follows the owner and leaves with the owner (**INFERRED**: the follow period and distance are the pet values; the soul-shard cap in its spawn data is sent as 0 because the original code that sets it was not found)
- Guardian experience (only while the Summon Guardian skill is running, as on the original servers): each hit the owner lands gives the guardian a tenth of the exp the owner gets for that hit (the ratio comes from the client's SoulGuardianExp table, truncated); the owner sees the gain, and a level-up shows the level, exp and skill points with the "level up" effect around the guardian, as on the original servers; the level stops at 200; the guardian's exp and level are saved (a level-up saves exp as 0, as the original database procedure does) (**INFERRED**: the owner's hit exp is used before game-master rate and potion scaling)
- Guardian login check: at login the skill points plus the spent card levels must equal the guardian level, otherwise the points are given back and the cards go to level 1; exp at or above the next-level value is cleared
- Guardian lock-on: choosing a monster with the guardian marks it and the client shows the mark list; the mark is dropped when the monster dies (**INFERRED**: one mark at a time, because the mode that allows several was not found)
- Guardian casting (while the Summon Guardian skill is running): every half second the guardian casts one of its card skills at the monster you locked on to, otherwise at the monster you are fighting; with an order list it goes through the list one skill per round, otherwise through the card groups starting with its own type; below 20% of your life it first tries its healing card (Cure) on you; each cast has a cooldown of twice the skill's cool time, the guardian's own stats set the damage and it never misses with physical skills, the monster counts as yours (kill credit, exp, drops, guardian exp), and the guardian says the skill name above its head and plays emote 6. Only cards marked usable by a guardian are cast (card option 2, as in the original server): that is every single hit, area hit, object hit, ground skill, chain, polar bear and tornado hit, the mana ring, the debuffs on the target and Cure, all cast through the same routines the players use. No usable card is a buff, a throw, a gun skill, a galder skill, Basic Healing or Revival, so those are never cast by a guardian. (**INFERRED**: the guardian is busy for the skill's delay after a cast; ground skills aim at the monster's place; the two debuff cards that also count as friendly, Petrify and Shield Break, are not cast on you in the healing pass)
- Pet collection (**reconstructed**): a 3-star or better pet in the bag eats its food and collects an item from the current map every few seconds while you are online; deeper drilling maps lower the chance; mine maps need a pet and food with the special dig mark (no item in the KR data has it, so mines refuse with the original message); take the items to your bag or throw them away (the pet's items are kept after logout and sent again at login); the filter check boxes of the window are not sent by the client, so they change nothing
- Pet speech reacts to the owner's death and stays silent in zone type 14 (**inferred**)

### GM and hosting
- GM login, logout, teleport and kick from chat and from the command line tool
- GM kick with a reason: the player sees the reason and is disconnected when his client confirms it 10 seconds later, and his account is marked sanctioned, as on the original servers; GM account sanction on or off by character name (command line tool)
- GM chat commands (typed with `@`) for the rest of the client's GM keywords: notice to the chat lobby, block a character for a number of days (with or without a reason), chat / move / trade / My Camp / shop / chat room blocks in minutes (0 lifts the block, -1 blocks for 15 days), the chat, move and trade block lists, summon monster, `@item <itemId> [count] [name]` (puts the item in your own bag, or in the bag of an online character, and tells you why when it did not arrive), and the command help list
- GM character count of every zone, close a My Camp by character id, and summon an NPC at the GM's position (command line tool)
- GM close of a player's instant chat room, from chat (`/rmvmychat name`) and the command line tool (`remove-chat`); a room member gets the original "master only" refusal instead
- GM summon monster, set music, remove monsters or one monster, close a personal shop or My Camp (command line tool; shop and camp also from chat). Summon monster takes the original order's map field: with a map id the monsters appear at a random arrival point of that map and the GM does not move there (the original's point list is INFERRED to be the map's restart and entry points)
- GM spawning with a spread (command line tool and the panel's Spawns tab): `spawn-monsters` puts a count of monsters over a circle of a given radius, centred on the GM, on a random arrival point of a map, or on chosen coordinates of any map, without moving the GM; `remove-my-monsters` removes only the monsters this GM character summoned (the server keeps the list per GM until the game server restarts). These two are tool-only orders added to the server because the original has no order for them; a game client cannot send them
- GM character count of a zone, character info, and a notice to the chat lobby (command line tool); summon, music and remove replies are shown by the tool
- GM hide or show an NPC on a map, list the hidden NPCs, and show them again by zone (command line tool; hidden NPCs stay hidden for players entering the zone until shown again or the server restarts)
- GM issue item: an NPC on the GM's map hands out an item, one per player, until the set count runs out (command line tool)
- GM OX give item: on an OX quiz map, hand items from the GM's bag to every other character on the map (command line tool)
- GM block and unblock a character by type (chat, trade, move, My Camp, personal shop, chat room, normal; every type but normal also works when the character is offline), block with a cause (the player sees the cause and is disconnected when he confirms it), and list the blocks (command line tool); blocks are saved in the database through the MasterServer, as on the original servers (directly when the MasterServer is not connected); the SystemServer loads the active blocks from the database when it starts, so the lists match what is enforced after a restart
- GM guild tools: guild info, memo to members up to a grade, block a guild's online members, dissolve a guild (command line tool)
- GM guild game switch: close or open the making and joining of guild game rooms (closed lists refuse with the original error 12), and read or set a room's GM flag (command line tool); the lists start open, as on the original servers
- GM EXP events: plan a time window in which every player gets extra EXP and TM EXP from monsters, drilling and skills (quest, party quest and card game rewards are not raised, as on the original servers), list and delete the planned and running windows, or set the extra percentages at once (command line tool); the windows are checked every 5 seconds and are kept in the database, so they survive a SystemServer restart
- GM ability events: plan a time window in which one stat of every player (AP, AC, DX, MP, MA, MD, WT, DA, LK, HP, DP, HV) is raised by a percentage of the value from his stat points, list and delete the windows (command line tool); a window deleted while it runs stays applied, as on the original servers, until another window of the same stat ends; the windows are checked every 5 seconds, kept in the database (they survive a SystemServer restart) and every login receives the current percentages
- The KR 2014 client has no GM chat of its own (it keeps every line that starts with `/` for itself), so the GM chat commands are read from normal chat lines that start with `@`: `@login <password>`, `@logout`, `@kick`, `@speed`, `@immortal`, `@invisible`, the blocks, notices, memos, teleports, summon, `@item` and the rest of the list in the GM window's Help tab. A line that starts with a GM keyword is never shown to other players. `@login` works only for the registered GM character with its account password from a permitted address, every other command needs that login and re-checks the registration, and anyone else gets one private refusal. Every attempt and command is written to the server log. The built-in keywords win over NPC-script chat commands, and a script cannot define a command with such a name (`tposeidon scripts check` reports it)
- GM flag from the tools: `tposeidon gm game-flag on|off` and the GM accounts tab of the panel log the GM character in or out as GM in the game (GM eagle over the character, `@` commands enabled) without typing anything
- Registering a GM character by name
- The GM tool and the `@` commands find an online character by name through the game servers (a character counts as online when a game server holds it), not through the character's own chat-server connection; give item, summon and the other orders that need a game server answer with a refusal when the character is not on one instead of staying silent, and give item reports the game server's result (item given, no such item, no room in the bag)
- Galder coupons (item type 1) add the item's Money column to the character's galder, refuse when the total would pass 2,100,000,000 and are used up like a potion
- A game master window in the control panel (the GM button) with every GM tool command: players (search, look up, kick, timed and normal blocks, account sanction, account penalties, move, give items, memos, closing camps, shops and chat rooms), spawning monsters, NPCs and items picked by name from the game data, notices, OX quiz, block lists, map tools, EXP and ability events, guild tools, penalty kinds, and registering, listing and removing GM characters (also `tposeidon gm unregister` and `tposeidon gm list`); it says why it can't connect, asks before anything that can't be undone, and lists the in-game GM chat commands
- Parties and friends across game servers


---

## 3. Not built yet / partial

### Combat and skills
- 1-on-1 duel: the loser puts the pet away, rises with 30% HP and re-enters the zone as in the original (**inferred**: the arrival spot is the restart pick, the original sends position 0,0); the pet is not put away at the start because the original does not. Closed with evidence: the duel packet 0x02C5 is sent only after a reward answer from the Master and the reward gate of the original always refuses, so it never appears; the original loads the duel record into the player when entering a duel-arena zone, we read the same stored record when a challenge is made, so the shown record is the same
- Guild game, during the match: defeated Team Deathmatch players are not switched to spectator view; Monster Match leaves out the number of appearances setting of the make window (its use is not known), the monsters are not coloured red, the reward for the winner of a Monster Match is the same as for the other match types, and the order of the two numbers in the player kill notice is inferred (the respawn alert may show for the killer instead of the victim)

### NPC services
- The Leviathan quest NPC skill (function 258) only shows its effect: the original servers have no code for it, so the rule that lets the buffed player hurt the quest monster is not known (monsters are hurtable by anyone here)

### Items and economy
- Not included: purchase events, coupons and the Drill Egg gacha; the web shop catalogue starts empty and is up to each server owner
- Guild shop: the two emblem goods (guild emblem 500 coins, emblem package 1000 coins) are not sold: the emblem maker window and its messages are not built, so buying them is refused with the client's 'goods not found' message and takes no coins. Guild coins have no automatic income: the client only says that coins come from winning guild matches, and no table or server code gives the amount, so the owner adds coins with `tposeidon guild-coins --guild <name> --add <amount>`. The guild level and the member count of the guild window are still sent as zero. The bank of the Safety Bank NPC is the client's "Neo warehouse" and is built (see section 1)

### Social
- Inspect another player: stat totals, bonuses and resists are the values of our own stat model; the order of the boss kill list is by monster number (**inferred**: the order of the original list is not read), and the boss history of a player is read from the Master with a request of our own (the original loads it with the character; that route is not read)
- Key box window: the original takes the grant through the Master, holds the player in a key box state (item operation kind 0x37) while it runs and refuses with 'other' while another item operation of the same player runs. This is not built because the grant is applied inside the one request that confirms it, so no second item operation of the player can run in between; what the key box state blocks while the window is open is not read.
- Friend list: the original keeps a fast-repeat guard byte per friend object, set when a position request or a move-to-friend request is sent to the game server and cleared when the move-to-friend report arrives; a request that finds it set is dropped with a log line and no reply. It is not built: no clearing is read for the position request (its report path is not read), so following the code literally would lock position requests for the rest of the session
- Friend replies that the original server never sends (two of the client's friend opcodes) and the report replies 0x2719 and 0x271F are closed with evidence: the original sends 0x2719 only when its queued report command fails its own consistency check (the reporter object changed, or the database step count differs) and 0x271F when that command is cancelled, and our report lookup is immediate so neither state exists; the other report replies that its code never sends are not built. The triage of the server notifications of the original is finished: of the 139 packets the original sends for features we built, 31 are built, 76 closed with evidence (the original never sends them, the 2014 client never asks for them, or the feature they belong to is absent here) and 32 are not determinable: party follow (0x0215 to 0x0217, 0x02FA: the follow movement engine and the relation test are not read), card battle title refusal 0x01D5 (the zone rectangle lists), the second item registration 0x0104/0x0324/0x02F3 (the Master exchanges and the expiry source), guild rank bonus exp 0x037C, the elemental boss resist 0x0346, the gun line of fire 0x020D (no per-cell heights), Shadow World and Harcon instance events, the secret map exchange 0x0440 (checked by the Master), the marriage option success 0x0480 (item records come from the Master), the profile text 0x03EB, the party quest record 0x04B0, the portal click walk 0x04B2, couple memo texts 0x2EB3, the GM OX quiz 0x0279, and a few boss and doppelganger effects whose client reader or data is missing; each reason is in the project analysis notes. Saving a guardian's skill cards does not check the card option bit 1 because the original server does not either (its save handler only checks that the card item is in the bag and the card row exists)
- Cancelling a delayed item use: no usable item of the 2014 item table has a use delay, so the state cannot be entered

### GM and hosting
- Watching a guild game room: the original waits for a match that has not started yet, here the request is ignored until the match exists; the arrival spot of a watcher is the centre of the arena (inferred), and watchers are not hidden from the fighters
- The GM chat keywords move notice, chat colour and monster search: the original servers have no command for them (they were actions of the original GM client, which is not available), so there is no `@` command for them and such a line stays normal chat
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
