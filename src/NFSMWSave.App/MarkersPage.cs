using NFSMWSave.Core;

namespace NFSMWSave.App;

internal sealed class MarkersPage : EditorPage
{
    private static readonly (string Title, int First, int Count)[] Sections = [("Police", 0, 3), ("Performance", 3, 7), ("Appearance", 10, 9)];

    private readonly Dictionary<int, NumericUpDown> _counts = [];
    private readonly Label _slots = Ui.Text("");
    private readonly ProgressBar _used = new() { Width = Ui.S(240), Height = Ui.S(16), Margin = new Padding(Ui.S(2), Ui.S(6), Ui.S(8), Ui.S(4)) };

    public MarkersPage()
    {
        var columns = Sections.Select(s => (Control)Ui.Section(s.Title, Build(s.First, s.Count), 250)).ToArray();
        Controls.Add(Ui.Page(Ui.Column(Ui.Across(_used, _slots), Ui.Across(columns))));
    }

    public override string Title => "Markers";
    public override string IconName => "tab_markers";

    private TableLayoutPanel Build(int first, int count)
    {
        var table = Ui.Table(3);
        foreach (var type in MarkerInventory.Types.Skip(first).Take(count))
        {
            var icon = Ui.Picture(24, 24);
            icon.Image = Assets.Icon($"marker_{type.Id}");
            var number = Ui.Number(0, NFSMWSave.Core.Layout.MarkerCount, 60);
            _counts[type.Id] = number;
            var row = table.RowCount++;
            table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            table.Controls.Add(icon, 0, row);
            table.Controls.Add(Ui.Text(type.Name), 1, row);
            table.Controls.Add(number, 2, row);
            OnNumber(number, v =>
            {
                File!.Markers.SetCount(type.Id, (int)v);
                Bound(RefreshLimits);
            });
        }
        table.ColumnStyles[1] = new ColumnStyle(SizeType.Absolute, Ui.S(140));
        return table;
    }

    protected override void Populate() => RefreshLimits();

    private void RefreshLimits()
    {
        var markers = File!.Markers;
        var free = markers.Capacity - markers.UsedSlots;
        foreach (var (id, control) in _counts)
        {
            var have = markers.Count(id);
            control.Maximum = have + free;
            control.Value = have;
        }
        _used.Maximum = markers.Capacity;
        _used.Value = Math.Min(markers.UsedSlots, markers.Capacity);
        _slots.Text = $"{markers.UsedSlots} of {markers.Capacity} marker slots used";
    }
}
