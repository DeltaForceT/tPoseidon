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

### World and movement
- Zone changes between game servers - walk through a portal that leaves the current server
- Teleport NPCs and the quest return warp
- Sector visibility (3x3) - crowded maps, check nothing pops in late
- Sitting and lying down shown to other players - sit while a friend watches
- Time-of-day map colours
- Portal region click (the client sends 0x04B1 for a portal on a Fiesta map): the server checks the map range, the Fiesta window and its banquet hall portal, the level band of the portal table and the two-entries-a-day limit, answers the original codes (accepted, closed, level, daily limit) and walks the character to the portal like the ordinary portal click (the dead are not walked, **inferred**)

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
- Player shop, shop signs, remote search and buy
- Memory port: save, delete, teleport
- Special use items, built from the original game server's item helper and the 2014 client data. Each is consumed only when its effect was stored; every refusal keeps the item:
- Remove all equipment button (equipment window): releases every listed worn item at once, or refuses the whole list when one item cannot be released; at most 30 items in one request (**inferred**)
- Ending the card identification game from the client's end question (**inferred** from the client text; the original server has no handler for it)
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
  - Key boxes (effect 49): one key of KeyboxItemInfo is taken from the bag with the box and the rare item B is given with its percent chance, otherwise the rare item A; a box that needs no key gives its normal item (INFERRED: the table columns are named this way and nothing else describes the rules)
  - TM exp booster potions (effect 51): the exp and TM gains are multiplied by the Item_ExpParam advantage for the potion's Life in minutes (INFERRED: the unit; the rate packets 0x0376 and 0x0377 are not sent, and the boost ends at logout)
  - Imprints (effect 52, ImprintSkillInfo): the first twelve types add their value to the twelve stats in order for the table's hours, types 20 and 21 add that percent to exp and TM gains; an imprint of the same type replaces the old one and is kept across logout (INFERRED: the original servers predate it; the client's reaction to a drunk imprint is not known, so nothing is sent beyond the use answer, and types 12 to 19 and 22 to 23 are stored but not applied)
  - Pet piercings (effect 48) are applied through the character pet window and are refused (not consumed) when double-clicked; the seeds of effect 42 use the soul seed system

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
- Guild game, during Team Match and Team Deathmatch (**partly inferred**): the score board opens when the battle starts, every defeat shows the kill notice to both teams and updates the kill and defeat counts, in Team Match a defeated player rises with 30% HP at the next 30-second mark of the remaining time (3 to 5 seconds later when the mark is less than 3 seconds away, as the respawn message of the client shows), and in Team Deathmatch a defeated player stays down; try with two guilds of three players and check the counts, the respawn countdown message and the end of the match
- Angel system: call, accept, warp, graduation at level 31
- Couple and wedding: couple making, closeness points, divorce, wedding hall, wedding board, the lover's position and moving to the lover (married couples, also across game servers; refused while the lover is on a map the original servers close to it)
- Report a player to the GMs
- Friend list replies: the request dialog signals relayed to the requester, the request-shown notice, the request-in-flight refusal, the add-pair mismatch, deleting an offline friend, a friend's job and name changes shown to his online friends; the position request answers the zone, answers both zones while the friend changes maps (**inferred**: the pair is the zone left and the zone entered), and when you are on the same map as the friend it also makes the friend's game server send you his map cell twice, now and three seconds later; move-to-friend answers 'moving' while the friend changes maps, 'same zone' on the same map, otherwise asks the friend's game server for his exact spot and answers with it, or with 'not reachable' for zone 0 or above 999 (**inferred**: a pending lookup is dropped after 30 seconds)
- A friend request to a game master is refused as not available, and reporting a game master is refused with the original reply
- Guild-to-guild SMS (guild window): a message to another guild goes to its master at once, or into the master's message box when he is offline; sending to your own guild is refused with the original reply
- Angel graduation tells each of the newbie's angels with a memo (online at once; offline it is stored as a game master memo, **inferred**), and a game master memo to an online character is shown at once
- Head message, card battle title and fortune sign share one sign slot as on the original servers: setting one removes the others, the head message and the card battle title disappear when the character moves, and the card battle title can be ended; there is no region refusal and newcomers do not see an existing sign (**inferred**)
- Chat bar of a dead character, which the client sends on its own channel: shown to the map as the original line (**inferred**: the client switches channel on the flag the death routine sets)
- Party recruit board (party window): a party member posts a recruit note for the party (at most 34 bytes), a player without a party posts a seeking note, and both boards list eight posts a page with name search, a level search (three levels up or down) and sorting. A post shows the master's name, class, the party type and average level, and the detail window shows the master's level and map, the member count, the exp division, the item share and the note; the posting master (or the seeking player) also gets the note as a sign above the head. Rules as on the original servers: a party that is full cannot post, one post per party and per player, the board holds 1000 party posts and 1500 seeking posts, only the master removes the party post, a post disappears when the party breaks or fills up or its last online member leaves, a seeking post disappears when the player joins a party or logs out, and the average level, member count, type, division and master follow the party. **Inferred** where the original code could not be read to the end: whether the order of a sorted list is highest first (the switch on) or lowest first, the order of a level-filtered list, the "type 7" sort of the seeking board being the default list, the sign moving to the new master when the master changes, the zone shown in the seeking player's detail window, and the capacity checks (the original tests the party count against the seeking board's counter); the sign above the head is not shown to players who arrive later. Not live-tested - post from two clients, open the list from a third, and check the pages, the searches and the sign

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
- GM chat commands (typed with `@`) for the rest of the client's GM keywords: notice to the chat lobby, block a character for a number of days (with or without a reason), chat / move / trade / My Camp / shop / chat room blocks in minutes (0 lifts the block, -1 blocks for 15 days), the chat, move and trade block lists, summon monster, and the command help list
- GM character count of every zone, close a My Camp by character id, and summon an NPC at the GM's position (command line tool)
- GM close of a player's instant chat room, from chat (`/rmvmychat name`) and the command line tool (`remove-chat`); a room member gets the original "master only" refusal instead
- GM summon monster, set music, remove monsters or one monster, close a personal shop or My Camp (command line tool; shop and camp also from chat). Summon monster takes the original order's map field: with a map id the monsters appear at a random arrival point of that map and the GM does not move there (the original's point list is INFERRED to be the map's restart and entry points)
- GM spawning with a spread (command line tool and the panel's Spawn tab): `spawn-monsters` puts a count of monsters over a circle of a given radius, centred on the GM, on a random arrival point of a map, or on chosen coordinates of any map, without moving the GM; `remove-my-monsters` removes only the monsters this GM character summoned (the server keeps the list per GM until the game server restarts). These two are tool-only orders added to the server because the original has no order for them; a game client cannot send them
- GM character count of a zone, character info, and a notice to the chat lobby (command line tool); summon, music and remove replies are shown by the tool
- GM hide or show an NPC on a map, list the hidden NPCs, and show them again by zone (command line tool; hidden NPCs stay hidden for players entering the zone until shown again or the server restarts)
- GM issue item: an NPC on the GM's map hands out an item, one per player, until the set count runs out (command line tool)
- GM OX give item: on an OX quiz map, hand items from the GM's bag to every other character on the map (command line tool)
- GM block and unblock a character by type (chat, trade, move, My Camp, personal shop, chat room, normal; every type but normal also works when the character is offline), block with a cause (the player sees the cause and is disconnected when he confirms it), and list the blocks (command line tool); blocks are saved in the database through the MasterServer, as on the original servers (directly when the MasterServer is not connected); the SystemServer loads the active blocks from the database when it starts, so the lists match what is enforced after a restart
- GM guild tools: guild info, memo to members up to a grade, block a guild's online members, dissolve a guild (command line tool)
- GM guild game switch: close or open the making and joining of guild game rooms (closed lists refuse with the original error 12), and read or set a room's GM flag (command line tool); the lists start open, as on the original servers
- GM EXP events: plan a time window in which every player gets extra EXP and TM EXP from monsters, drilling and skills (quest, party quest and card game rewards are not raised, as on the original servers), list and delete the planned and running windows, or set the extra percentages at once (command line tool); the windows are checked every 5 seconds and are kept in the database, so they survive a SystemServer restart
- GM ability events: plan a time window in which one stat of every player (AP, AC, DX, MP, MA, MD, WT, DA, LK, HP, DP, HV) is raised by a percentage of the value from his stat points, list and delete the windows (command line tool); a window deleted while it runs stays applied, as on the original servers, until another window of the same stat ends; the windows are checked every 5 seconds, kept in the database (they survive a SystemServer restart) and every login receives the current percentages
- The KR 2014 client has no GM chat of its own (it keeps every line that starts with `/` for itself), so the GM chat commands are read from normal chat lines that start with `@`: `@login <password>`, `@logout`, `@kick`, `@speed`, `@immortal`, `@invisible`, the blocks, notices, memos, teleports, summon and the rest of the list in the GM window's Help tab. A line that starts with a GM keyword is never shown to other players. `@login` works only for the registered GM character with its account password from a permitted address, every other command needs that login and re-checks the registration, and anyone else gets one private refusal. Every attempt and command is written to the server log. The built-in keywords win over NPC-script chat commands, and a script cannot define a command with such a name (`tposeidon scripts check` reports it)
- GM flag from the tools: `tposeidon gm game-flag on|off` and the GM accounts tab of the panel log the GM character in or out as GM in the game (GM eagle over the character, `@` commands enabled) without typing anything
- Registering a GM character by name
- A game master window in the control panel (the GM button) with every GM tool command: players (search, look up, kick, timed and normal blocks, account sanction, account penalties, move, give items, memos, closing camps, shops and chat rooms), spawning monsters, NPCs and items picked by name from the game data, notices, OX quiz, block lists, map tools, EXP and ability events, guild tools, penalty kinds, and registering, listing and removing GM characters (also `tposeidon gm unregister` and `tposeidon gm list`); it says why it can't connect, asks before anything that can't be undone, and lists the in-game GM chat commands
- Parties and friends across game servers


---

## 3. Not built yet / partial

### Combat and skills
- 1-on-1 duel: the loser puts the pet away, rises with 30% HP and re-enters the zone as in the original (**inferred**: the arrival spot is the restart pick, the original sends position 0,0); the pet is not put away at the start because the original does not. Closed with evidence: the duel packet 0x02C5 is sent only after a reward answer from the Master and the reward gate of the original always refuses, so it never appears; the original loads the duel record into the player when entering a duel-arena zone, we read the same stored record when a challenge is made, so the shown record is the same
- Guild game, during the match: Monster Match has no live scoring yet (no summons, no kill notice, no score board); in Team Match and Team Deathmatch the revived player stays where they fell instead of going back to their team's start point, and defeated Team Deathmatch players are not switched to spectator view

### NPC services
- The Leviathan quest NPC skill (function 258) only shows its effect: the original servers have no code for it, so the rule that lets the buffed player hurt the quest monster is not known (monsters are hurtable by anyone here)

### Items and economy
- Not included: purchase events, coupons and the Drill Egg gacha; the web shop catalogue starts empty and is up to each server owner
- Guild depot (the guild warehouse window of the 2014 client, opened from the guild window): not built, deferred until after the beta. The bank of the Safety Bank NPC is the client's "Neo warehouse" and is built (see section 1)

### Social
- Friend list: the original also keeps the position request guard that refuses a second request while one is running; it is not built because its meaning is not read
- Friend replies that the original server never sends (two of the client's friend opcodes) and the report replies 0x2719 and 0x271F are closed with evidence: the original sends 0x2719 only when its queued report command fails its own consistency check (the reporter object changed, or the database step count differs) and 0x271F when that command is cancelled, and our report lookup is immediate so neither state exists; the other report replies that its code never sends are not built. About 105 server notifications of the original (effects, state refreshes, replies to requests we do not handle such as inspecting another player's equipment) are located but not built because their fields or client reaction are not read; the list with evidence is kept in the project analysis notes. Saving a guardian's skill cards does not check the card option bit 1 because the original server does not either (its save handler only checks that the card item is in the bag and the card row exists)
- Cancelling a delayed item use: no usable item of the 2014 item table has a use delay, so the state cannot be entered

### GM and hosting
- Watching a guild game room: the 2014 client sends the watch request, but it has no reader for the original answers (accepted / refused) nor for the observer notice the original game server sends to the map, so a watcher cannot be shown; not built
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
