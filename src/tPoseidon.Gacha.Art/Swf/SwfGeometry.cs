namespace tPoseidon.Gacha.Art.Swf;

public readonly record struct SwfRect(int XMin, int XMax, int YMin, int YMax)
{
    public static SwfRect Read(SwfReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);
        reader.Align();
        var bits = (int)reader.UBits(5);
        var rect = new SwfRect(reader.SBits(bits), reader.SBits(bits), reader.SBits(bits), reader.SBits(bits));
        reader.Align();
        return rect;
    }
}

public readonly record struct SwfColor(byte R, byte G, byte B, byte A)
{
    public static SwfColor ReadRgb(SwfReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);
        return new SwfColor(reader.U8(), reader.U8(), reader.U8(), 255);
    }

    public static SwfColor ReadRgba(SwfReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);
        return new SwfColor(reader.U8(), reader.U8(), reader.U8(), reader.U8());
    }

    public static SwfColor ReadArgb(SwfReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);
        var a = reader.U8();
        return new SwfColor(reader.U8(), reader.U8(), reader.U8(), a);
    }
}

public readonly record struct SwfMatrix(double A, double B, double C, double D, int TranslateX, int TranslateY)
{
    public static readonly SwfMatrix Identity = new(1, 0, 0, 1, 0, 0);

    public static SwfMatrix Read(SwfReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);
        reader.Align();
        double a = 1, d = 1, b = 0, c = 0;
        if (reader.Flag())
        {
            var bits = (int)reader.UBits(5);
            a = reader.FBits(bits);
            d = reader.FBits(bits);
        }
        if (reader.Flag())
        {
            var bits = (int)reader.UBits(5);
            b = reader.FBits(bits);
            c = reader.FBits(bits);
        }
        var translateBits = (int)reader.UBits(5);
        var tx = reader.SBits(translateBits);
        var ty = reader.SBits(translateBits);
        reader.Align();
        return new SwfMatrix(a, b, c, d, tx, ty);
    }
}

public readonly record struct SwfColorTransform(double RedMultiplier, double GreenMultiplier, double BlueMultiplier, double AlphaMultiplier, int RedAdd, int GreenAdd, int BlueAdd, int AlphaAdd)
{
    public static readonly SwfColorTransform Identity = new(1, 1, 1, 1, 0, 0, 0, 0);

    public bool IsIdentity => this == Identity;

    public static SwfColorTransform Read(SwfReader reader, bool withAlpha)
    {
        ArgumentNullException.ThrowIfNull(reader);
        reader.Align();
        var hasAdd = reader.Flag();
        var hasMultiply = reader.Flag();
        var bits = (int)reader.UBits(4);
        double rm = 1, gm = 1, bm = 1, am = 1;
        int ra = 0, ga = 0, ba = 0, aa = 0;
        if (hasMultiply)
        {
            rm = reader.SBits(bits) / 256.0;
            gm = reader.SBits(bits) / 256.0;
            bm = reader.SBits(bits) / 256.0;
            if (withAlpha) am = reader.SBits(bits) / 256.0;
        }
        if (hasAdd)
        {
            ra = reader.SBits(bits);
            ga = reader.SBits(bits);
            ba = reader.SBits(bits);
            if (withAlpha) aa = reader.SBits(bits);
        }
        reader.Align();
        return new SwfColorTransform(rm, gm, bm, am, ra, ga, ba, aa);
    }
}
