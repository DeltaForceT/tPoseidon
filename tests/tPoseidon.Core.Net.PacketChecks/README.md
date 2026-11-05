# Packet regression checks

Run `dotnet run --project tests/tPoseidon.Core.Net.PacketChecks` from the repository root.

This dependency-free executable checks synthetic treasure records for classes 0, 1
and 2, both hidden-state values, ordinary ground-item round trips, rare extensions
and consecutive record boundaries. It is an executable check, not a test adapter;
`dotnet test tPoseidon.sln` does not run it.
