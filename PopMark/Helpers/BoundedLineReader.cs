using System.Text;

namespace PopMark.Helpers;

/// <summary>
/// Reads newline-delimited input with a hard length limit. Bridge requests are
/// small, so an endless line is either a bug or an attempt to exhaust memory.
/// </summary>
internal sealed class BoundedLineReader
{
    private readonly TextReader _reader;
    private readonly int _maxLength;
    private readonly char[] _buffer = new char[4096];
    private int _offset;
    private int _count;

    public BoundedLineReader(TextReader reader, int maxLength)
    {
        _reader = reader;
        _maxLength = maxLength;
    }

    public async Task<LineReadResult> ReadLineAsync(CancellationToken cancellationToken)
    {
        var line = new StringBuilder();
        var tooLong = false;

        while (true)
        {
            if (_offset >= _count)
            {
                _count = await _reader.ReadAsync(_buffer.AsMemory(), cancellationToken);
                _offset = 0;

                if (_count == 0)
                {
                    return new LineReadResult(
                        tooLong || line.Length == 0 ? null : TrimCarriageReturn(line),
                        tooLong,
                        EndOfStream: true);
                }
            }

            while (_offset < _count)
            {
                var character = _buffer[_offset++];

                if (character == '\n')
                {
                    if (tooLong)
                        return new LineReadResult(null, TooLong: true, EndOfStream: false);

                    return new LineReadResult(TrimCarriageReturn(line), TooLong: false, EndOfStream: false);
                }

                if (tooLong)
                    continue;

                if (line.Length >= _maxLength)
                {
                    tooLong = true;
                    line.Clear();
                    continue;
                }

                line.Append(character);
            }
        }
    }

    private static string TrimCarriageReturn(StringBuilder line)
    {
        if (line.Length > 0 && line[^1] == '\r')
            line.Length--;

        return line.ToString();
    }

    internal readonly record struct LineReadResult(string? Line, bool TooLong, bool EndOfStream);
}
