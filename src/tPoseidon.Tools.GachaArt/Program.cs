using tPoseidon.Gacha.Art;

namespace tPoseidon.Tools.GachaArt;

internal static class Program
{
    public static int Main(string[] args)
    {
        if (args.Length != 2)
        {
            Console.Error.WriteLine("[gacha-art] usage: GachaArt <movie.swf> <outputDir>");
            return ExtractCommand.BadArguments;
        }
        return ExtractCommand.Run(args[0], args[1], Console.Out, Console.Error);
    }
}
