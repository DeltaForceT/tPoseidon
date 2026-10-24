using System.Net;
using System.Net.Sockets;
using tPoseidon.Core.Common;
using tPoseidon.Core.Crypto.S2;
using tPoseidon.Core.Net.Packets;
using tPoseidon.Core.Net.S2;
using Xunit;

namespace tPoseidon.Core.Net.Tests.S2;

public sealed class CharListSendOnConnectTests
{
    [Fact]
    public async Task ServerSendsEncryptedCharListImmediatelyOnConnect()
    {
        var keyTable = KeyTable.Identity();
        IS2CipherDerivation derivation = new IdentityDerivation();
        var cipher = new S2PacketCipher(keyTable, derivation);

        static byte[] MakeEntry(string name, byte slot)
        {
            var entry = new byte[CharacterListPacket.EntryWidth];
            entry[0x08] = slot;
            var n = System.Text.Encoding.ASCII.GetBytes(name);
            Array.Copy(n, 0, entry, 0x09, n.Length);
            return entry;
        }
        var src = new CharacterListPacket
        {
            Entries = new[]
            {
                new CharacterListPacket.Entry { Raw = MakeEntry("FirstChar", 0), Name = "FirstChar" },
                new CharacterListPacket.Entry { Raw = MakeEntry("SecondChar", 1), Name = "SecondChar" },
                new CharacterListPacket.Entry { Raw = MakeEntry("ThirdChar", 2), Name = "ThirdChar" },
            },
        };

        await using var acceptor = new TcpAcceptor(
            new IPEndPoint(IPAddress.Loopback, 0),
            async (session, ct) =>
            {
                var sessionInfo = new SessionInfo { IsStrict = false };
                var charListPayload = src.EncodePayload();
                var frame = cipher.Encrypt(
                    opcode: (ushort)Opcode.CHAR_LIST,
                    sequence: 0,
                    randKey: 0x01,
                    flags: PackingFlags.PayloadEncrypted,
                    plaintextPayload: charListPayload,
                    session: sessionInfo);
                await session.SendAsync(frame, ct);
                await Task.Delay(TimeSpan.FromMilliseconds(100), ct);
            });
        acceptor.Start();
        var port = acceptor.BoundEndpoint!.Port;

        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, port);
        var stream = client.GetStream();

        var expectedPayloadSize = src.EncodePayload().Length;
        var expectedFrameSize = 9 + expectedPayloadSize + 2;
        var buf = new byte[expectedFrameSize];
        var totalRead = 0;
        while (totalRead < buf.Length)
        {
            var n = await stream.ReadAsync(buf.AsMemory(totalRead, buf.Length - totalRead))
                .AsTask().WaitAsync(TimeSpan.FromSeconds(5));
            if (n == 0) break;
            totalRead += n;
        }
        Assert.Equal(expectedFrameSize, totalRead);

        var clientSession = new SessionInfo { IsStrict = false };
        var decoded = cipher.Decrypt(buf, clientSession);

        Assert.Equal((ushort)Opcode.CHAR_LIST, decoded.Opcode);
        var decodedList = CharacterListPacket.DecodePayload(decoded.Payload);
        Assert.Equal(3, decodedList.Entries.Count);
        var expectedNames = new[] { "FirstChar", "SecondChar", "ThirdChar" };
        Assert.Equal(expectedNames, decodedList.Entries.Select(e => e.Name).ToArray());
    }
}
