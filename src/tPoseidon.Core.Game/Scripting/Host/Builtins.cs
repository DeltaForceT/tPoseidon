using System.Globalization;

namespace tPoseidon.Core.Game.Scripting.Host;

public sealed record Builtin(string Name, int MinimumArguments, int MaximumArguments, Func<IScriptHost, ScriptValue[], ScriptValue> Invoke);

public static class Builtins
{
    public const string QuestVariablePrefix = "quest.";

    private static readonly Builtin[] All = Build();

    private static readonly Dictionary<string, int> IdByName = All
        .Select((builtin, index) => (builtin.Name, index))
        .ToDictionary(pair => pair.Name, pair => pair.index, StringComparer.Ordinal);

    public static bool TryFind(string name, out int id) => IdByName.TryGetValue(name, out id);

    public static Builtin ById(int id) => All[id];

    private static Builtin[] Build() => new[]
    {
        Zero("name", host => ScriptValue.FromText(host.PlayerName)),
        Zero("level", host => ScriptValue.FromNumber(host.Level)),
        Zero("tmlevel", host => ScriptValue.FromNumber(host.TechniqueLevel)),
        Zero("job", host => ScriptValue.FromNumber(host.JobTier)),
        Zero("jobtype", host => ScriptValue.FromNumber(host.JobType)),
        Zero("class", host => ScriptValue.FromNumber(host.CharacterClass)),
        Zero("money", host => ScriptValue.FromNumber(host.Money)),
        Zero("exp", host => ScriptValue.FromNumber(host.Experience)),
        Zero("mapid", host => ScriptValue.FromNumber(host.MapId)),
        Zero("posx", host => ScriptValue.FromNumber(host.PositionX)),
        Zero("posy", host => ScriptValue.FromNumber(host.PositionY)),
        Zero("isgm", host => ScriptValue.FromBool(host.IsGameMaster)),
        Zero("online", host => ScriptValue.FromNumber(host.OnlinePlayers)),
        Zero("argc", host => ScriptValue.FromNumber(host.ArgumentCount)),
        Zero("now", host => ScriptValue.FromNumber(host.Now.ToUnixTimeSeconds())),
        Zero("today", host => ScriptValue.FromNumber(host.Now.Year * 10000L + host.Now.Month * 100L + host.Now.Day)),
        Zero("hour", host => ScriptValue.FromNumber(host.Now.Hour)),
        Zero("minute", host => ScriptValue.FromNumber(host.Now.Minute)),
        Zero("weekday", host => ScriptValue.FromNumber((long)host.Now.DayOfWeek)),
        Of("arg", 1, 1, (host, args) => ScriptValue.FromText(host.Argument(checked((int)args[0].Number)))),
        Of("countitem", 1, 1, (host, args) => ScriptValue.FromNumber(host.CountItem(args[0].Number))),
        Of("giveitem", 1, 2, (host, args) => ScriptValue.FromBool(host.GiveItem(args[0].Number, Amount(args, 1)))),
        Of("takeitem", 1, 2, (host, args) => ScriptValue.FromBool(host.TakeItem(args[0].Number, Amount(args, 1)))),
        Of("givemoney", 1, 1, (host, args) =>
        {
            host.GiveMoney(NonNegative(args[0]));
            return ScriptValue.Zero;
        }),
        Of("takemoney", 1, 1, (host, args) => ScriptValue.FromBool(host.TakeMoney(NonNegative(args[0])))),
        Of("giveexp", 1, 1, (host, args) =>
        {
            host.GiveExperience(NonNegative(args[0]));
            return ScriptValue.Zero;
        }),
        Of("givetm", 1, 1, (host, args) =>
        {
            host.GiveTechniqueExperience(NonNegative(args[0]));
            return ScriptValue.Zero;
        }),
        Of("warp", 3, 3, (host, args) =>
        {
            host.Warp(args[0].Number, args[1].Number, args[2].Number);
            return ScriptValue.Zero;
        }),
        Of("questactive", 1, 1, (host, args) => ScriptValue.FromBool(host.QuestActive(args[0].Number))),
        Of("questdone", 1, 1, (host, args) => ScriptValue.FromBool(host.QuestDone(args[0].Number))),
        Of("queststate", 1, 1, (host, args) => host.GetVariable(Lang.VariableScope.Character, QuestVariablePrefix + args[0].Text)),
        Of("setqueststate", 2, 2, (host, args) =>
        {
            host.SetVariable(Lang.VariableScope.Character, QuestVariablePrefix + args[0].Text, args[1]);
            return ScriptValue.Zero;
        }),
        Of("tell", 1, 1, (host, args) =>
        {
            host.Tell(args[0].Text);
            return ScriptValue.Zero;
        }),
        Of("announce", 1, 1, (host, args) =>
        {
            host.Announce(args[0].Text);
            return ScriptValue.Zero;
        }),
        Of("trigger", 1, 1, (host, args) =>
        {
            host.TriggerEvent(args[0].Text);
            return ScriptValue.Zero;
        }),
        Of("npcfunc", 1, 2, (host, args) =>
        {
            host.RunNpcFunction(args[0].Number, args.Length > 1 ? args[1].Text : null);
            return ScriptValue.Zero;
        }),
        Of("rand", 1, 2, (host, args) =>
        {
            var minimum = args.Length == 2 ? args[0].Number : 0;
            var maximum = args.Length == 2 ? args[1].Number : args[0].Number - 1;
            if (maximum < minimum) throw new ScriptRuntimeException("rand needs a range that holds at least one number");
            return ScriptValue.FromNumber(host.Random(minimum, maximum));
        }),
        Of("min", 2, 2, (_, args) => ScriptValue.FromNumber(Math.Min(args[0].Number, args[1].Number))),
        Of("max", 2, 2, (_, args) => ScriptValue.FromNumber(Math.Max(args[0].Number, args[1].Number))),
        Of("abs", 1, 1, (_, args) => ScriptValue.FromNumber(Math.Abs(args[0].Number))),
        Of("str", 1, 1, (_, args) => ScriptValue.FromText(args[0].Text)),
        Of("len", 1, 1, (_, args) => ScriptValue.FromNumber(args[0].Text.Length)),
        Of("num", 1, 1, (_, args) => ScriptValue.FromNumber(
            long.TryParse(args[0].Text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0)),
    };

    private static Builtin Zero(string name, Func<IScriptHost, ScriptValue> read) => new(name, 0, 0, (host, _) => read(host));

    private static Builtin Of(string name, int minimum, int maximum, Func<IScriptHost, ScriptValue[], ScriptValue> invoke) => new(name, minimum, maximum, invoke);

    private static long Amount(ScriptValue[] args, int index)
    {
        var amount = args.Length > index ? args[index].Number : 1;
        if (amount < 1) throw new ScriptRuntimeException("An item amount must be at least 1");
        return amount;
    }

    private static long NonNegative(ScriptValue value)
    {
        if (value.Number < 0) throw new ScriptRuntimeException("An amount cannot be negative");
        return value.Number;
    }
}
