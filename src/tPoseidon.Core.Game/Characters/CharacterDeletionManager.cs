using System.Net;
using tPoseidon.Core.Data;

namespace tPoseidon.Core.Game.Characters;

public sealed class CharacterDeletionManager
{
    private readonly IAccountStore _accountStore;
    private readonly IDeletedCharStore _deletedCharStore;
    private readonly IFriendStore? _friendStore;
    private readonly IMyCampStore? _myCampStore;
    private readonly IGuildStore? _guildStore;
    private readonly IUniqueCharDutyInfoStore? _uniqueDuty;
    private readonly ICoupleStore? _coupleStore;

    public CharacterDeletionManager(
        IAccountStore accountStore,
        IDeletedCharStore deletedCharStore,
        IFriendStore? friendStore = null,
        IMyCampStore? myCampStore = null,
        IGuildStore? guildStore = null,
        IUniqueCharDutyInfoStore? uniqueDuty = null,
        ICoupleStore? coupleStore = null)
    {
        ArgumentNullException.ThrowIfNull(accountStore);
        ArgumentNullException.ThrowIfNull(deletedCharStore);
        _accountStore = accountStore;
        _deletedCharStore = deletedCharStore;
        _friendStore = friendStore;
        _myCampStore = myCampStore;
        _guildStore = guildStore;
        _uniqueDuty = uniqueDuty;
        _coupleStore = coupleStore;
    }

    public CharDeletionOutcome DeleteCharacter(ulong characterId, uint ownerAccountId, IPAddress? clientIp)
    {
        var character = _accountStore.GetCharacterById(characterId);
        if (character is null || character.OwnerAccountId != ownerAccountId)
        {
            return CharDeletionOutcome.Fail(CharDeletionError.NotFound);
        }

        if (character.GuildState == 2)
        {
            return CharDeletionOutcome.Fail(CharDeletionError.GuildMember);
        }

        if (_coupleStore is not null)
        {
            var couple = _coupleStore.FindCoupleForCharacter(characterId);
            if (couple is not null && !couple.CoupleAuthority)
            {
                return CharDeletionOutcome.Fail(CharDeletionError.Married);
            }
        }

        _deletedCharStore.SoftDelete(characterId, ownerAccountId, character.Name, clientIp);

        _friendStore?.RemoveAllFor(characterId);

        if (_myCampStore is not null)
        {
            foreach (var slot in _myCampStore.GetSlotsForCharacter(characterId))
            {
                var deco = _myCampStore.GetDecoForSlot(slot.SlotCode);
                foreach (var d in deco)
                {
                    var item = _myCampStore.FindItem(d.ItemUid);
                    if (item is not null && item.ItemIndex is 18001 or 18002)
                    {
                        _myCampStore.DeleteItem(item.ItemUid);
                    }
                }
                _myCampStore.InitDecoForSlot(slot.SlotCode);
                _myCampStore.DeleteSlot(slot.SlotCode);
            }
        }

        if (_guildStore is not null && character.GuildState == 1)
        {
            var membership = _guildStore.FindMembershipForCharacter(characterId);
            if (membership is not null)
            {
                _guildStore.RemoveStandby(membership.GuildCode, characterId);
            }
        }

        _uniqueDuty?.Delete(characterId);

        _accountStore.DeleteCharacter(characterId, ownerAccountId);

        return CharDeletionOutcome.Success();
    }
}
