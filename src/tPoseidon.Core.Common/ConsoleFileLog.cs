using System.Text;

namespace tPoseidon.Core.Common;

public static class ConsoleFileLog
{
    private const int FileBufferBytes = 5376;
    private static readonly object Gate = new();

    public static void EnableIfConfigured()
    {
        var path = Environment.GetEnvironmentVariable("MT_LOG_FILE");
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        Enable(path.Trim());
    }

    public static void Enable(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var stream = new FileStream(filePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite, FileBufferBytes);
        var fileWriter = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true };

        Console.SetOut(new TeeTextWriter(Console.Out, fileWriter));
        Console.SetError(new TeeTextWriter(Console.Error, fileWriter));
    }

    private sealed class TeeTextWriter : TextWriter
    {
        private readonly TextWriter _console;
        private readonly TextWriter _file;

        public TeeTextWriter(TextWriter console, TextWriter file)
        {
            _console = console;
            _file = file;
        }

        public override Encoding Encoding => _console.Encoding;

        public override void Write(char value)
        {
            lock (Gate)
            {
                _console.Write(value);
                _file.Write(value);
            }
        }

        public override void Write(string? value)
        {
            lock (Gate)
            {
                _console.Write(value);
                _file.Write(value);
            }
        }

        public override void WriteLine(string? value)
        {
            lock (Gate)
            {
                _console.WriteLine(value);
                _file.WriteLine(value);
            }
        }

        public override void Flush()
        {
            lock (Gate)
            {
                _console.Flush();
                _file.Flush();
            }
        }
    }
}
