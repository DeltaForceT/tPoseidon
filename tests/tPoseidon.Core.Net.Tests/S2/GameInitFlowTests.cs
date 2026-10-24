using System.Net;
using System.Net.Sockets;
using tPoseidon.Core.Common;
using tPoseidon.Core.Crypto.S2;
using tPoseidon.Core.Data;
using tPoseidon.Core.Net.Packets;
using tPoseidon.Core.Net.S2;
using Xunit;

namespace tPoseidon.Core.Net.Tests.S2;

public sealed class GameInitFlowTests
{
    [Fact]
    public async Task GameInit_TriggersBatchReplyWithCharDataAndSkillList()
    {
        var keyTable = KeyTable.Identity();
        IS2CipherDerivation derivation = new IdentityDerivation();
        var cipher = new S2PacketCipher(keyTable, derivation);

        var accountStore = new InMemoryAccountStore();
        var account = accountStore.RegisterAccount("flowtest", PasswordHasher.Hash("flowtestpw"));

        const ulong testCharId = 0x10000001ul;
        var devCharacter = new Character
        {
            CharacterId = testCharId,
            OwnerAccountId = account.AccountId,
            Slot = 0,
            Name = "TestChar",
            Species = Species.Buffalo,
            FType = FType.Power,
            Level = 1,
        };
        accountStore.AddCharacter(devCharacter);

        await using var acceptor = new TcpAcceptor(
            new IPEndPoint(IPAddress.Loopback, 0),
            async (session, ct) =>
            {
                var sessionInfo = new SessionInfo { IsStrict = false };
                var dispatcher = new OpcodeDispatcher();
                dispatcher.Register(Opcode.GAME_INIT, async (_, _, innerCt) =>
                {
                    var charData = new GameCharDataPacket
                    {
                        CharacterId = devCharacter.CharacterId,
                        UserId = devCharacter.OwnerAccountId,
                        Slot = devCharacter.Slot,
                        Name = devCharacter.Name,
                        Species = (ushort)devCharacter.Species,
                    };
                    var charFrame = cipher.Encrypt(
                        (ushort)Opcode.GAME_CHAR_DATA, 0, 0x01,
                        PackingFlags.PayloadEncrypted,
                        charData.EncodePayload(),
                        sessionInfo);
                    await session.SendAsync(charFrame, innerCt);

                    var skillFrame = cipher.Encrypt(
                        (ushort)Opcode.GAME_SKILL_LIST, 0, 0x01,
                        PackingFlags.PayloadEncrypted,
                        new GameSkillListPacket().EncodePayload(),
                        sessionInfo);
                    await session.SendAsync(skillFrame, innerCt);
                });

                var adapter = new S2SessionAdapter(
                    session, cipher, keyTable, derivation, dispatcher, sessionInfo);
                await adapter.RunAsync(ct);
            });
        acceptor.Start();
        var port = acceptor.BoundEndpoint!.Port;

        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, port);
        var stream = client.GetStream();

        var clientSession = new SessionInfo { IsStrict = false };
        var gameInitFrame = cipher.Encrypt(
            (ushort)Opcode.GAME_INIT, 1, 0x4F,
            PackingFlags.PayloadEncrypted,
            Array.Empty<byte>(),
            clientSession);
        await stream.WriteAsync(gameInitFrame);

        var charDataWireSize = 9 + GameCharDataPacket.PayloadSize + 2;
        var skillListWireSize = 9 + GameSkillListPacket.HeaderSize + 2;
        var totalExpected = charDataWireSize + skillListWireSize;

        var buf = new byte[totalExpected];
        var read = 0;
        while (read < buf.Length)
        {
            var n = await stream.ReadAsync(buf.AsMemory(read, buf.Length - read))
                .AsTask().WaitAsync(TimeSpan.FromSeconds(5));
            if (n == 0) break;
            read += n;
        }
        Assert.Equal(totalExpected, read);

        var serverDecryptSession = new SessionInfo { IsStrict = false };
        var first = cipher.Decrypt(buf.AsSpan(0, charDataWireSize), serverDecryptSession);
        Assert.Equal((ushort)Opcode.GAME_CHAR_DATA, first.Opcode);
        var charDataDecoded = GameCharDataPacket.DecodePayload(first.Payload);
        Assert.Equal(devCharacter.CharacterId, charDataDecoded.CharacterId);
        Assert.Equal(devCharacter.Name, charDataDecoded.Name);

        var second = cipher.Decrypt(buf.AsSpan(charDataWireSize, skillListWireSize), serverDecryptSession);
        Assert.Equal((ushort)Opcode.GAME_SKILL_LIST, second.Opcode);
        var skillListDecoded = GameSkillListPacket.DecodePayload(second.Payload);
        Assert.Empty(skillListDecoded.Entries);
    }
}
