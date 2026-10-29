# Updating your players' clients

The player launcher can keep your players' clients up to date with your own content, and it shows your server's banner, name, colour and news. Everything is served by the web port your server already runs for the item shop (9990 by default), so there is nothing new to open or install.

## Only publish your own files

Publish only files you made or have the right to give away: custom content for your server such as your own models, icons, fashion and effects. Never publish the original game files, and never publish files from the client you got elsewhere. The launcher refuses `Trickster.bin` and `Trickster.exe`, and tPoseidon does not ship, host or check any original game content. You are responsible for what you put in the folder.

## The updates folder

`tposeidon launcher folder` prints it (the `updates` folder next to the program). Publishing creates the folders below if they are missing.

| Path | What it does |
|---|---|
| `client/` | Files copied into the player's client folder, with the same relative paths. `client/data/item/coat.nri` becomes `data/item/coat.nri` in the client. |
| `packs/<name>/` | The same as `client/`, but the files are tagged with the pack name in the manifest, so a custom pack (for example a fashion pack) can be added or removed as a unit. |
| `news/` | One `.txt` file per news post. The first line is the title and the rest is the text. A file named `2026-10-29 anything.txt` is dated 2026-10-29; otherwise the file's date is used. A picture with the same name and a `.png` or `.jpg` ending (`2026-10-29 anything.png`) is shown inside the post. `order.json` holds the order and the pinned posts that the panel sets; without it the newest post is first. At most 20 posts are shown, pinned ones first. |
| `banner.png` or `banner.jpg` | The picture at the top of the launcher (up to 2 MB). |
| `channel.json` | Optional: `{ "name": "My Server", "color": "#E0457B", "maintenance": "Back at 18:00 UTC", "launcherVersion": "0.60.0-beta" }`. The name defaults to the world name, the colour is the accent of the launcher, and a maintenance message turns the server's status to Maintenance and stops players from starting the game until you remove it and publish again. `launcherVersion` defaults to the version you are running; it lets a newer launcher be announced. |

## Edit it in the panel

The panel's menu has **Edit launcher news and updates...**. It opens a window with six sections, so you never have to touch the folders yourself:

- **News**: add, edit and remove posts (title, date, text, optional picture), put a post above the others with **Pin**, reorder with **Move up** and **Move down**, and see the post the way the launcher draws it.
- **Banner**: choose or replace the picture at the top of the launcher. It must be a PNG or JPG of at most 2 MB. The launcher shows it at 630 x 210, so a smaller picture is accepted with a warning that it will look blurry.
- **Maintenance**: a switch and the message players see. This writes `channel.json` and keeps the other fields in it.
- **Client files**: add files or folders with the buttons or by dropping them on the page. Folders keep their structure, and the optional field puts added files into a folder inside the client (for example `data/item`). Each file shows its size and whether it is new, changed or already published since the last publish. A file whose name matches a file in the original client (the client folder you set up the server with, and `Trickster.bin` and `Trickster.exe` always) gets a warning. The warning does not block publishing: you are responsible for publishing only your own files.
- **Publish**: shows what will change first (for example "2 news posts, banner changed, 3 files added, 1 changed, 0 removed"), takes an optional short note, and builds the manifest.
- **History**: every publish with its time, note and counts. It is stored next to your other server files as `launcher-history.json` (the newest 200 are kept). Publishing with `tposeidon launcher publish` is recorded too.

Nothing reaches players until you press **Publish**.

## Publish

Drop the files in, then press **Publish launcher update** in the panel's menu, or run:

```
tposeidon launcher publish
```

It writes `updates/manifest.json` with every file's path, size and SHA-256, and the news (with pictures and pins), banner, name and colour. Publish again whenever you change anything in the folder. The web server reads the manifest on every request, so there is no restart. A file changed after publishing is refused (409) until you publish again, so players never get a file that does not match its checksum.

## What the launcher does

When a player picks your server, the launcher reads the manifest, shows the news and your branding, and compares every listed file with the player's client. After the player's login is accepted it downloads only the files that differ, resumes a dropped download, checks each one's SHA-256, and only then replaces the player's files. Replaced files are saved first under `.tposeidon-backup` in the client folder, and the open launcher's settings have **Undo last update**. It never updates while the client is running. Updates are required: **Play** stays locked until every listed file is installed and verified, and a failed download shows **Retry**.

Safety rules the launcher and the server both enforce:

- Paths must stay inside the client folder. `..`, absolute paths, drive letters, backslashes and the launcher's own folders are rejected.
- At most 20,000 files, 1 GB per file and 8 GB in total per manifest; a manifest over 8 MB is refused.
- The server only serves files listed in the manifest.

The web port has no encryption by default. For a public server put it behind HTTPS and give your players the `https://` web address in the server file (see [Hosting securely](hosting-securely.md)). Players' passwords also travel to that port.

## Status

The launcher shows Online when the web port answers, Offline when it does not, and Maintenance when you set a maintenance message. It cannot show a player count: the web server has no access to the game servers' player lists.
