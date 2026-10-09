using System.Collections.ObjectModel;

namespace LunaWm;

public sealed class JustTypeModel : ObservableModel
{
    private string _query = "";
    private int _selected = -1;

    public ObservableCollection<JustTypeRow> Rows { get; } = [];

    public string Query
    {
        get => _query;
        set
        {
            if (Set(ref _query, value ?? ""))
            {
                QueryChanged?.Invoke(_query);
            }
        }
    }

    public int Selected => _selected;

    public JustTypeRow? SelectedRow => _selected >= 0 && _selected < Rows.Count ? Rows[_selected] : null;

    public event Action<string>? QueryChanged;

    public event Action<JustTypeRow>? Chosen;

    public event Action? Dismissed;

    public void SetRows(IEnumerable<JustTypeRow> rows)
    {
        Rows.Clear();
        foreach (var row in rows)
        {
            Rows.Add(row);
        }

        _selected = -1;
        Move(1);
    }

    public void Move(int direction)
    {
        if (Rows.Count == 0)
        {
            return;
        }

        var next = _selected;
        for (var i = 0; i < Rows.Count; i++)
        {
            next = (next + direction + Rows.Count) % Rows.Count;
            if (Rows[next].IsResult)
            {
                break;
            }
        }

        if (SelectedRow is { } old)
        {
            old.Selected = false;
        }

        _selected = Rows[next].IsResult ? next : -1;
        if (SelectedRow is { } row)
        {
            row.Selected = true;
        }

        Raise(nameof(Selected));
    }

    public void ChooseSelected()
    {
        if (SelectedRow is { } row)
        {
            Chosen?.Invoke(row);
        }
    }

    public void Choose(JustTypeRow row) => Chosen?.Invoke(row);

    public void Dismiss() => Dismissed?.Invoke();
}
