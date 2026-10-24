# Scripting status

## Audit of the earlier script layer

The first attempt lived in `src/tPoseidon.Core.Game/Dialog/Script/` and was never connected to a server.

- It builds, and the 1257 tests of the game project (which include its own) passed before we started.
- It parsed a tiny language: `script "name" <type> { mes ...; next; close; select(...); goto; if (...) }`. Variables, arithmetic and comparisons were rejected by the compiler ("not yet supported"), and no item, money, warp or variable access existed.
- It produced the text format of an older client generation (`S_Quest_Check` menu actions) and not the page links the current NPC messages use.
- No server created it, and no `.tscript` file existed.

We kept that code in place, untouched and unused, and built the working engine next to it in `src/tPoseidon.Core.Game/Scripting/`. The old layer can be removed once nothing needs its tests.

## What exists now

- Language: lexer, parser, bytecode compiler, resumable machine. Variables, expressions, loops, functions, menus with conditions.
- Library: loads a folder of `.tscript` files, reports `file:line:column` errors, skips bad files, resolves functions across files, schedules timers and cron entries.
- Variables: character, account and server scope, persisted through migration 067 (`script_variable`).
- Server (KR 2014): script NPCs placed in the world, overrides by type, map and position, hooks for login, level up, map enter, monster kill and named events, timers, chat commands, `@script reload`, and the CLI commands `tposeidon scripts check|reload|folder`.
- Tests: parser, compiler, machine, library, scheduler and variables in `tests/tPoseidon.Core.Game.Tests/Scripting`, dialog frames and the engine in `tests/tPoseidon.Core.Net.Tests/Dialogs`, and the four example scripts are compiled and run in `ExampleScriptTests`.

## Not done or not verified

- Not run against a game client yet. The dialog text follows the page-link format found in the game's own NPC message rows, and the close uses the original close frame.
- A game master tool order (`tposeidon gm ...`) for reload is not added; the chat command and the CLI marker file cover it.
- `announce` and `tell` are chat lines until the original notice frame is captured.
- Original quest actions (accept, complete) are not callable from scripts.
