using NFSMWSave.Core;

namespace NFSMWSave.App;

internal sealed class AddCarDialog : Form
{
    private readonly ListView _list = Ui.List();
    private readonly Button _add;
    private readonly Label _status = new() { AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(Ui.S(2), Ui.S(7), Ui.S(4), Ui.S(2)) };
    private readonly int _free;

    public AddCarDialog(IReadOnlyList<CatalogVehicle> vehicles, int freeSlots, string? catalogLabel)
    {
        _free = freeSlots;
        Text = "Add Car";
        Font = new Font("Segoe UI", 9f);
        BackColor = Theme.Back;
        Load += (_, _) => Theme.Apply(this);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(Ui.S(430), Ui.S(500));

        _list.MultiSelect = true;
        _list.Dock = DockStyle.Fill;
        _list.Columns.Add("Vehicle");
        _list.Columns.Add("Status");
        Ui.FitColumns(_list, 265, 125);
        var groups = new Dictionary<VehicleGroup, ListViewGroup>();
        foreach (var group in Enum.GetValues<VehicleGroup>())
        {
            groups[group] = new ListViewGroup(group == VehicleGroup.Bosses ? "Blacklist Bosses" : group.ToString()) { HeaderAlignment = HorizontalAlignment.Left };
            _list.Groups.Add(groups[group]);
        }
        _list.BeginUpdate();
        foreach (var vehicle in vehicles)
        {
            var item = new ListViewItem(vehicle.Name, groups[vehicle.Group]) { Tag = vehicle, ToolTipText = vehicle.Id };
            item.SubItems.Add(SafetyText(vehicle.Safety));
            _list.Items.Add(item);
        }
        _list.EndUpdate();
        _list.ShowItemToolTips = true;
        _list.SelectedIndexChanged += (_, _) => RefreshState();
        _list.DoubleClick += (_, _) => Confirm();
        Ui.RightClick(_list, (menu, hit) =>
        {
            if (hit is not null)
            {
                var count = _list.SelectedItems.Count;
                Ui.Item(menu, count == 1 ? "Add Car" : $"Add {count} Cars", Confirm, count <= _free);
            }
        });

        _add = Ui.Action("Add", Confirm, 80);
        var close = Ui.Action("Close", () => DialogResult = DialogResult.Cancel, 80);
        CancelButton = close;
        AcceptButton = _add;

        var bottom = new TableLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, ColumnCount = 3, RowCount = 1, BackColor = Theme.Back, Padding = new Padding(Ui.S(4)) };
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        bottom.Controls.Add(_status, 0, 0);
        bottom.Controls.Add(_add, 1, 0);
        bottom.Controls.Add(close, 2, 0);

        var note = new Label
        {
            Dock = DockStyle.Top,
            Height = Ui.S(24),
            Padding = new Padding(Ui.S(6), Ui.S(5), 0, 0),
            Text = catalogLabel == "Modded" ? "Bosses and presets keep their look, the rest are stock (modded parts)." : "Bosses and presets keep their look, the rest are stock.",
        };
        Controls.Add(_list);
        Controls.Add(note);
        Controls.Add(bottom);
        RefreshState();
    }

    public IReadOnlyList<CatalogVehicle> Selected { get; private set; } = [];

    private static string SafetyText(VehicleSafety safety) => safety switch
    {
        VehicleSafety.Drivable => "Drivable",
        VehicleSafety.AiVehicle => "AI vehicle",
        _ => "May crash",
    };

    private List<CatalogVehicle> Picked() => _list.SelectedItems.Cast<ListViewItem>().Select(i => (CatalogVehicle)i.Tag!).ToList();

    private void RefreshState()
    {
        var count = _list.SelectedItems.Count;
        _add.Enabled = count > 0 && count <= _free;
        _status.Text = count > _free ? $"Only {_free} garage slots are free" : $"{count} selected, {_free} slots free";
    }

    private void Confirm()
    {
        var picked = Picked();
        if (picked.Count == 0 || picked.Count > _free)
        {
            return;
        }
        var risky = picked.Where(v => v.Safety != VehicleSafety.Drivable).ToList();
        if (risky.Count > 0)
        {
            var names = string.Join("\n", risky.Take(8).Select(v => $"{v.Name} ({SafetyText(v.Safety)})"));
            var answer = MessageBox.Show(this, $"These vehicles were not made for the player and can misbehave or crash the game:\n\n{names}\n\nAdd them anyway?", "Add Car", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (answer != DialogResult.Yes)
            {
                return;
            }
        }
        Selected = picked;
        DialogResult = DialogResult.OK;
    }
}
