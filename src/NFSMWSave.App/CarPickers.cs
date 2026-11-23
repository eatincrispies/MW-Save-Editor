using NFSMWSave.Core;

namespace NFSMWSave.App;

internal sealed class PartPicker
{
    private const string CurrentGroup = "Current";
    private readonly IReadOnlyList<string>? _order;
    private List<PartChoice> _choices = [];
    private bool _loading;

    public PartPicker(int width, IReadOnlyList<string>? order = null)
    {
        _order = order;
        Type = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = Ui.S(width), MaxDropDownItems = 16, Margin = new Padding(Ui.S(2)) };
        Item = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = Ui.S(width), DisplayMember = nameof(PartChoice.Name), MaxDropDownItems = 18, Margin = new Padding(Ui.S(2)) };
        Type.SelectedIndexChanged += (_, _) =>
        {
            if (_loading)
            {
                return;
            }
            var name = (Item.SelectedItem as PartChoice)?.Name;
            var group = Type.SelectedItem as string;
            FillItems(_choices.FirstOrDefault(p => p.Group == group && p.Name == name));
            Raise();
        };
        Item.SelectedIndexChanged += (_, _) =>
        {
            if (!_loading)
            {
                Raise();
            }
        };
    }

    public event Action<ushort>? Picked;

    public ComboBox Type { get; }

    public ComboBox Item { get; }

    public ushort? Value => (Item.SelectedItem as PartChoice)?.Part;

    public void Load(IReadOnlyList<PartChoice> choices, ushort current)
    {
        _loading = true;
        _choices = choices.ToList();
        if (_choices.All(p => p.Part != current))
        {
            var existing = _choices.Select(p => p.Group).Distinct().ToList();
            var group = existing.Count == 1 ? existing[0] : CurrentGroup;
            _choices.Insert(0, new PartChoice(current, group, current == CarPaints.NoPart ? "None" : "Current part"));
        }
        var groups = _choices.Select(p => p.Group).Distinct().ToList();
        if (_order is not null)
        {
            groups = groups.OrderBy(g => _order.Contains(g) ? _order.ToList().IndexOf(g) : _order.Count).ToList();
        }
        Type.BeginUpdate();
        Type.Items.Clear();
        Type.Items.AddRange(groups.Cast<object>().ToArray());
        Type.EndUpdate();
        var installed = _choices.First(p => p.Part == current);
        Type.SelectedItem = installed.Group;
        Type.Enabled = groups.Count > 1;
        FillItems(installed);
        _loading = false;
    }

    public void Clear()
    {
        _loading = true;
        _choices = [];
        Type.Items.Clear();
        Item.Items.Clear();
        Type.Enabled = false;
        Item.Enabled = false;
        _loading = false;
    }

    private void FillItems(PartChoice? select)
    {
        var wasLoading = _loading;
        _loading = true;
        var group = Type.SelectedItem as string;
        var items = _choices.Where(p => p.Group == group).ToList();
        Item.BeginUpdate();
        Item.Items.Clear();
        Item.Items.AddRange(items.Cast<object>().ToArray());
        Item.EndUpdate();
        Item.Enabled = items.Count > 1;
        if (select is not null)
        {
            Item.SelectedItem = select;
        }
        else if (items.Count > 0)
        {
            Item.SelectedIndex = 0;
        }
        _loading = wasLoading;
    }

    private void Raise()
    {
        if (Value is { } part)
        {
            Picked?.Invoke(part);
        }
    }
}

internal sealed class SwatchPicker
{
    private readonly bool _optional;
    private IReadOnlyList<PaintSwatch> _swatches = [];
    private bool _loading;

    public SwatchPicker(int width, bool optional = false)
    {
        _optional = optional;
        Type = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = Ui.S(width), Margin = new Padding(Ui.S(2)) };
        Colour = SwatchCombo(width);
        Type.SelectedIndexChanged += (_, _) =>
        {
            if (_loading)
            {
                return;
            }
            FillColours(null);
            Raise();
        };
        Colour.SelectedIndexChanged += (_, _) =>
        {
            if (!_loading)
            {
                Raise();
            }
        };
    }

    public const string DefaultType = "Default";

    public event Action<ushort>? Picked;

    public ComboBox Type { get; }

    public ComboBox Colour { get; }

    public ushort Value => Colour.SelectedItem is PaintSwatch swatch ? swatch.Part : CarPaints.NoPart;

    public static ComboBox SwatchCombo(int width)
    {
        var combo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, DrawMode = DrawMode.OwnerDrawFixed, ItemHeight = Ui.S(18), Width = Ui.S(width), MaxDropDownItems = 14, Margin = new Padding(Ui.S(2)) };
        combo.DrawItem += (_, e) => Ui.DrawSwatch(e, combo.Items);
        return combo;
    }

    public void Load(IReadOnlyList<PaintSwatch> swatches, ushort current)
    {
        _loading = true;
        _swatches = swatches;
        var types = swatches.Select(s => s.Finish).Distinct().ToList();
        if (_optional)
        {
            types.Insert(0, DefaultType);
        }
        Type.BeginUpdate();
        Type.Items.Clear();
        Type.Items.AddRange(types.Cast<object>().ToArray());
        Type.EndUpdate();
        var installed = swatches.FirstOrDefault(s => s.Part == current);
        Type.SelectedItem = installed?.Finish ?? types[0];
        FillColours(installed);
        if (installed is null)
        {
            Colour.SelectedIndex = -1;
        }
        _loading = false;
    }

    public void SetEnabled(bool enabled)
    {
        Type.Enabled = enabled;
        Colour.Enabled = enabled && Colour.Items.Count > 0;
    }

    private void FillColours(PaintSwatch? select)
    {
        var wasLoading = _loading;
        _loading = true;
        var finish = Type.SelectedItem as string;
        var items = _swatches.Where(s => s.Finish == finish).ToList();
        Colour.BeginUpdate();
        Colour.Items.Clear();
        Colour.Items.AddRange(items.Cast<object>().ToArray());
        Colour.EndUpdate();
        Colour.Enabled = Type.Enabled && items.Count > 0;
        if (select is not null)
        {
            Colour.SelectedItem = select;
        }
        else if (items.Count > 0)
        {
            Colour.SelectedIndex = 0;
        }
        _loading = wasLoading;
    }

    private void Raise() => Picked?.Invoke(Value);
}
