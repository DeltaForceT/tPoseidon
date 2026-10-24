using System.Text.Json;
using tPoseidon.Gacha.Art.Images;
using tPoseidon.Gacha.Art.Output;
using tPoseidon.Gacha.Art.Swf;

namespace tPoseidon.Tools.GachaArt.Tests;

public sealed class GachaArtTests
{
    private static readonly string[] ExpectedLabels = ["go"];
    private static readonly string[] ExpectedConditions = ["overUpToOverDown"];
    private static readonly int[] ExpectedAdvances = [100, 110];

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Header_IsReadFromPlainAndCompressedMovies(bool compressed)
    {
        var builder = new SwfBuilder { Frames = 3 };
        builder.Add(SwfTagCode.SetBackgroundColor, [0x99, 0x98, 0x97]);

        var file = SwfFile.Parse(builder.Build(compressed));
        var library = SwfLibrary.Load(file);

        Assert.Equal(8, file.Version);
        Assert.Equal(640 * 20, file.FrameSize.XMax);
        Assert.Equal(480 * 20, file.FrameSize.YMax);
        Assert.Equal(20.0, file.FrameRate);
        Assert.Equal(3, file.FrameCount);
        Assert.Equal(new SwfColor(0x99, 0x98, 0x97, 255), library.Background);
    }

    [Fact]
    public void SolidRectangle_BecomesOneClosedFillPath()
    {
        var library = Load(new SwfBuilder().Add(SwfTagCode.DefineShape, SwfBuilder.SolidRectangleShape(1, 200, 200, 10, 20, 30)));

        var shape = library.Shapes[1];
        var layer = Assert.Single(shape.Layers);
        var fill = Assert.Single(layer.Fills);
        Assert.Equal(1, fill.Style);
        Assert.Equal("M0 0L10 0L10 10L0 10L0 0Z", LibraryWriter.PathData(fill.Commands, false));
        var solid = Assert.IsType<SolidFill>(layer.Styles.Fills[0].Start);
        Assert.Equal(new SwfColor(10, 20, 30, 255), solid.Color);
    }

    [Fact]
    public void SharedEdge_IsLinkedIntoBothNeighbouringFills()
    {
        var w = new BitWriter().U16(5).Rect(0, 800, 0, 400);
        w.U8(2).U8(0x00).U8(255).U8(0).U8(0).U8(0x00).U8(0).U8(0).U8(255);
        w.U8(0);
        w.Bits(2, 4).Bits(0, 4);
        w.Bits(0, 1).Bits(0b00101, 5).Bits(16, 5).Bits(0, 16).Bits(0, 16).Bits(1, 2);
        SwfBuilder.StraightX(w, 400);
        w.Bits(0, 1).Bits(0b00010, 5).Bits(2, 2);
        SwfBuilder.StraightY(w, 400);
        w.Bits(0, 1).Bits(0b00010, 5).Bits(0, 2);
        SwfBuilder.StraightX(w, -400);
        SwfBuilder.StraightY(w, -400);
        w.Bits(0, 1).Bits(0b00111, 5).Bits(16, 5).Bits(400, 16).Bits(0, 16).Bits(0, 2).Bits(2, 2);
        SwfBuilder.StraightX(w, 400);
        SwfBuilder.StraightY(w, 400);
        SwfBuilder.StraightX(w, -400);
        w.Bits(0, 1).Bits(0, 5);

        var layer = Assert.Single(Load(new SwfBuilder().Add(SwfTagCode.DefineShape, w)).Shapes[5].Layers);

        Assert.Equal(2, layer.Fills.Count);
        Assert.Equal("M0 0L20 0L20 20L0 20L0 0Z", LibraryWriter.PathData(layer.Fills[0].Commands, false));
        Assert.Equal("M20 20L20 0L40 0L40 20L20 20Z", LibraryWriter.PathData(layer.Fills[1].Commands, false));
    }

    [Fact]
    public void MorphShape_KeepsStartAndEndGeometry()
    {
        var styles = new BitWriter().U8(1).U8(0x00).U8(1).U8(2).U8(3).U8(255).U8(4).U8(5).U8(6).U8(0).U8(0).ToArray();
        var start = new BitWriter().Bits(1, 4).Bits(0, 4);
        start.Bits(0, 1).Bits(0b00101, 5).Bits(16, 5).Bits(0, 16).Bits(0, 16).Bits(1, 1);
        SwfBuilder.StraightX(start, 200);
        SwfBuilder.StraightY(start, 200);
        SwfBuilder.StraightX(start, -200);
        SwfBuilder.StraightY(start, -200);
        start.Bits(0, 1).Bits(0, 5);
        var end = new BitWriter().Bits(0, 4).Bits(0, 4);
        end.Bits(0, 1).Bits(0b00001, 5).Bits(16, 5).Bits(0, 16).Bits(0, 16);
        SwfBuilder.StraightX(end, 400);
        SwfBuilder.StraightY(end, 400);
        SwfBuilder.StraightX(end, -400);
        SwfBuilder.StraightY(end, -400);
        end.Bits(0, 1).Bits(0, 5);
        var startBytes = start.ToArray();
        var body = new BitWriter().U16(9).Rect(0, 200, 0, 200).Rect(0, 400, 0, 400)
            .U32((uint)(styles.Length + startBytes.Length)).Raw(styles).Raw(startBytes).Raw(end.ToArray());

        var shape = Load(new SwfBuilder().Add(SwfTagCode.DefineMorphShape, body)).Shapes[9];

        Assert.True(shape.IsMorph);
        var fill = Assert.Single(Assert.Single(shape.Layers).Fills);
        Assert.Equal("M0 0L10 0L10 10L0 10L0 0Z", LibraryWriter.PathData(fill.Commands, false));
        Assert.Equal("M0 0L20 0L20 20L0 20L0 0Z", LibraryWriter.PathData(fill.Commands, true));
        var pair = shape.Layers[0].Styles.Fills[0];
        Assert.Equal(new SwfColor(1, 2, 3, 255), Assert.IsType<SolidFill>(pair.Start).Color);
        Assert.Equal(new SwfColor(4, 5, 6, 0), Assert.IsType<SolidFill>(pair.End).Color);
    }

    [Fact]
    public void Sprite_TimelineKeepsPlacementsMovesRemovalsAndLabels()
    {
        var inner = new List<byte>();
        inner.AddRange(SwfBuilder.Tag(SwfTagCode.PlaceObject2, new BitWriter().U8(0x2E).U16(1).U16(1).Matrix(100, 200).AlphaTransform(128).Text("box").ToArray()));
        inner.AddRange(SwfBuilder.Tag(SwfTagCode.FrameLabel, new BitWriter().Text("go").ToArray()));
        inner.AddRange(SwfBuilder.Tag(SwfTagCode.ShowFrame, []));
        inner.AddRange(SwfBuilder.Tag(SwfTagCode.PlaceObject2, new BitWriter().U8(0x05).U16(1).Matrix(300, 400, 2, 2).ToArray()));
        inner.AddRange(SwfBuilder.Tag(SwfTagCode.ShowFrame, []));
        inner.AddRange(SwfBuilder.Tag(SwfTagCode.RemoveObject2, new BitWriter().U16(1).ToArray()));
        inner.AddRange(SwfBuilder.Tag(SwfTagCode.ShowFrame, []));
        inner.AddRange(SwfBuilder.Tag(SwfTagCode.End, []));
        var sprite = new BitWriter().U16(2).U16(3).Raw(inner.ToArray());

        var library = Load(new SwfBuilder()
            .Add(SwfTagCode.DefineShape, SwfBuilder.SolidRectangleShape(1, 200, 200, 1, 1, 1))
            .Add(SwfTagCode.DefineSprite, sprite));

        var frames = library.Sprites[2].Frames;
        Assert.Equal(3, frames.Count);
        Assert.Equal(ExpectedLabels, frames[0].Labels);
        var place = Assert.IsType<PlaceOperation>(Assert.Single(frames[0].Operations));
        Assert.False(place.Move);
        Assert.Equal(1, place.CharacterId);
        Assert.Equal("box", place.Name);
        Assert.Equal(100, place.Matrix!.Value.TranslateX);
        Assert.Equal(0.5, place.ColorTransform!.Value.AlphaMultiplier);
        var move = Assert.IsType<PlaceOperation>(Assert.Single(frames[1].Operations));
        Assert.True(move.Move);
        Assert.Null(move.CharacterId);
        Assert.Equal(2.0, move.Matrix!.Value.A);
        Assert.Equal(1, Assert.IsType<RemoveOperation>(Assert.Single(frames[2].Operations)).Depth);
    }

    [Fact]
    public void Button_KeepsStateRecordsConditionsAndSounds()
    {
        var button = new BitWriter().U16(3).U8(0).U16(0)
            .U8(0x01).U16(1).U16(1).Matrix(0, 0).AlphaTransform(256)
            .U8(0x0E).U16(1).U16(2).Matrix(20, 20).AlphaTransform(256)
            .U8(0);
        var bytes = button.ToArray();
        var actionOffsetAt = 3;
        var actions = new BitWriter().U16(0).U8(0x04).U8(0x00).U8(0x07).U8(0x00).ToArray();
        var withActions = bytes.Concat(actions).ToArray();
        var offset = bytes.Length - actionOffsetAt;
        withActions[actionOffsetAt] = (byte)offset;
        withActions[actionOffsetAt + 1] = (byte)(offset >> 8);
        var sound = new BitWriter().U16(3).U16(0).U16(7).U8(0).U16(0).U16(0).ToArray();

        var library = Load(new SwfBuilder()
            .Add(SwfTagCode.DefineShape, SwfBuilder.SolidRectangleShape(1, 200, 200, 1, 1, 1))
            .Add(SwfTagCode.DefineButton2, withActions)
            .Add(SwfTagCode.DefineButtonSound, sound));

        var definition = library.Buttons[3];
        Assert.Equal(2, definition.Records.Count);
        Assert.True(definition.Records[0].Up);
        Assert.False(definition.Records[0].Hit);
        Assert.True(definition.Records[1].Over && definition.Records[1].Down && definition.Records[1].Hit);
        Assert.Equal(ExpectedConditions, definition.ActionConditions);
        Assert.Equal(7, definition.Sounds["idleToOverUp"].SoundId);
    }

    [Fact]
    public void StaticText_IsDecodedThroughTheFontCodeTable()
    {
        var font = new BitWriter().U16(4).U8(0x04).U8(0).U8(4).Raw("Sans"u8.ToArray()).U16(2)
            .U16(6).U16(8).U16(10)
            .U8(0x10).U8(0x00).U8(0x10).U8(0x00)
            .U16('H').U16('i');
        var text = new BitWriter().U16(6).Rect(0, 400, 0, 400).Matrix(0, 0).U8(1).U8(8)
            .U8(0x8F).U16(4).U8(10).U8(20).U8(30).U16(40).U16(240).U16(240).U8(2)
            .Bits(0, 1).Bits(100, 8).Bits(1, 1).Bits(110, 8).Align().U8(0);

        var library = Load(new SwfBuilder().Add(SwfTagCode.DefineFont3, font).Add(SwfTagCode.DefineText, text));

        var run = Assert.Single(library.Texts[6].Runs);
        Assert.Equal("Hi", run.Text);
        Assert.Equal(240, run.Height);
        Assert.Equal(new SwfColor(10, 20, 30, 255), run.Color);
        Assert.Equal(ExpectedAdvances, run.Glyphs.Select(g => g.Advance));
    }

    [Fact]
    public void Lossless2_IsUnpremultipliedIntoPng()
    {
        var pixels = new byte[] { 128, 64, 0, 0, 255, 0, 255, 0 };
        var body = new BitWriter().U16(11).U8(5).U16(2).U16(1).Raw(SwfBuilder.Deflate(pixels));
        var library = Load(new SwfBuilder().Add(SwfTagCode.DefineBitsLossless2, body));

        var exported = BitmapExporter.Export(library, library.Bitmaps[11]);
        var (width, height, channels, data) = PngReader.Read(exported.Content);

        Assert.Equal("png", exported.Extension);
        Assert.True(exported.Alpha);
        Assert.Equal((2, 1, 4), (width, height, channels));
        Assert.Equal(new byte[] { 128, 0, 0, 128, 0, 255, 0, 255 }, data);
    }

    [Fact]
    public void ColourMappedLossless_HonoursRowPadding()
    {
        var palette = new byte[] { 255, 0, 0, 0, 0, 255 };
        var indices = new byte[] { 0, 1, 0, 0, 1, 1, 0, 0 };
        var body = new BitWriter().U16(12).U8(3).U16(3).U16(2).U8(1).Raw(SwfBuilder.Deflate(palette.Concat(indices).ToArray()));
        var library = Load(new SwfBuilder().Add(SwfTagCode.DefineBitsLossless, body));

        var exported = BitmapExporter.Export(library, library.Bitmaps[12]);
        var (width, height, channels, data) = PngReader.Read(exported.Content);

        Assert.False(exported.Alpha);
        Assert.Equal((3, 2, 3), (width, height, channels));
        Assert.Equal(new byte[] { 255, 0, 0, 0, 0, 255, 255, 0, 0, 0, 0, 255, 0, 0, 255, 255, 0, 0 }, data);
    }

    [Fact]
    public void Jpeg3_AlphaPlaneIsMergedIntoPng()
    {
        var jpeg = SwfBuilder.GrayJpeg();
        var alpha = Enumerable.Range(0, 64).Select(i => (byte)(i < 32 ? 255 : 0)).ToArray();
        var body = new BitWriter().U16(13).U32((uint)jpeg.Length).Raw(jpeg).Raw(SwfBuilder.Deflate(alpha));
        var library = Load(new SwfBuilder().Add(SwfTagCode.DefineBitsJpeg3, body));

        var exported = BitmapExporter.Export(library, library.Bitmaps[13]);
        var (width, height, channels, data) = PngReader.Read(exported.Content);

        Assert.Equal((8, 8, 4), (width, height, channels));
        Assert.Equal(new byte[] { 128, 128, 128, 255 }, data[..4]);
        Assert.Equal(new byte[] { 0, 0, 0, 0 }, data[(32 * 4)..(33 * 4)]);
    }

    [Fact]
    public void Jpeg2_LosesTheErroneousLeadingMarkers()
    {
        var jpeg = SwfBuilder.GrayJpeg();
        var body = new BitWriter().U16(14).Raw([0xFF, 0xD9, 0xFF, 0xD8]).Raw(jpeg);
        var library = Load(new SwfBuilder().Add(SwfTagCode.DefineBitsJpeg2, body));

        var exported = BitmapExporter.Export(library, library.Bitmaps[14]);

        Assert.Equal("jpg", exported.Extension);
        Assert.Equal((8, 8), (exported.Width, exported.Height));
        Assert.Equal(jpeg, exported.Content);
    }

    [Fact]
    public void JpegDecoder_DecodesBaselineGray()
    {
        var image = JpegDecoder.Decode(SwfBuilder.GrayJpeg());

        Assert.Equal((8, 8), (image.Width, image.Height));
        Assert.All(image.Pixels, value => Assert.Equal(128, value));
    }

    [Fact]
    public void PngWriter_RoundTripsRgb()
    {
        var pixels = Enumerable.Range(0, 5 * 4 * 3).Select(i => (byte)(i * 7)).ToArray();

        var (width, height, channels, data) = PngReader.Read(PngWriter.Encode(5, 4, pixels, false));

        Assert.Equal((5, 4, 3), (width, height, channels));
        Assert.Equal(pixels, data);
    }

    [Fact]
    public void Extract_WritesBitmapsSoundsAndLibrary()
    {
        var mp3Frames = new byte[] { 0xFF, 0xFB, 0x10, 0x00, 1, 2, 3 };
        var sound = new BitWriter().U16(20).Bits(2, 4).Bits(1, 2).Bits(1, 1).Bits(0, 1).U32(1152).U16(0).Raw(mp3Frames);
        var place = new BitWriter().U8(0x06).U16(1).U16(1).Matrix(0, 0);
        var swf = new SwfBuilder()
            .Add(SwfTagCode.DefineShape, SwfBuilder.SolidRectangleShape(1, 200, 200, 1, 2, 3))
            .Add(SwfTagCode.DefineBitsJpeg2, new BitWriter().U16(2).Raw(SwfBuilder.GrayJpeg()))
            .Add(SwfTagCode.DefineSound, sound)
            .Add(SwfTagCode.ExportAssets, new BitWriter().U16(1).U16(20).Text("bang"))
            .Add(SwfTagCode.PlaceObject2, place)
            .Add(SwfTagCode.StartSound, new BitWriter().U16(20).U8(0x04).U16(3))
            .Add(SwfTagCode.ShowFrame, [])
            .Build(true);
        var directory = Path.Combine(Path.GetTempPath(), "gacha-art-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            var summary = ArtExtractor.Extract(swf, directory);

            Assert.Equal((1, 1, 1), (summary.Bitmaps, summary.Sounds, summary.Shapes));
            Assert.Equal(mp3Frames, File.ReadAllBytes(Path.Combine(directory, "sounds", "20.mp3")));
            Assert.True(File.Exists(Path.Combine(directory, "bitmaps", "2.jpg")));
            using var json = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(directory, ArtExtractor.LibraryFile)));
            var root = json.RootElement;
            Assert.Equal(20, root.GetProperty("exports").GetProperty("bang").GetInt32());
            Assert.Equal(11025, root.GetProperty("sounds").GetProperty("20").GetProperty("rate").GetInt32());
            Assert.Equal("shape", root.GetProperty("characters").GetProperty("1").GetProperty("type").GetString());
            var frame = root.GetProperty("root").GetProperty("frames")[0];
            Assert.Equal(1, frame.GetProperty("ops")[0].GetProperty("id").GetInt32());
            Assert.Equal(3, frame.GetProperty("sounds")[0].GetProperty("loops").GetInt32());
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }

    private static SwfLibrary Load(SwfBuilder builder) => SwfLibrary.Load(SwfFile.Parse(builder.Build(false)));
}
