using System.Text;
using tPoseidon.Core.Common;
using tPoseidon.Core.Data;
using tPoseidon.Core.Game.Progression;
using tPoseidon.Core.Net.World;

internal sealed class ProfileViewController
{
    private readonly ICharProfileStore _profiles;
    private readonly IPkInfoStore _pk;
    private readonly Func<ushort, ushort, InGameSessionState?> _playerOnMap;
    private readonly Func<ulong, string> _guildNameOf;
    private readonly Func<InGameSessionState, ProfileGear> _gearOf;
    private readonly Encoding _text;
    private readonly string _logTag;
    private readonly Random _rng = new();
    private readonly object _rngGate = new();

    public ProfileViewController(
        ICharProfileStore profiles,
        IPkInfoStore pk,
        Func<ushort, ushort, InGameSessionState?> playerOnMap,
        Func<ulong, string> guildNameOf,
        Func<InGameSessionState, ProfileGear> gearOf,
        Encoding text,
        string logTag)
    {
        _profiles = profiles ?? throw new ArgumentNullException(nameof(profiles));
        _pk = pk ?? throw new ArgumentNullException(nameof(pk));
        _playerOnMap = playerOnMap ?? throw new ArgumentNullException(nameof(playerOnMap));
        _guildNameOf = guildNameOf ?? throw new ArgumentNullException(nameof(guildNameOf));
        _gearOf = gearOf ?? throw new ArgumentNullException(nameof(gearOf));
        _text = text ?? throw new ArgumentNullException(nameof(text));
        _logTag = logTag ?? throw new ArgumentNullException(nameof(logTag));
    }

    public async Task ViewAsync(InGameSessionState asker, ReadOnlyMemory<byte> payload, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(asker);
        if (!ProfileViewFrames.TryReadRequest(payload.Span, out var entityId))
        {
            Console.WriteLine($"{_logTag} [profile] view request of {payload.Length} bytes not read");
            return;
        }
        if (_playerOnMap(asker.MapId, entityId) is not { Character: { } character, Stats: { } stats } target)
        {
            await SendAsync(asker, Opcode.S2C_PROFILE_VIEW_UNKNOWN, ProfileViewFrames.Unknown()).ConfigureAwait(false);
            return;
        }
        var profile = _profiles.GetOrCreate(character.CharacterId);
        var record = _pk.GetOrCreate(character.CharacterId);
        var flags = unchecked((uint)profile.PublicFlagBitmask);
        var bossKills = ProfileViewRules.Shows(flags, ProfileViewRules.ShowCollection) ? target.BossKills : [];
        var source = new ProfileSource(
            flags,
            character.Name,
            target.PlayerEntityId,
            (byte)StatGrowth.TypeIndexOf(character.FType),
            tPoseidon.Core.Game.Characters.LookChangeRules.BodyTypeOf(character),
            stats.Level,
            stats.TmLevel,
            tPoseidon.Core.Game.Characters.LookChangeRules.HighestJobOf(character),
            _guildNameOf(character.CharacterId),
            target.ProfileTitle,
            record.KillTotalCount,
            record.DeathTotalCount,
            profile.SelfIntro,
            [character.PtsP, character.PtsM, character.PtsS, character.PtsC],
            ProfileViewRules.Shows(flags, ProfileViewRules.ShowEquipment) ? _gearOf(target) : ProfileGear.Empty,
            bossKills);
        var view = ProfileViewBuilder.Build(source, _text);
        await SendAsync(asker, Opcode.S2C_PROFILE_VIEW, ProfileViewFrames.View(view, _text)).ConfigureAwait(false);
    }

    public Task CoverLetterAsync(InGameSessionState player, ReadOnlyMemory<byte> payload)
    {
        ArgumentNullException.ThrowIfNull(player);
        if (player.Character is not { } character) return Task.CompletedTask;
        switch (ProfileViewFrames.TryReadCoverLetter(payload.Span, out var letter))
        {
            case CoverLetterRead.OutOfRange:
                return SendAsync(player, Opcode.S2C_PROFILE_COVER_LETTER_REFUSED, ProfileViewFrames.CoverLetterRefused());
            case CoverLetterRead.Truncated:
                Console.WriteLine($"{_logTag} [profile] cover letter request of {payload.Length} bytes not read");
                return Task.CompletedTask;
        }
        var clean = ProfileViewRules.SanitizeCoverLetter(letter);
        var profile = _profiles.GetOrCreate(character.CharacterId);
        _profiles.Upsert(profile with { SelfIntro = _text.GetString(clean) });
        Console.WriteLine($"{_logTag} [profile] 0x{player.PlayerEntityId:X4} cover letter of {clean.Length} bytes");
        return SendAsync(player, Opcode.S2C_PROFILE_COVER_LETTER, ProfileViewFrames.CoverLetter(clean));
    }

    public Task OptionsAsync(InGameSessionState player, ReadOnlyMemory<byte> payload)
    {
        ArgumentNullException.ThrowIfNull(player);
        if (player.Character is not { } character) return Task.CompletedTask;
        if (!ProfileViewFrames.TryReadOptions(payload.Span, out var flags))
        {
            Console.WriteLine($"{_logTag} [profile] options request of {payload.Length} bytes not read");
            return Task.CompletedTask;
        }
        var profile = _profiles.GetOrCreate(character.CharacterId);
        _profiles.Upsert(profile with { PublicFlagBitmask = unchecked((int)flags) });
        Console.WriteLine($"{_logTag} [profile] 0x{player.PlayerEntityId:X4} public flags 0x{flags:X}");
        return SendAsync(player, Opcode.S2C_PROFILE_OPTIONS, ProfileViewFrames.Options(flags));
    }

    private Task SendAsync(InGameSessionState player, Opcode opcode, byte[] payload)
    {
        byte randKey;
        lock (_rngGate) randKey = (byte)_rng.Next(1, 256);
        return player.SendFrameAsync(opcode, payload, randKey, default);
    }
}
