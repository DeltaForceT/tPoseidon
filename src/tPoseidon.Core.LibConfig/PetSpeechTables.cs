namespace tPoseidon.Core.LibConfig;

public sealed record PetSpeechLine(string Text, string Voice, int Threshold);

public sealed record PetSpeechParam(string Table, bool Voice, int Color);

public sealed record PetSpeechTables(
    IReadOnlyDictionary<int, PetSpeechParam> Params,
    IReadOnlyDictionary<string, IReadOnlyDictionary<int, IReadOnlyList<PetSpeechLine>>> Tables)
{
    public const string ParamTable = "PetParam";
    public const string TablePrefix = "PetSpeech";
    public const int LinesPerType = 3;
    public const double RatioScale = 1000.0;
    public const int FirstChrType = 1;
    public const int LastChrType = 479999;

    public static PetSpeechTables Empty { get; } = new(
        new Dictionary<int, PetSpeechParam>(),
        new Dictionary<string, IReadOnlyDictionary<int, IReadOnlyList<PetSpeechLine>>>());
}
