using System.Text;

namespace tPoseidon.Core.Common;

public static class ClientText
{
    public const int CodePage = 949;

    static ClientText()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        Encoding = Encoding.GetEncoding(CodePage);
    }

    public static Encoding Encoding { get; }
}
