namespace Presenter.Documents;

internal sealed class DeckHistory
{
    private readonly List<Entry> _undo = [];
    private readonly List<Entry> _redo = [];
    private Entry _current;
    private string? _editGroup;
    private long _lastEdit;

    public DeckHistory(Deck deck, Guid slideId)
    {
        _current = new(DeckFile.Snapshot(deck), slideId);
    }

    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;
    public string Current => _current.Content;

    public void Record(Deck deck, Guid slideId, string? editGroup = null)
    {
        string snapshot = DeckFile.Snapshot(deck);
        if (snapshot == _current.Content)
            return;

        long now = Environment.TickCount64;
        if (editGroup == null || _editGroup != editGroup || now - _lastEdit > 900)
        {
            if (_undo.Count == 120)
                _undo.RemoveAt(0);

            _undo.Add(_current);
        }

        _current = new(snapshot, slideId);
        _editGroup = editGroup;
        _lastEdit = now;
        _redo.Clear();
    }

    public void BreakGroup() => _editGroup = null;

    public void SelectSlide(Guid slideId)
    {
        _current = _current with { SlideId = slideId };
        BreakGroup();
    }

    public Entry? Undo() => Restore(_undo, _redo);
    public Entry? Redo() => Restore(_redo, _undo);

    private Entry? Restore(List<Entry> source, List<Entry> destination)
    {
        if (source.Count == 0)
            return null;

        destination.Add(_current);
        _current = source[^1];
        source.RemoveAt(source.Count - 1);
        BreakGroup();

        return _current;
    }

    internal sealed record Entry(string Content, Guid SlideId);
}
