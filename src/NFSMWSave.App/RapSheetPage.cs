using NFSMWSave.Core;

namespace NFSMWSave.App;

internal sealed class RapSheetPage : EditorPage
{
    private const int SectionWidth = 370;
    private const int LabelWidth = 200;

    private readonly List<(StatField Field, NumericUpDown Control)> _fields = [];
    private readonly Label _note = Ui.Text("");

    public RapSheetPage()
    {
        var root = Ui.Page(
            Ui.Section("Pursuit totals", Build(RapSheet.PursuitFields), SectionWidth),
            Ui.Section("Damage and deployments", Build(RapSheet.DamageFields), SectionWidth));
        _note.Dock = DockStyle.Top;
        Controls.Add(root);
        Controls.Add(_note);
    }

    public override string Title => "Rap Sheet";
    public override string IconName => "tab_rapsheet";

    protected override void Populate()
    {
        var available = File!.Stats.IsAvailable;
        _note.Text = available ? "" : "Rap Sheet stats are not available for this PS2 save version.";
        _note.Visible = !available;
        foreach (var (field, control) in _fields)
        {
            var value = File.Stats.Get(field);
            Ui.Show(control, field.IsTime ? value / RapSheet.TicksPerSecond : value);
            control.Enabled = available;
        }
    }

    private TableLayoutPanel Build(IEnumerable<StatField> fields)
    {
        var table = Ui.Fields(LabelWidth);
        foreach (var field in fields)
        {
            var max = field.IsTime ? int.MaxValue / RapSheet.TicksPerSecond : int.MaxValue;
            var control = Ui.Number(0, max, 130);
            Ui.Row(table, field.IsTime ? field.Name + " (seconds)" : field.Name, control);
            _fields.Add((field, control));
            OnNumber(control, v => File!.Stats.Set(field, field.IsTime ? (int)v * RapSheet.TicksPerSecond : (int)v));
        }
        return table;
    }
}
