# Contributing

Thanks for wanting to help. Bug reports, suggestions and code are all welcome.

## Reports and suggestions

- **Play and report bugs.** Use the Bug report form on GitHub or the [forum template](docs/beta/forum-bug-report-template.md). [This page](docs/beta/how-to-report-a-bug.md) shows where the logs are. One bug per report, with your version, your system, the steps and the server logs.
- **Tell us how the original worked.** Screenshots, videos and your own memories of how something behaved are gold. We match the original, not what we think would be nicer.
- **Suggest things.** Use the Feature request form. Missing original features come first; ideas that change the game go on a list for later.
- **Test on Linux and macOS.** There aren't many of you yet, and every report from those systems counts.
- **Answer questions** in Discussions and on the forum.

Security problems don't go in public issues. See [SECURITY.md](SECURITY.md).

## Code contributions

You can fork the repository to prepare a pull request. The [license](LICENSE) allows a fork for that purpose only: change it, build it and test it on your own machines, then send the change back as a pull request. You can't publish builds or releases of a fork, host a public game server with it, or use it for anything else.

What we're looking for first:

- Fixes for open issues
- Tests
- Translations and docs

For a bigger feature or a new system, open an issue first so we can talk it through. tPoseidon follows the original KR game, so nobody should spend a week on something that works differently.

How it works:

1. Fork the repository and make your change on a branch.
2. Keep each pull request to one change, and link the issue it fixes.
3. Sign off every commit with `git commit -s`. The `Signed-off-by` line confirms you wrote the code (or have the right to send it) and accept section 4 of the [license](LICENSE). Pull requests without it fail the checks.
4. If your change touches game behaviour, say how you know the original worked that way: your own captures, screenshots, videos or your memory of playing it.
5. The build has to come out clean and the tests have to pass.
6. Open the pull request and fill in the checklist.

We review every pull request. The project's public history is rebuilt for each release, so pull requests are not merged on GitHub. When we accept one, the change ships in the next release with you as the commit author, and we close the pull request with the version it went into. We may adjust a change before including it, and we may decline one; if we do, we'll say why.

## House rules for the project

These apply to everything you send us, now and later.

1. No game files. Nothing from the client or the game's servers: no executables, libraries, art, music, maps or data tables. Test data has to be made up or captured by you.
2. No code from the original game. Nothing taken from the original client or servers, and no decompiled or disassembled code pasted in. Describe what the original does in your own words and write your own code.
3. No copied code from elsewhere, unless its license lets us use it under ours. Say where it came from.
4. No personal data. Keep real account names, character names, passwords, IP addresses and your own folder paths out of everything you post, including logs and screenshots.
5. Name things for what they are. Anything a player sees says tPoseidon. Don't name the game's publisher or any private server in code, docs or commits.

## Building

You need the .NET 8 SDK.

```
dotnet build tPoseidon.sln
dotnet test tPoseidon.sln
```

Warnings are errors and the .NET analyzers are on, so the build has to come out clean.

## The legal bit

If you send us anything (a bug report, a suggestion or a pull request), you confirm you have the right to send it, and you give the project the license described in section 4 of the [LICENSE](LICENSE). In short, we can use your contribution in tPoseidon now and keep using it when we move to an open-source license.

Everyone follows the [Code of Conduct](CODE_OF_CONDUCT.md).
