namespace tPoseidon.Core.ClientArt;

public sealed class ClientPicture
{
    public ClientPicture(int width, int height, byte[] rgba)
    {
        ArgumentNullException.ThrowIfNull(rgba);
        if (width <= 0 || height <= 0 || rgba.Length != checked(width * height * 4))
        {
            throw new ArgumentException("The pixel buffer does not match the picture size.", nameof(rgba));
        }

        Width = width;
        Height = height;
        Rgba = rgba;
    }

    public int Width { get; }

    public int Height { get; }

    public byte[] Rgba { get; }

    public bool IsBlank
    {
        get
        {
            for (var i = 3; i < Rgba.Length; i += 4)
            {
                if (Rgba[i] != 0) return false;
            }

            return true;
        }
    }
}
