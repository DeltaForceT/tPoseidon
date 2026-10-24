namespace tPoseidon.Gacha.Art.Images;

public sealed record RgbImage(int Width, int Height, byte[] Pixels);

public sealed class JpegFormatException : Exception
{
    public JpegFormatException()
    {
    }

    public JpegFormatException(string message)
        : base(message)
    {
    }

    public JpegFormatException(string message, Exception inner)
        : base(message, inner)
    {
    }
}

public static class JpegDecoder
{
    private const int BlockSize = 64;

    private static readonly int[] ZigZag =
    [
        0, 1, 8, 16, 9, 2, 3, 10,
        17, 24, 32, 25, 18, 11, 4, 5,
        12, 19, 26, 33, 40, 48, 41, 34,
        27, 20, 13, 6, 7, 14, 21, 28,
        35, 42, 49, 56, 57, 50, 43, 36,
        29, 22, 15, 23, 30, 37, 44, 51,
        58, 59, 52, 45, 38, 31, 39, 46,
        53, 60, 61, 54, 47, 55, 62, 63,
    ];

    private static readonly double[] Cosines = BuildCosines();

    public static (int Width, int Height) ReadSize(ReadOnlySpan<byte> data)
    {
        var position = 0;
        while (position + 4 <= data.Length)
        {
            if (data[position] != 0xFF)
            {
                position++;
                continue;
            }
            var marker = data[position + 1];
            if (marker is 0xD8 or 0xD9 or 0x01 || marker is >= 0xD0 and <= 0xD7 || marker == 0xFF)
            {
                position += marker == 0xFF ? 1 : 2;
                continue;
            }
            var length = (data[position + 2] << 8) | data[position + 3];
            if (marker is 0xC0 or 0xC1 or 0xC2 or 0xC3 && position + 9 <= data.Length)
            {
                var height = (data[position + 5] << 8) | data[position + 6];
                var width = (data[position + 7] << 8) | data[position + 8];
                return (width, height);
            }
            if (marker == 0xDA) break;
            position += 2 + length;
        }
        throw new JpegFormatException("no frame header");
    }

    public static RgbImage Decode(ReadOnlySpan<byte> data)
    {
        var state = new DecoderState();
        var position = 0;
        var decodedAnyScan = false;
        while (position + 2 <= data.Length)
        {
            if (data[position] != 0xFF)
            {
                position++;
                continue;
            }
            var marker = data[position + 1];
            if (marker == 0xFF)
            {
                position++;
                continue;
            }
            if (marker is 0xD8 or 0x01 || marker is >= 0xD0 and <= 0xD7)
            {
                position += 2;
                continue;
            }
            if (marker == 0xD9)
            {
                if (decodedAnyScan) break;
                position += 2;
                continue;
            }
            if (position + 4 > data.Length) throw new JpegFormatException("truncated marker segment");
            var length = (data[position + 2] << 8) | data[position + 3];
            if (length < 2 || position + 2 + length > data.Length) throw new JpegFormatException($"bad segment length {length} for marker 0x{marker:X2}");
            var segment = data.Slice(position + 4, length - 2);
            position += 2 + length;
            switch (marker)
            {
                case 0xDB:
                    state.ReadQuantization(segment);
                    break;
                case 0xC4:
                    state.ReadHuffman(segment);
                    break;
                case 0xC0:
                case 0xC1:
                    state.ReadFrame(segment);
                    break;
                case 0xC2:
                case 0xC3:
                case 0xC5:
                case 0xC6:
                case 0xC7:
                case 0xC9:
                case 0xCA:
                case 0xCB:
                case 0xCD:
                case 0xCE:
                case 0xCF:
                    throw new JpegFormatException($"unsupported JPEG process (marker 0x{marker:X2}); only baseline Huffman is supported");
                case 0xDD:
                    state.RestartInterval = (segment[0] << 8) | segment[1];
                    break;
                case 0xEE:
                    if (segment.Length >= 12 && segment[0] == (byte)'A' && segment[1] == (byte)'d' && segment[2] == (byte)'o' && segment[3] == (byte)'b' && segment[4] == (byte)'e')
                    {
                        state.AdobeTransform = segment[11];
                    }
                    break;
                case 0xDA:
                    position = state.DecodeScan(data, segment, position);
                    decodedAnyScan = true;
                    break;
                default:
                    break;
            }
        }
        if (!decodedAnyScan) throw new JpegFormatException("no scan data");
        return state.Output();
    }

    private static double[] BuildCosines()
    {
        var table = new double[BlockSize];
        for (var x = 0; x < 8; x++)
        {
            for (var u = 0; u < 8; u++)
            {
                var c = u == 0 ? 1 / Math.Sqrt(2) : 1.0;
                table[(x * 8) + u] = c * Math.Cos(((2 * x) + 1) * u * Math.PI / 16);
            }
        }
        return table;
    }

    private sealed class HuffmanTable
    {
        private readonly int[] _maxCode = new int[18];
        private readonly int[] _valuePointer = new int[17];
        private readonly int[] _minCode = new int[17];
        private readonly byte[] _values;

        public HuffmanTable(ReadOnlySpan<byte> counts, ReadOnlySpan<byte> values)
        {
            _values = values.ToArray();
            var code = 0;
            var k = 0;
            for (var length = 1; length <= 16; length++)
            {
                var count = counts[length - 1];
                _valuePointer[length] = k;
                _minCode[length] = code;
                code += count;
                k += count;
                _maxCode[length] = count == 0 ? -1 : code - 1;
                code <<= 1;
            }
            _maxCode[17] = int.MaxValue;
        }

        public int Decode(ref BitReader bits)
        {
            var code = bits.Bit();
            for (var length = 1; length <= 16; length++)
            {
                if (code <= _maxCode[length]) return _values[_valuePointer[length] + code - _minCode[length]];
                code = (code << 1) | bits.Bit();
            }
            throw new JpegFormatException("bad Huffman code");
        }
    }

    private sealed class Component
    {
        public int Id { get; init; }

        public int Horizontal { get; init; }

        public int Vertical { get; init; }

        public int QuantTable { get; init; }

        public int BlocksPerLine { get; set; }

        public int BlocksPerColumn { get; set; }

        public byte[] Plane { get; set; } = [];

        public int Stride => BlocksPerLine * 8;

        public HuffmanTable? Dc { get; set; }

        public HuffmanTable? Ac { get; set; }

        public int Predictor { get; set; }
    }

    private ref struct BitReader
    {
        private readonly ReadOnlySpan<byte> _data;
        private int _position;
        private int _buffer;
        private int _count;

        public BitReader(ReadOnlySpan<byte> data, int position)
        {
            _data = data;
            _position = position;
            _buffer = 0;
            _count = 0;
        }

        public readonly int Position => _position;

        public int Bit()
        {
            if (_count == 0)
            {
                _buffer = NextByte();
                _count = 8;
            }
            _count--;
            return (_buffer >> _count) & 1;
        }

        public int Receive(int length)
        {
            var value = 0;
            for (var i = 0; i < length; i++) value = (value << 1) | Bit();
            return value;
        }

        public void Restart()
        {
            _count = 0;
            while (_position + 1 < _data.Length)
            {
                if (_data[_position] == 0xFF && _data[_position + 1] >= 0xD0 && _data[_position + 1] <= 0xD7)
                {
                    _position += 2;
                    return;
                }
                if (_data[_position] == 0xFF && _data[_position + 1] != 0x00 && _data[_position + 1] != 0xFF) return;
                _position++;
            }
        }

        public int SkipToMarker()
        {
            _count = 0;
            while (_position + 1 < _data.Length)
            {
                if (_data[_position] == 0xFF && _data[_position + 1] != 0x00 && !(_data[_position + 1] >= 0xD0 && _data[_position + 1] <= 0xD7)) return _position;
                _position++;
            }
            return _data.Length;
        }

        private int NextByte()
        {
            if (_position >= _data.Length) return 0;
            var value = _data[_position];
            if (value != 0xFF)
            {
                _position++;
                return value;
            }
            if (_position + 1 < _data.Length && _data[_position + 1] == 0x00)
            {
                _position += 2;
                return 0xFF;
            }
            return 0;
        }
    }

    private sealed class DecoderState
    {
        private readonly int[][] _quantization = new int[4][];
        private readonly HuffmanTable?[] _dcTables = new HuffmanTable?[4];
        private readonly HuffmanTable?[] _acTables = new HuffmanTable?[4];
        private readonly List<Component> _components = new();
        private int _width;
        private int _height;
        private int _maxHorizontal;
        private int _maxVertical;
        private int _mcusPerLine;
        private int _mcusPerColumn;

        public int RestartInterval { get; set; }

        public int AdobeTransform { get; set; } = -1;

        public void ReadQuantization(ReadOnlySpan<byte> segment)
        {
            var offset = 0;
            while (offset < segment.Length)
            {
                var precision = segment[offset] >> 4;
                var id = segment[offset] & 0x0F;
                offset++;
                if (id > 3) throw new JpegFormatException($"quantization table id {id}");
                var table = new int[BlockSize];
                for (var i = 0; i < BlockSize; i++)
                {
                    if (precision == 0)
                    {
                        table[i] = segment[offset++];
                    }
                    else
                    {
                        table[i] = (segment[offset] << 8) | segment[offset + 1];
                        offset += 2;
                    }
                }
                _quantization[id] = table;
            }
        }

        public void ReadHuffman(ReadOnlySpan<byte> segment)
        {
            var offset = 0;
            while (offset < segment.Length)
            {
                var tableClass = segment[offset] >> 4;
                var id = segment[offset] & 0x0F;
                if (id > 3) throw new JpegFormatException($"Huffman table id {id}");
                var counts = segment.Slice(offset + 1, 16);
                var total = 0;
                foreach (var count in counts) total += count;
                var values = segment.Slice(offset + 17, total);
                var table = new HuffmanTable(counts, values);
                if (tableClass == 0) _dcTables[id] = table;
                else _acTables[id] = table;
                offset += 17 + total;
            }
        }

        public void ReadFrame(ReadOnlySpan<byte> segment)
        {
            if (segment[0] != 8) throw new JpegFormatException($"sample precision {segment[0]} is not supported");
            _height = (segment[1] << 8) | segment[2];
            _width = (segment[3] << 8) | segment[4];
            if (_width == 0 || _height == 0) throw new JpegFormatException("zero image size");
            var count = segment[5];
            _components.Clear();
            for (var i = 0; i < count; i++)
            {
                var at = 6 + (i * 3);
                _components.Add(new Component
                {
                    Id = segment[at],
                    Horizontal = segment[at + 1] >> 4,
                    Vertical = segment[at + 1] & 0x0F,
                    QuantTable = segment[at + 2],
                });
            }
            _maxHorizontal = _components.Max(c => c.Horizontal);
            _maxVertical = _components.Max(c => c.Vertical);
            _mcusPerLine = (_width + (8 * _maxHorizontal) - 1) / (8 * _maxHorizontal);
            _mcusPerColumn = (_height + (8 * _maxVertical) - 1) / (8 * _maxVertical);
            foreach (var component in _components)
            {
                component.BlocksPerLine = _mcusPerLine * component.Horizontal;
                component.BlocksPerColumn = _mcusPerColumn * component.Vertical;
                component.Plane = new byte[component.BlocksPerLine * component.BlocksPerColumn * BlockSize];
            }
        }

        public int DecodeScan(ReadOnlySpan<byte> data, ReadOnlySpan<byte> header, int position)
        {
            if (_components.Count == 0) throw new JpegFormatException("scan before frame header");
            var count = header[0];
            var scan = new List<Component>(count);
            for (var i = 0; i < count; i++)
            {
                var id = header[1 + (i * 2)];
                var tables = header[2 + (i * 2)];
                var component = _components.FirstOrDefault(c => c.Id == id) ?? throw new JpegFormatException($"scan names unknown component {id}");
                component.Dc = _dcTables[tables >> 4] ?? throw new JpegFormatException("missing DC table");
                component.Ac = _acTables[tables & 0x0F] ?? throw new JpegFormatException("missing AC table");
                component.Predictor = 0;
                scan.Add(component);
            }
            var bits = new BitReader(data, position);
            var coefficients = new int[BlockSize];
            var restartsLeft = RestartInterval;
            if (scan.Count == 1)
            {
                var component = scan[0];
                var columns = ((((_width * component.Horizontal) + _maxHorizontal - 1) / _maxHorizontal) + 7) / 8;
                var rows = ((((_height * component.Vertical) + _maxVertical - 1) / _maxVertical) + 7) / 8;
                for (var row = 0; row < rows; row++)
                {
                    for (var column = 0; column < columns; column++)
                    {
                        if (RestartInterval > 0 && restartsLeft == 0)
                        {
                            bits.Restart();
                            component.Predictor = 0;
                            restartsLeft = RestartInterval;
                        }
                        DecodeBlock(ref bits, component, coefficients, row, column);
                        restartsLeft--;
                    }
                }
            }
            else
            {
                for (var mcuRow = 0; mcuRow < _mcusPerColumn; mcuRow++)
                {
                    for (var mcuColumn = 0; mcuColumn < _mcusPerLine; mcuColumn++)
                    {
                        if (RestartInterval > 0 && restartsLeft == 0)
                        {
                            bits.Restart();
                            foreach (var component in scan) component.Predictor = 0;
                            restartsLeft = RestartInterval;
                        }
                        foreach (var component in scan)
                        {
                            for (var v = 0; v < component.Vertical; v++)
                            {
                                for (var h = 0; h < component.Horizontal; h++)
                                {
                                    DecodeBlock(ref bits, component, coefficients, (mcuRow * component.Vertical) + v, (mcuColumn * component.Horizontal) + h);
                                }
                            }
                        }
                        restartsLeft--;
                    }
                }
            }
            return bits.SkipToMarker();
        }

        public RgbImage Output()
        {
            var pixels = new byte[_width * _height * 3];
            var planes = _components.Select(c => Upsample(c)).ToArray();
            var convert = _components.Count == 3 && AdobeTransform != 0;
            for (var i = 0; i < _width * _height; i++)
            {
                if (_components.Count == 1)
                {
                    var y = planes[0][i];
                    pixels[i * 3] = y;
                    pixels[(i * 3) + 1] = y;
                    pixels[(i * 3) + 2] = y;
                }
                else if (convert)
                {
                    double luma = planes[0][i];
                    var cb = planes[1][i] - 128.0;
                    var cr = planes[2][i] - 128.0;
                    pixels[i * 3] = Clamp(luma + (1.402 * cr));
                    pixels[(i * 3) + 1] = Clamp(luma - (0.344136 * cb) - (0.714136 * cr));
                    pixels[(i * 3) + 2] = Clamp(luma + (1.772 * cb));
                }
                else if (_components.Count == 3)
                {
                    pixels[i * 3] = planes[0][i];
                    pixels[(i * 3) + 1] = planes[1][i];
                    pixels[(i * 3) + 2] = planes[2][i];
                }
                else
                {
                    throw new JpegFormatException($"{_components.Count} colour components are not supported");
                }
            }
            return new RgbImage(_width, _height, pixels);
        }

        private byte[] Upsample(Component component)
        {
            var result = new byte[_width * _height];
            var scaleX = (double)component.Horizontal / _maxHorizontal;
            var scaleY = (double)component.Vertical / _maxVertical;
            var planeWidth = (int)Math.Ceiling(_width * scaleX);
            var planeHeight = (int)Math.Ceiling(_height * scaleY);
            var stride = component.Stride;
            var plane = component.Plane;
            for (var y = 0; y < _height; y++)
            {
                var sy = ((y + 0.5) * scaleY) - 0.5;
                if (sy < 0) sy = 0;
                var y0 = (int)sy;
                var y1 = Math.Min(y0 + 1, planeHeight - 1);
                var fy = sy - y0;
                for (var x = 0; x < _width; x++)
                {
                    var sx = ((x + 0.5) * scaleX) - 0.5;
                    if (sx < 0) sx = 0;
                    var x0 = (int)sx;
                    var x1 = Math.Min(x0 + 1, planeWidth - 1);
                    var fx = sx - x0;
                    var top = (plane[(y0 * stride) + x0] * (1 - fx)) + (plane[(y0 * stride) + x1] * fx);
                    var bottom = (plane[(y1 * stride) + x0] * (1 - fx)) + (plane[(y1 * stride) + x1] * fx);
                    result[(y * _width) + x] = Clamp((top * (1 - fy)) + (bottom * fy));
                }
            }
            return result;
        }

        private void DecodeBlock(ref BitReader bits, Component component, int[] coefficients, int blockRow, int blockColumn)
        {
            var quant = _quantization[component.QuantTable] ?? throw new JpegFormatException("missing quantization table");
            Array.Clear(coefficients);
            var t = component.Dc!.Decode(ref bits);
            var diff = t == 0 ? 0 : Extend(bits.Receive(t), t);
            component.Predictor += diff;
            coefficients[0] = component.Predictor * quant[0];
            var k = 1;
            while (k < BlockSize)
            {
                var rs = component.Ac!.Decode(ref bits);
                var run = rs >> 4;
                var size = rs & 0x0F;
                if (size == 0)
                {
                    if (run != 15) break;
                    k += 16;
                    continue;
                }
                k += run;
                if (k >= BlockSize) throw new JpegFormatException("coefficient index past the block");
                coefficients[ZigZag[k]] = Extend(bits.Receive(size), size) * quant[k];
                k++;
            }
            if (blockRow >= component.BlocksPerColumn || blockColumn >= component.BlocksPerLine) return;
            InverseTransform(coefficients, component.Plane, (blockRow * 8 * component.Stride) + (blockColumn * 8), component.Stride);
        }

        private static void InverseTransform(int[] coefficients, byte[] plane, int offset, int stride)
        {
            Span<double> temp = stackalloc double[BlockSize];
            for (var v = 0; v < 8; v++)
            {
                for (var x = 0; x < 8; x++)
                {
                    double sum = 0;
                    for (var u = 0; u < 8; u++) sum += Cosines[(x * 8) + u] * coefficients[(v * 8) + u];
                    temp[(v * 8) + x] = sum / 2;
                }
            }
            for (var x = 0; x < 8; x++)
            {
                for (var y = 0; y < 8; y++)
                {
                    double sum = 0;
                    for (var v = 0; v < 8; v++) sum += Cosines[(y * 8) + v] * temp[(v * 8) + x];
                    plane[offset + (y * stride) + x] = Clamp((sum / 2) + 128);
                }
            }
        }

        private static int Extend(int value, int length) => value < (1 << (length - 1)) ? value - (1 << length) + 1 : value;

        private static byte Clamp(double value)
        {
            var rounded = (int)Math.Round(value);
            return (byte)(rounded < 0 ? 0 : rounded > 255 ? 255 : rounded);
        }
    }
}
