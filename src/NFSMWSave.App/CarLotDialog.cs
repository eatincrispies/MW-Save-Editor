using NFSMWSave.Core;

namespace NFSMWSave.App;

internal sealed class CarLotDialog : Form
{
    private readonly ListView _list = Ui.List();
    private readonly Label _status = new() { AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(Ui.S(2), Ui.S(7), Ui.S(4), Ui.S(2)) };

    public CarLotDialog(IReadOnlyList<LotChoice> choices)
    {
        Text = "Car Lot";
        Font = new Font("Segoe UI", 9f);
        BackColor = Theme.Back;
        Load += (_, _) => Theme.Apply(this);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(Ui.S(430), Ui.S(500));

        _list.CheckBoxes = true;
        _list.MultiSelect = false;
        _list.Dock = DockStyle.Fill;
        _list.Columns.Add("Vehicle");
        _list.Columns.Add("Status");
        Ui.FitColumns(_list, 265, 125);
        var groups = new Dictionary<VehicleGroup, ListViewGroup>();
        foreach (var group in choices.Select(c => c.Vehicle.Group).Distinct().OrderBy(g => g))
        {
            groups[group] = new ListViewGroup(group == VehicleGroup.Stock ? "Hidden Cars" : group.ToString()) { HeaderAlignment = HorizontalAlignment.Left };
            _list.Groups.Add(groups[group]);
        }
        _list.BeginUpdate();
        foreach (var choice in choices)
        {
            var item = new ListViewItem(choice.Vehicle.Name, groups[choice.Vehicle.Group]) { Tag = choice.Vehicle, Checked = choice.InLot, ToolTipText = choice.Vehicle.Id };
            item.SubItems.Add(choice.Vehicle.Safety == VehicleSafety.Drivable ? "Drivable" : "AI vehicle");
            _list.Items.Add(item);
        }
        _list.EndUpdate();
        _list.ShowItemToolTips = true;
        _list.ItemChecked += (_, _) => RefreshState();
        Ui.RightClick(_list, BuildMenu);

        var save = Ui.Action("OK", Confirm, 80);
        var close = Ui.Action("Cancel", () => DialogResult = DialogResult.Cancel, 80);
        CancelButton = close;
        AcceptButton = save;

        var bottom = new TableLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, ColumnCount = 3, RowCount = 1, BackColor = Theme.Back, Padding = new Padding(Ui.S(4)) };
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        bottom.Controls.Add(_status, 0, 0);
        bottom.Controls.Add(save, 1, 0);
        bottom.Controls.Add(close, 2, 0);

        var note = new Label
        {
            Dock = DockStyle.Top,
            Height = Ui.S(42),
            Padding = new Padding(Ui.S(6), Ui.S(5), Ui.S(6), 0),
            Text = "Checked vehicles are sold in the game's Car Lot, stock with a random paint. Boss and preset cars are under Add Car.",
        };
        Controls.Add(_list);
        Controls.Add(note);
        Controls.Add(bottom);
        RefreshState();
    }

    public IReadOnlyList<CatalogVehicle> Wanted { get; private set; } = [];

    private void BuildMenu(ContextMenuStrip menu, ListViewItem? hit)
    {
        if (hit?.Group is { } group)
        {
            Ui.Item(menu, hit.Checked ? "Remove from Lot" : "Add to Lot", () => hit.Checked = !hit.Checked);
            Ui.Line(menu);
            Ui.Item(menu, $"Add All {group.Header}", () => SetChecked(group.Items.Cast<ListViewItem>(), true));
            Ui.Item(menu, $"Remove All {group.Header}", () => SetChecked(group.Items.Cast<ListViewItem>(), false));
            Ui.Line(menu);
        }
        Ui.Item(menu, "Remove Everything", () => SetChecked(_list.Items.Cast<ListViewItem>(), false), _list.CheckedItems.Count > 0);
    }

    private void SetChecked(IEnumerable<ListViewItem> items, bool on)
    {
        _list.BeginUpdate();
        foreach (var item in items.ToList())
        {
            item.Checked = on;
        }
        _list.EndUpdate();
    }

    private void RefreshState() => _status.Text = $"{_list.CheckedItems.Count} of {_list.Items.Count} in the lot";

    private void Confirm()
    {
        var picked = _list.CheckedItems.Cast<ListViewItem>().Select(i => (CatalogVehicle)i.Tag!).ToList();
        var risky = picked.Where(v => v.Safety != VehicleSafety.Drivable).ToList();
        if (risky.Count > 0)
        {
            var names = string.Join("\n", risky.Take(8).Select(v => v.Name));
            var more = risky.Count > 8 ? $"\nand {risky.Count - 8} more" : "";
            var answer = MessageBox.Show(this, $"These AI vehicles were not made for the player and can misbehave or crash the game:\n\n{names}{more}\n\nKeep them in the lot?", "Car Lot", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (answer != DialogResult.Yes)
            {
                return;
            }
        }
        Wanted = picked;
        DialogResult = DialogResult.OK;
    }
}
