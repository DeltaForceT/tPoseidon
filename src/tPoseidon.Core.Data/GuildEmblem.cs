namespace tPoseidon.Core.Data;

public readonly record struct GuildEmblem(byte Template, byte Colour, uint Icon);

public sealed record GuildTag(string GuildName, string Alias, GuildEmblem Emblem);

public readonly record struct GuildEmblemState(GuildEmblem Emblem, byte Purchase, uint Times, DateTimeOffset? ChangedAt);

public readonly record struct GuildEmblemRequest(byte Template, byte Colour, ulong ItemUid);

public readonly record struct GuildEmblemDecision(int Error, GuildEmblemState After);

public static class GuildEmblemPurchase
{
    public const byte None = 0;
    public const byte OneEmblem = 1;
    public const byte Package = 2;
}

public static class GuildEmblemErrors
{
    public const int Ok = 0;
    public const int ItemUnverified = 101;
    public const int EntryError = 102;
    public const int ChoiceMissing = 103;
    public const int BadTemplate = 104;
    public const int BadColour = 105;
    public const int NoGuildInfo = 106;
    public const int NotBought = 107;
    public const int NoPermission = 108;
    public const int OncePerDay = 109;
}

public static class GuildEmblemRules
{
    public const byte NoValue = 0xFF;
    public const byte NoChoice = 0;
    public const int TemplateCount = 10;
    public const int FirstPremiumTemplate = 6;
    public const int ColourCount = 16;
    public const ushort PermissionRight = 0x80;
    public const int PermissionRightIndex = 7;
    public static readonly TimeSpan ChangeInterval = TimeSpan.FromDays(1);

    public static GuildEmblemDecision Decide(GuildEmblemState state, GuildEmblemRequest request, ushort rights, uint? icon, DateTimeOffset now)
    {
        var error = Check(state, request, rights, icon, now);
        if (error != GuildEmblemErrors.Ok) return new GuildEmblemDecision(error, state);
        var purchase = state.Purchase == GuildEmblemPurchase.OneEmblem ? GuildEmblemPurchase.None : state.Purchase;
        return new GuildEmblemDecision(
            GuildEmblemErrors.Ok,
            new GuildEmblemState(new GuildEmblem(request.Template, request.Colour, icon ?? 0), purchase, state.Times + 1, now));
    }

    private static int Check(GuildEmblemState state, GuildEmblemRequest request, ushort rights, uint? icon, DateTimeOffset now)
    {
        if (request.ItemUid != 0 && icon is null) return GuildEmblemErrors.ItemUnverified;
        if (request.Template == NoValue || request.Colour == NoValue) return GuildEmblemErrors.EntryError;
        if (request.Template == NoChoice || request.Colour == NoChoice) return GuildEmblemErrors.ChoiceMissing;
        if (request.Template > TemplateCount) return GuildEmblemErrors.BadTemplate;
        if (request.Template >= FirstPremiumTemplate && state.Purchase != GuildEmblemPurchase.Package) return GuildEmblemErrors.BadTemplate;
        if (request.Colour > ColourCount) return GuildEmblemErrors.BadColour;
        if (state.Purchase == GuildEmblemPurchase.None) return GuildEmblemErrors.NotBought;
        if ((rights & PermissionRight) == 0) return GuildEmblemErrors.NoPermission;
        if (state.ChangedAt is { } changed && now - changed < ChangeInterval) return GuildEmblemErrors.OncePerDay;
        return GuildEmblemErrors.Ok;
    }
}
