using tPoseidon.Core.Common;
using tPoseidon.Core.Data;
using tPoseidon.Core.Net.Packets;

namespace tPoseidon.Core.Net.S2;

public sealed class LoginSessionHandler
{
    private readonly IAccountStore _accounts;

    public LoginSessionHandler(IAccountStore accounts)
    {
        ArgumentNullException.ThrowIfNull(accounts);
        _accounts = accounts;
    }

    public LoginSessionResult Handle(ReadOnlySpan<byte> sessionInitPayload)
    {
        SessionInitPacket pkt;
        try
        {
            pkt = SessionInitPacket.DecodePayload(sessionInitPayload);
        }
        catch (InvalidPacketException ex)
        {
            return LoginSessionResult.Reject(LoginRejectReason.MalformedSessionInit, ex.Message);
        }

        var token = _accounts.ValidateAndConsumeSessionToken(pkt.SessionToken);
        if (token is null)
        {
            return LoginSessionResult.Reject(LoginRejectReason.InvalidToken,
                $"token 0x{pkt.SessionToken:X8} unknown / expired / already consumed");
        }

        var character = _accounts.GetCharacterById(pkt.CharacterId);
        if (character is null)
        {
            return LoginSessionResult.Reject(LoginRejectReason.UnknownCharacter,
                $"char 0x{pkt.CharacterId:X8} not found");
        }
        if (character.OwnerAccountId != token.AccountId)
        {
            return LoginSessionResult.Reject(LoginRejectReason.CharacterOwnershipMismatch,
                $"char 0x{pkt.CharacterId:X8} not owned by account {token.AccountId}");
        }

        return LoginSessionResult.Accept(token.AccountId, character);
    }
}

public enum LoginRejectReason
{
    MalformedSessionInit,
    InvalidToken,
    UnknownCharacter,
    CharacterOwnershipMismatch,
}

public sealed class LoginSessionResult
{
    public bool Accepted { get; }
    public uint AccountId { get; }
    public Character? Character { get; }
    public LoginRejectReason? RejectReason { get; }
    public string Message { get; }

    private LoginSessionResult(bool accepted, uint accountId, Character? character,
                               LoginRejectReason? rejectReason, string message)
    {
        Accepted = accepted;
        AccountId = accountId;
        Character = character;
        RejectReason = rejectReason;
        Message = message;
    }

    public static LoginSessionResult Accept(uint accountId, Character character) =>
        new(accepted: true, accountId, character, rejectReason: null,
            message: $"authenticated as account {accountId}, character '{character.Name}' (slot {character.Slot})");

    public static LoginSessionResult Reject(LoginRejectReason reason, string message) =>
        new(accepted: false, accountId: 0, character: null, rejectReason: reason, message: message);
}
