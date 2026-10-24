using tPoseidon.Core.Common;
using tPoseidon.Core.Game.Characters;
using tPoseidon.Core.Game.Posture;
using tPoseidon.Core.Net.World;

internal sealed class DisguiseController
{
    private readonly Func<IReadOnlyDictionary<int, int>> _kits;
    private readonly Func<InGameSessionState, bool> _otherForm;
    private readonly Func<InGameSessionState, Opcode, byte[], Task> _sendToArea;
    private readonly string _logTag;
    private readonly Random _rng = new();
    private readonly object _rngGate = new();

    public DisguiseController(
        Func<IReadOnlyDictionary<int, int>> kits,
        Func<InGameSessionState, bool> otherForm,
        Func<InGameSessionState, Opcode, byte[], Task> sendToArea,
        string logTag)
    {
        _kits = kits ?? throw new ArgumentNullException(nameof(kits));
        _otherForm = otherForm ?? throw new ArgumentNullException(nameof(otherForm));
        _sendToArea = sendToArea ?? throw new ArgumentNullException(nameof(sendToArea));
        _logTag = logTag ?? throw new ArgumentNullException(nameof(logTag));
    }

    public BodyForm FormOf(InGameSessionState player)
    {
        ArgumentNullException.ThrowIfNull(player);
        return player.Disguise.Disguised ? BodyForm.Disguised : _otherForm(player) ? BodyForm.Other : BodyForm.Normal;
    }

    public byte? DigRefusal(InGameSessionState player)
    {
        ArgumentNullException.ThrowIfNull(player);
        return DisguiseRules.DigRefusal(player.Disguise.Disguised, _otherForm(player));
    }

    public async Task<bool> UseKitAsync(InGameSessionState player, int itemId)
    {
        ArgumentNullException.ThrowIfNull(player);
        var real = player.PlayerCharacterType;
        int? kitType = _kits().TryGetValue(itemId, out var shown) ? shown : null;
        var outcome = player.Disguise.UseKit(kitType, real, !player.Disguise.Disguised && !_otherForm(player));
        if (outcome != DisguiseUse.Disguised)
        {
            Console.WriteLine($"{_logTag} [disguise] 0x{player.PlayerEntityId:X4} kit {itemId} refused: {outcome}");
            await SendAsync(player, Opcode.S2C_DISGUISE_REFUSED, Array.Empty<byte>()).ConfigureAwait(false);
            return false;
        }
        await _sendToArea(player, Opcode.S2C_DISGUISED, DisguiseFrames.Disguised(player.PlayerEntityId, player.Disguise.ShownType)).ConfigureAwait(false);
        Console.WriteLine($"{_logTag} [disguise] 0x{player.PlayerEntityId:X4} looks like character type {player.Disguise.ShownType} (kit {itemId})");
        return true;
    }

    public async Task RemoveAsync(InGameSessionState player)
    {
        ArgumentNullException.ThrowIfNull(player);
        if (!player.Disguise.Remove(player.PlayerCharacterType))
        {
            await SendAsync(player, Opcode.S2C_DISGUISE_REMOVE_REFUSED, Array.Empty<byte>()).ConfigureAwait(false);
            return;
        }
        await _sendToArea(player, Opcode.S2C_DISGUISE_REMOVED, DisguiseFrames.Removed(player.PlayerEntityId, DisguiseRules.RemovedByRequest)).ConfigureAwait(false);
        Console.WriteLine($"{_logTag} [disguise] 0x{player.PlayerEntityId:X4} disguise removed");
    }

    public async Task WearDressAsync(InGameSessionState player, int dressType)
    {
        ArgumentNullException.ThrowIfNull(player);
        if (player.Disguise.LooksDifferent) return;
        if (player.Disguise.Wear(dressType, player.PlayerCharacterType, !_otherForm(player)) != DisguiseUse.Disguised)
        {
            Console.WriteLine($"{_logTag} [disguise] 0x{player.PlayerEntityId:X4} wedding dress {dressType} stored, not shown (other form)");
            return;
        }
        await _sendToArea(player, Opcode.S2C_DISGUISED, DisguiseFrames.Disguised(player.PlayerEntityId, player.Disguise.ShownType)).ConfigureAwait(false);
        Console.WriteLine($"{_logTag} [disguise] 0x{player.PlayerEntityId:X4} wears wedding dress type {dressType}");
    }

    public async Task RemoveDressAsync(InGameSessionState player)
    {
        ArgumentNullException.ThrowIfNull(player);
        if (!player.Disguise.WearsDress || !player.Disguise.Remove(player.PlayerCharacterType)) return;
        await _sendToArea(player, Opcode.S2C_DISGUISE_REMOVED, DisguiseFrames.Removed(player.PlayerEntityId, DisguiseRules.RemovedByCostumeEnd)).ConfigureAwait(false);
        Console.WriteLine($"{_logTag} [disguise] 0x{player.PlayerEntityId:X4} wedding dress removed");
    }

    private Task SendAsync(InGameSessionState player, Opcode opcode, byte[] payload)
    {
        byte randKey;
        lock (_rngGate) randKey = (byte)_rng.Next(1, 256);
        return player.SendFrameAsync(opcode, payload, randKey, default);
    }
}
