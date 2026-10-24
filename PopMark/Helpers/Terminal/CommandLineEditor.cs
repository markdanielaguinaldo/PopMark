using System.Text;

namespace PopMark.Helpers.Terminal;

/// <summary>
/// Editable command line with a caret and history navigation. It is deliberately
/// independent of the console so the input rules can be unit tested. History is
/// only reachable while the buffer contains text; an empty buffer keeps the
/// existing playback shortcuts.
/// </summary>
internal sealed class CommandLineEditor
{
    private readonly StringBuilder _buffer = new();
    private readonly List<string> _history;
    private int _historyIndex;
    private string _historyDraft = string.Empty;

    public CommandLineEditor(List<string> history)
    {
        _history = history;
        _historyIndex = _history.Count;
    }

    public string Text => _buffer.ToString();

    public int Length => _buffer.Length;

    public bool IsEmpty => _buffer.Length == 0;

    public int Caret { get; private set; }

    public void Insert(char value)
    {
        _buffer.Insert(Caret, value);
        Caret++;
        ResetHistoryCursor();
    }

    public bool Backspace()
    {
        if (Caret <= 0)
            return false;

        _buffer.Remove(Caret - 1, 1);
        Caret--;
        ResetHistoryCursor();
        return true;
    }

    public bool Delete()
    {
        if (Caret >= _buffer.Length)
            return false;

        _buffer.Remove(Caret, 1);
        ResetHistoryCursor();
        return true;
    }

    public bool MoveLeft()
    {
        if (Caret <= 0)
            return false;

        Caret--;
        return true;
    }

    public bool MoveRight()
    {
        if (Caret >= _buffer.Length)
            return false;

        Caret++;
        return true;
    }

    public bool MoveHome()
    {
        if (Caret == 0)
            return false;

        Caret = 0;
        return true;
    }

    public bool MoveEnd()
    {
        if (Caret == _buffer.Length)
            return false;

        Caret = _buffer.Length;
        return true;
    }

    public bool HistoryPrevious()
    {
        if (_buffer.Length == 0 || _history.Count == 0)
            return false;

        if (_historyIndex >= _history.Count)
        {
            _historyDraft = _buffer.ToString();
            _historyIndex = _history.Count;
        }

        if (_historyIndex > 0)
            _historyIndex--;

        SetBuffer(_history[_historyIndex]);
        return true;
    }

    public bool HistoryNext()
    {
        if (_buffer.Length == 0 || _history.Count == 0)
            return false;

        if (_historyIndex < _history.Count - 1)
        {
            _historyIndex++;
            SetBuffer(_history[_historyIndex]);
            return true;
        }

        if (_historyIndex == _history.Count - 1)
        {
            _historyIndex = _history.Count;
            SetBuffer(_historyDraft);
            return true;
        }

        return false;
    }

    private void SetBuffer(string value)
    {
        _buffer.Clear();
        _buffer.Append(value);
        Caret = _buffer.Length;
    }

    private void ResetHistoryCursor()
    {
        _historyIndex = _history.Count;
        _historyDraft = _buffer.ToString();
    }
}
