# NPC scripting

We built a small script language so hosts can add NPCs, quests and events without touching C#. Scripts are plain text files with the extension `.tscript`. The game server compiles them at start-up, and you can reload them while the server runs.

Scripting is opt-in. With no scripts, every NPC behaves exactly as before. A script only takes over when it names an NPC that it defines or overrides.

## Quick start

1. Put `.tscript` files in the scripts folder: `<server data>/scripts/npc/` (any sub-folders are read too). Run `tposeidon scripts folder` to print the exact path for your install. To use another folder, set the environment variable `MT_NPC_SCRIPTS_DIR` for the game server.
2. Check them: `tposeidon scripts check` lists every error as `file:line:column: message`.
3. Reload a running server with either of these:
   - `tposeidon scripts reload` (checks, then asks the server to reload within a second), or
   - the chat command `@script reload`, typed by a game master in game. `@script` alone prints what is loaded.

A file with an error is skipped and reported in the server log and in the check output. The other files still load.

Four complete examples are in [`examples/`](examples/): a greeter NPC with a menu, a daily login reward, a fetch quest and a game-master event NPC.

## A first script

```
npc "Welcome Guide" sprite 1301 map 1 at 120, 340 face south {
    mes "Hello, " + name() + "!";
    menu {
        "Tell me more" {
            mes "Come back any time.";
            close;
        }
        "Goodbye" {
            end;
        }
    }
}
```

This places a new NPC on map 1 at x=120, y=340, using the NPC model with type id 1301. Talking to it shows the text and the menu.

## Language reference

### Comments, values, names

- `// line comment`, `# line comment` and `/* block comment */`.
- Values are whole numbers (64 bit) or text. Text is written in double quotes with the escapes `\n`, `\t`, `\"` and `\\`. Hexadecimal numbers such as `0x10` work too.
- `+` joins text and numbers: `"You have " + money() + " gelder"`.
- Zero and empty text are false, everything else is true. Comparisons give `1` or `0`.
- Names are letters, digits and `_`. The words `let if else while break continue return mes next close end fallback menu npc override on every at cron command function char account server` are reserved.

### Variables

| Kind | Written as | Lives |
|------|------------|-------|
| Local | `let x = 1;` then `x = x + 1;` | until the script step ends |
| Character | `char.visits` | saved per character |
| Account | `account.bonus` | saved per account, shared by its characters |
| Server | `server.event_running` | saved for the whole server |

Reading a saved variable that was never set gives `0`. Saved variables are stored in PostgreSQL in the table `script_variable` (created by migration 067), and every write goes to the database at once, so they survive restarts. A name is at most 64 characters, and one owner can hold at most 512 variables.

Server variables are cached by each game server process. If you run more than one game server, treat a server variable as eventually consistent and restart or reload after changing it by hand in the database.

Compound assignment works for all variables: `+=`, `-=`, `*=`, `/=`, `%=`.

### Operators

From weakest to strongest: `||`, `&&`, `== !=`, `< <= > >=`, `+ -`, `* / %`, then unary `!` and `-`. `&&` and `||` stop early. Arithmetic on text is an error, and so is dividing by zero.

### Statements

```
let x = expression;
x = expression;           // also += -= *= /= %=
if (condition) { ... } else if (condition) { ... } else { ... }
while (condition) { ... } // with break; and continue;
return;                   // or return value; inside a function
name(arguments);          // call a built in or your own function
```

### Dialog statements

These only work in an NPC dialog.

| Statement | What it does |
|-----------|--------------|
| `mes "text";` | Adds a line to the current page. A page holds at most 40 lines. |
| `next;` | Shows the page with a Next button and waits for the click. |
| `close;` | Shows the page with a Close button. The script ends when the player closes it. |
| `end;` | Ends the script. If text is waiting it is shown with a Close button, otherwise the dialog closes at once. |
| `menu { ... }` | Shows the page with a list of options and runs the one that is chosen. |
| `fallback;` | Hands the NPC back to its original dialog. Useful in an `override`. |

A menu looks like this. An option can have a condition and is hidden when it is false. At most 20 options can be shown at once.

```
menu {
    "Always shown" { ... }
    "Only for level 50 and up" if (level() >= 50) { ... }
}
```

`menu("A", "B", "C")` also works as an expression. It returns the number of the chosen option, starting at 1.

Text in dialogs is plain ASCII. Characters outside it are shown as `?`, and a backslash is removed because the client uses it as a separator.

### Declarations

**`npc`** adds a new NPC. All of `sprite`, `map` and `at` are required; `face` is optional (`north`, `northeast`, `east`, `southeast`, `south`, `southwest`, `west`, `northwest` or a number of degrees).

```
npc "Name" sprite 1301 map 1 at 120, 340 face south { ...dialog... }
```

The NPC appears for players on that map, including when you reload the scripts. The facing is stored with the NPC but is not part of the spawn frame we send, so the client's own idle facing applies.

**`override npc`** replaces the dialog of an NPC that already exists, matched by its type id, and optionally by map and exact position. The most specific match wins.

```
override npc 1011 { ... }                    // every NPC of type 1011
override npc 1011 map 1 { ... }              // only on map 1
override npc 1011 map 1 at 100, 200 { ... }  // only that one
```

Use `fallback;` inside an override to let the original dialog run, for example after adding your own menu in front of it.

**`on login`, `on levelup`, `on mapenter [map]`, `on kill [monster]`, `on event "name"`** run for a character:

```
on login { ... }
on levelup { ... }          // level() is the new level
on mapenter 5 { ... }       // without a number: every map; mapid() tells which
on kill 1001 { ... }        // without a number: every monster; runs for the character that owns the kill
on event "boss_spawned" { ... }
```

`on login` and `on mapenter` run when the client has finished loading. `trigger("boss_spawned")` runs every `on event "boss_spawned"` hook for each online character. Events cannot show dialogs; use `tell()` to reach the player.

**Timers** run on the server with no character, so they can use server variables, `announce`, `trigger`, `online`, time and `rand`, but not character functions:

```
every 30 seconds { ... }    // seconds, minutes or hours; at least 5 seconds
at "20:30" { ... }          // every day at that server time
cron "*/15 8-22 * * 1-5" { ... }  // minute hour day-of-month month day-of-week
```

To act on players from a timer, call `trigger("name")` and handle it with `on event "name"`.

**`command "name" [gm]`** adds a chat command. A player types `@name arg1 arg2`; quote an argument that contains spaces. Add `gm` to restrict it to game masters. The built-in GM chat keywords (`login`, `logout`, `kick`, `speed`, `block`, ... the list is in the GM window's Help tab) always win: a script cannot define a command with one of those names, and `tposeidon scripts check` reports it. Use `arg(n)` (text, starting at 1) and `argc()` inside.

**`function name(a, b) { ... return x; }`** defines a function you can call from any script file. A function cannot have the name of a built in function. Functions can nest up to 24 deep and may use `menu`, `mes` and `next`.

### Built in functions

Character:

| Function | Result |
|----------|--------|
| `name()` | the character's name |
| `level()`, `tmlevel()`, `exp()` | level, TM level, experience |
| `job()` | job tier, 1 to 3 |
| `jobtype()` | the id of the current job |
| `class()` | the character type number |
| `money()` | gelder carried |
| `mapid()`, `posx()`, `posy()` | where the character is |
| `isgm()` | 1 for a game master |

Items and rewards:

| Function | Result |
|----------|--------|
| `countitem(id)` | how many the character carries |
| `giveitem(id [, n])` | 1 if given, 0 if the bag or weight limit does not allow it. Unknown ids stop the script with an error. |
| `takeitem(id [, n])` | 1 if taken, 0 if the character does not hold enough |
| `givemoney(n)`, `takemoney(n)` | `takemoney` returns 0 when there is not enough |
| `giveexp(n)`, `givetm(n)` | adds experience or TM experience; the amount is exact, server rates are not applied |

Items use the same bag paths as quest rewards, so the player sees the normal item frames.

World and quests:

| Function | Result |
|----------|--------|
| `warp(map, x, y)` | moves the character and ends the dialog |
| `questactive(id)`, `questdone(id)` | state of an original quest (normal quests) |
| `queststate("name")`, `setqueststate("name", n)` | your own quest state, saved on the character |
| `tell("text")` | a message to the character |
| `announce("text")` | a message to every online character |
| `trigger("name")` | runs `on event "name"` for every online character |
| `online()` | how many characters are online |

`tell` and `announce` are delivered as chat lines. A dedicated notice banner needs a capture of the original frame, so we have not built one.

Numbers, text and time:

| Function | Result |
|----------|--------|
| `rand(n)` | 0 to n-1 |
| `rand(a, b)` | a to b, inclusive |
| `min(a, b)`, `max(a, b)`, `abs(n)` | as named |
| `str(n)`, `num("12")`, `len("text")` | conversions and length |
| `now()` | seconds since 1970 |
| `today()` | the server date as a number such as 20261022 |
| `hour()`, `minute()`, `weekday()` | server time; weekday is 0 for Sunday |
| `arg(n)`, `argc()` | command arguments |

### Original NPC functions

`npcfunc(id)` runs one of the game's own numbered NPC functions that opens a window, so a script can offer the same service as the original NPC. The dialog ends and the window opens.

| Id | Window |
|----|--------|
| 16 | warehouse |
| 17 | bank |
| 35 | cash box |
| 71 | refine |
| 72 | maturing compound |
| 73 | reset mix |
| 150 | look change |
| 152 | equipment composing |
| 173 | pet level up |
| 176, 209 | equipment reform |
| 177 | pet retrain |
| 201 | fortune house |
| 208 | coupon exchange |
| 266 | melting |
| 267 | slot extend |
| 268, 269 | ghost exchange |
| 273 to 275 | soul windows |
| 20, 22, 175 | compound, exchange shop and VIP shop; give the table name as a second argument, for example `npcfunc(22, "TableName")`, using a table name from your game data |

Functions that depend on the original NPC's own data (quest check and accept, teleport lists, job checks) are not available from scripts; script your own logic with variables instead.

### Limits and safety

Scripts cannot read or write files and cannot reach the network. They can only call the functions listed above.

- Each run (the start of a dialog, or one click) may execute at most 20 000 steps. A script that loops forever stops with an error that names the file and line.
- Functions nest at most 24 deep, and the evaluation stack is limited.
- Text values are cut at 2048 characters. A page holds at most 40 lines and a menu at most 20 options.
- A file is limited to 256 KiB.
- A runtime error never crashes the server. It is logged as `file:line: message`, the dialog is closed, and the other scripts keep working.
- Every character function checks its arguments; amounts cannot be negative.

### Errors

Compile errors are reported as `file:line:column: message`, relative to the scripts folder, in the server log (lines starting with `[scripts]`), in `tposeidon scripts check`, and in the first lines of the reply to `/script reload`. Runtime errors carry `file:line:`.

## How it fits the game server

When a player talks to an NPC, the server first looks for a script: a script NPC, or an `override` that matches the NPC. If there is none, the original dialog runs untouched. Script dialogs use the same frames as the original: the opening frame, the follow-up pages, page links for menu options and Next, and the normal close. Item, money, experience and window actions go through the same code as the original quests and NPCs.

## What is not covered yet

- Dialog frames are verified against the original message format and by automated tests; we have not yet run a script against a game client, so treat the first live run as a test.
- Original quests cannot be started from a script, only checked with `questactive` and `questdone`. Use script quest state for your own quests.
- `tell` and `announce` use chat lines, as described above.
- There are no arrays or text functions beyond the list above.
