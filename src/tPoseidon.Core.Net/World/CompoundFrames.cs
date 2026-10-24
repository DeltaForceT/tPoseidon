using System.Buffers.Binary;
using tPoseidon.Core.Game.Items;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Net.World;

public static class CompoundFrames
{
    public const int MaterialSize = 14;
    public const int RefusedCode = -1;
    public const int NoBagSpaceCode = 0;

    public static byte[] List(int grade, IReadOnlyList<int> recipeIds)
    {
        ArgumentNullException.ThrowIfNull(recipeIds);
        var w = new PacketWriter(3 + recipeIds.Count * 2);
        w.WriteByte((byte)recipeIds.Count);
        w.WriteUInt16((ushort)grade);
        foreach (var id in recipeIds) w.WriteUInt16((ushort)id);
        return w.ToArray();
    }

    public static byte[] Recipe(CompoundRecipe recipe)
    {
        ArgumentNullException.ThrowIfNull(recipe);
        var w = new PacketWriter(16 + recipe.Results.Count * 4 + recipe.Requirements.Count * 5);
        w.WriteUInt16((ushort)recipe.Type);
        w.WriteUInt16((ushort)recipe.Id);
        w.WriteUInt32((uint)recipe.Fee);
        w.WriteByte((byte)recipe.Results.Count);
        foreach (var result in recipe.Results) w.WriteUInt32((uint)result);
        w.WriteByte((byte)recipe.Requirements.Count);
        foreach (var requirement in recipe.Requirements)
        {
            w.WriteUInt32((uint)requirement.ItemId);
            w.WriteByte((byte)requirement.Count);
        }
        w.WriteByte((byte)recipe.Probability);
        return w.ToArray();
    }

    public static byte[] Percent(int percent) => new[] { (byte)percent };

    public static byte[] Refused(int code)
    {
        var w = new PacketWriter(4);
        w.WriteUInt32(unchecked((uint)code));
        return w.ToArray();
    }

    public static byte[] Result(IReadOnlyList<int> results, int wasteItem, int successes, int failures, IReadOnlyList<BagOperation> operations)
    {
        ArgumentNullException.ThrowIfNull(results);
        ArgumentNullException.ThrowIfNull(operations);
        var w = new PacketWriter(16 + results.Count * 4 + operations.Count * 24);
        w.WriteByte((byte)results.Count);
        foreach (var result in results) w.WriteUInt32((uint)result);
        w.WriteUInt32((uint)wasteItem);
        w.WriteByte((byte)successes);
        w.WriteByte((byte)failures);
        w.WriteByte((byte)operations.Count);
        foreach (var operation in operations) w.WriteBytes(QuestResultFrames.ItemOperation(operation));
        return w.ToArray();
    }

    public static bool TryReadRecipeRequest(ReadOnlySpan<byte> payload, out int recipeId)
    {
        recipeId = 0;
        if (payload.Length < sizeof(ushort)) return false;
        recipeId = BinaryPrimitives.ReadUInt16LittleEndian(payload);
        return true;
    }

    public static bool TryReadProbabilityRequest(
        ReadOnlySpan<byte> payload, out int recipeId, out byte mode, out int count, out IReadOnlyList<CompoundMaterial> materials)
    {
        recipeId = 0;
        mode = 0;
        count = 0;
        materials = Array.Empty<CompoundMaterial>();
        if (payload.Length < 5) return false;
        recipeId = BinaryPrimitives.ReadUInt16LittleEndian(payload);
        mode = payload[2];
        count = payload[3];
        return TryReadMaterials(payload[4..], out materials);
    }

    public static bool TryReadMaterials(ReadOnlySpan<byte> payload, out IReadOnlyList<CompoundMaterial> materials)
    {
        materials = Array.Empty<CompoundMaterial>();
        if (payload.Length < 1) return false;
        var n = payload[0];
        if (payload.Length < 1 + n * MaterialSize) return false;
        var list = new List<CompoundMaterial>(n);
        for (var i = 0; i < n; i++)
        {
            var entry = payload.Slice(1 + i * MaterialSize, MaterialSize);
            list.Add(new CompoundMaterial(
                BinaryPrimitives.ReadUInt64LittleEndian(entry),
                (int)BinaryPrimitives.ReadUInt32LittleEndian(entry[8..]),
                BinaryPrimitives.ReadUInt16LittleEndian(entry[12..])));
        }
        materials = list;
        return true;
    }
}
