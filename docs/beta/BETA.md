# The tPoseidon beta

What the beta is, what to expect, and where to get help.

## Why a beta

A lot of the game works. What we don't know yet is how it holds up with real players on other people's machines, other clients and other operating systems. That's what the beta is for. Once it stops turning up serious problems, we'll call it 1.0.

## Stages

**Closed beta.** A small group of invited testers. We want to know three things: does setup work on Windows, Linux and macOS, can you play from character creation onward, and does updating ever lose data. Expect frequent builds, sometimes a few a week.

**Open beta.** Anyone can download it. We slow down on new features and spend the time on bugs, performance, and making hosting easier. Fewer builds, and each one gets proper release notes.

**1.0.** When nothing known blocks normal play or loses data, and updating from one version to the next has been tested.

We announce each move on the forum and in the release notes.

## Beta rules

- **No forks during the beta.** Download it, run it, host it for yourself and your community, that's all fine. Publishing a modified copy, a fork or an emulator built from tPoseidon is not, and neither is re-uploading the source or the packages somewhere else. The details are in the [LICENSE](../../LICENSE). We plan to move to an open-source license after the beta.
- While we're in beta the project offers downloads (the release packages) and takes bug reports and suggestions. Code contributions and forks open at 1.0.
- Bug reports and suggestions are welcome. We aren't taking code contributions during the beta; see [CONTRIBUTING.md](../../CONTRIBUTING.md) for how that opens up at 1.0.
- Bring your own client. We don't ship it, and the project's spaces aren't the place to share it.

## Your data and updates

Everyone runs their own server, so there are no wipes. Your accounts and characters live in your database and stay there.

What can change is how that data is stored:

- When a new version needs database changes, the server applies them on start. `tposeidon start` warns you first and offers to make a backup. Take it. You can also run `tposeidon backup` any time. Backups use `pg_dump`, which comes with PostgreSQL.
- Save formats can change between beta versions. Once a newer version has touched your database, don't go back to an older one. If you need to roll back, restore the backup from before the update.
- It's beta software, so back up before every update and every now and then while you play.
- Unpacking a new version over the old one leaves `tposeidon.json`, `server-data/`, `backups/` and `log/` alone, and your database lives in PostgreSQL, outside the folder. If a setting gets renamed or removed, the release notes say so.

If you prefer a window, `tposeidon-panel` (`tposeidon-panel.cmd` on Windows) opens the control panel. It runs the same setup, start, stop and backup as the commands, and shows each server's log.

To update:

1. `tposeidon stop`
2. `tposeidon backup`
3. Unpack the new release over the old folder.
4. `tposeidon start`

## Version numbers

They look like `0.MINOR.PATCH-beta`. The first beta is `0.55.0-beta`, shown as v0.55 Beta; a fix release after it would be `0.55.1-beta`.

- MINOR goes up when we add systems or the database changes.
- PATCH goes up for fixes that don't touch the database.

You'll find the version in `tposeidon status`, the control panel's title bar and the release file name. Put it in every bug report.

## Known issues

Every release has a Known issues list in its notes, and there's a pinned thread on the forum. Issues on GitHub with the `known-issue` label are the same list. Have a look before you report something.

For now, expect:

- some systems missing or half done (the release notes say what's in);
- text, positions or timings that don't quite match the original yet;
- the control panel is packaged for Windows, Linux and macOS but has only been tried on Windows; the `tposeidon` command works everywhere;
- Linux and macOS less tested than Windows.

## How fixes reach you

A bug gets reported, we confirm it and label it, the fix goes in, and the next release notes list it with the issue number. Fix-only releases bump PATCH and go straight over the previous version. Releases with database changes bump MINOR and say so at the top of the notes, so back up before those.

Security fixes can come out on their own at any time. During the beta only the latest version gets fixes.

## Where to get help

| You want to | Go to |
|---|---|
| ask a question or get setup help | the forum, or GitHub Discussions |
| report a bug | the Bug report form on GitHub, or the forum with [the template](forum-bug-report-template.md) |
| suggest something | the Feature request form on GitHub |
| report a security problem | privately, see [SECURITY.md](../../SECURITY.md) |

[How to report a bug](how-to-report-a-bug.md) explains where the logs are and what to send. Everyone follows the [Code of Conduct](../../CODE_OF_CONDUCT.md).
