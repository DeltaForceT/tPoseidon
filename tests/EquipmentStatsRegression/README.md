# Equipment stat regression

Run `dotnet run --project tests/EquipmentStatsRegression` from the repository root.
This dependency-free executable is separate from `dotnet test tPoseidon.sln`.

The 375 synthetic scenarios exercise the production equipment calculation and
property rules with an in-memory inventory/property store: all twelve stats,
all ten compound slots, stored random bases below/equal to/above defaults,
refine bonuses, absent/default properties, unrelated properties, multiple items,
option-only definitions, repeated recalculation, equip/release and character
isolation. Existing extra bonuses must remain additive and unmodified.

No database or client is used. The supplied patch's HP/MP reproduction is
synthetic too; neither establishes live-client validation of this source port.

The PR CI workflow runs this executable after the solution build.
