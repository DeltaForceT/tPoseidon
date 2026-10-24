namespace tPoseidon.Core.Common;

public readonly record struct NpcPoint(float X, float Y, float Z, float Orientation)
{
        public static NpcPoint Xy(float x, float y) => new(x, y, 0f, 0f);

        public static NpcPoint Origin() => Xy(600f, 600f);
}
