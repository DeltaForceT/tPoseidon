# Contributing

Thanks for wanting to help. This is how it works while tPoseidon is in beta, and how it changes at 1.0.

## During the beta: reports and suggestions

Right now we're taking bug reports and suggestions, not code. Please don't fork the repository to work on it. The [beta license](LICENSE) doesn't allow forks or modified copies, and we can't review a pile of competing changes while the basics are still moving.

What helps most:

- **Play and report bugs.** Use the Bug report form on GitHub or the [forum template](docs/beta/forum-bug-report-template.md). [This page](docs/beta/how-to-report-a-bug.md) shows where the logs are. One bug per report, with your version, your system, the steps and the server logs.
- **Tell us how the original worked.** Screenshots, videos and your own memories of how something behaved are gold. We match the original, not what we think would be nicer.
- **Suggest things.** Use the Feature request form. Missing original features come first; ideas that change the game go on a list for later.
- **Test on Linux and macOS.** There aren't many of you yet, and every report from those systems counts.
- **Answer questions** in Discussions and on the forum.

Pull requests opened during the beta will be closed with a pointer to this file. Please don't spend time on one.

If you've already found the fix for something, put it in the bug report: which file, what's wrong, what you'd change. We'll make the change ourselves and credit you in the release notes if you want.

Security problems don't go in public issues. See [SECURITY.md](SECURITY.md).

## At 1.0: code contributions

When the beta ends we plan to move tPoseidon to an open-source license (AGPL). From then on forks and pull requests are welcome. Roughly:

1. Open an issue first for anything bigger than a small fix, so nobody spends a weekend on something we'd do differently.
2. Keep each pull request to one change.
3. If your change touches game behaviour, tell us how you know the original worked that way: your own packet captures, screenshots, videos, wiki pages.
4. The build has to come out clean and the tests have to pass.

We'll publish the final contribution terms with the license change. Until then, nothing here is a request for code.

## House rules for the project

These apply to everything you send us, now and later.

1. No game files. Nothing from the client or the game's servers: no executables, libraries, art, music, maps or data tables. Test data has to be made up or captured by you.
2. No copied code, unless its license lets us use it under ours. Say where it came from.
3. No personal data. Keep real account names, character names, passwords, IP addresses and your own folder paths out of everything you post, including logs and screenshots.
4. Name things for what they are. Anything a player sees says tPoseidon.

## Building and testing

You can build and test the source on your own machine to check a bug or prepare a report. You need the .NET 8 SDK.

```
dotnet build tPoseidon.sln
dotnet test tPoseidon.sln
```

Warnings are errors and the .NET analyzers are on, so the build has to come out clean. A few tests need local reference data that isn't in the repo; they skip themselves when it's missing.

## The legal bit

If you send us anything (a bug report with a code suggestion, and later a pull request), you confirm you have the right to send it, and you give the project the license described in section 4 of the [LICENSE](LICENSE). In short, we can use your suggestion in tPoseidon now and keep using it when we move to an open-source license.

Everyone follows the [Code of Conduct](CODE_OF_CONDUCT.md).
