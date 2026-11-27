# Updating tPoseidon

This page is about updating the server itself: the program, the panel and the database tables. For updating your players' game clients, see [Updating your players' clients](launcher-updates.md).

## How an update works

1. **Check.** The panel looks at the tPoseidon releases on GitHub when it starts and every six hours after that. It sends no account and no personal data, only an ordinary web request, and it backs off when GitHub says there were too many requests. When a newer release exists, a bar appears at the top of the panel with what is new.
2. **Verify.** Every release carries two extra files, `SHA256SUMS` and `SHA256SUMS.sig`. The list holds the checksum of each package and the signature proves the list comes from the tPoseidon release key, which is built into the panel. The panel downloads the package for your system (`.zip` on Windows, `.tar.gz` on Linux), checks its checksum against the signed list and refuses the update if anything does not match. Nothing on your computer is changed before this passes.
3. **Warn.** If the servers are running and you gave the panel a game master character (see below), players get an in-game notice with a countdown, 5 minutes by default.
4. **Stop and back up.** The servers stop and the database is backed up into the `backups` folder. If the backup fails, the servers start again and nothing is installed.
5. **Replace.** The new files are unpacked next to your install first. Then the program folder and the documents are swapped for the new ones. Your own files are never replaced: `tposeidon.json`, `server-data`, `log`, `backups`, `updates`, `db`, `pgsql`, `run`, `launcher-signing-key.json` and `launcher-history.json`.
6. **Start and check.** The new version starts, updates the database tables, and the updater waits to see that every server is up. If the servers were stopped, it only checks that the new program runs.
7. **Go back if it fails.** If the new version does not come up, the updater stops it, puts the old program files back, restores the database backup and starts the old version again. The panel then tells you what happened. The log of every update is in `.update/update.log` in your tPoseidon folder.

The panel closes while the files are replaced and opens again by itself when the update is done. A version that was rolled back is not tried again by the automatic mode.

## Turning it on

The default is **notify, with one click**: the panel tells you about an update and installs it when you click **Update now**. To change it open the panel menu (the three dots) and choose **Updates > Update settings**.

| Setting | What it does |
|---|---|
| How | *Notify only* shows the bar and never installs. *One click* (default) adds the **Update now** button. *Automatic* installs by itself once a day at the hour you choose, when an update is waiting. |
| Channel | *Stable* only offers full releases. *Beta* also offers beta releases. tPoseidon is in beta, so every release is a beta for now and Beta is the default. |
| Minutes | How long the in-game countdown runs before the servers stop (0 to 120, default 5). |
| Notice as | The registered game master character and its account password, used to send the in-game notice. Leave it empty and no notice is sent, so the servers stop at once. Register a character first with `tposeidon gm register --character <name>`. |

These settings are saved in the `updates` section of `tposeidon.json`. Without that section the defaults apply:

```json
"updates": {
  "mode": "oneClick",
  "channel": "beta",
  "automaticHour": 4,
  "countdownMinutes": 5,
  "noticeCharacter": "",
  "noticePassword": ""
}
```

`mode` is `notifyOnly`, `oneClick` or `automatic`, and `channel` is `stable` or `beta`.

## On Linux and headless servers

The same update runs from the command line:

```
tposeidon update check
tposeidon update install
tposeidon update status
```

`check` says whether a newer release exists and what is new. `install` does the whole sequence above and asks first (add `--yes` to skip the question). Options: `--channel stable|beta`, `--countdown <minutes>` and `--no-backup`. The install continues in the background after the command returns; `tposeidon update status` shows the result. If an update was interrupted, for example by a power cut, `tposeidon update recover` puts the old version back.

## Things to know

- Back up your own server folder now and then anyway. The updater backs up the database, not your whole folder.
- The database backup needs `pg_dump`, or PostgreSQL in a Docker container that publishes its port, the same as `tposeidon backup`. Restoring on a failed update needs `pg_restore` next to it. See [PostgreSQL](postgresql.md).
- The update only works in an installed package, where the program lives in the `app` folder next to `tposeidon.json`. It does not touch a copy you built from source.
- If you rather update by hand, set the mode to *notify only* and unpack the release over a new folder as before.
