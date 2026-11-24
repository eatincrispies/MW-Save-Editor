using NFSMWSave.Core;

namespace NFSMWSave.App;

internal sealed class CarsPage : EditorPage
{
    private const int SectionWidth = 276;
    private const int LabelWidth = 112;
    private static readonly string[] ImpoundNames = ["Not impounded", "Busted by Cross", "Strike limit reached", "Could not pay fine", "Released"];
    private static readonly int[] PerformanceOrder = [4, 3, 2, 6, 0, 1, 5];
    private static readonly Color LogoPlate = Color.FromArgb(24, 24, 28);

    private readonly ListView _list = Ui.List();
    private readonly ListViewGroup _careerGroup = new("Career Cars") { HeaderAlignment = HorizontalAlignment.Left };
    private readonly ListViewGroup _myCarsGroup = new("My Cars") { HeaderAlignment = HorizontalAlignment.Left };
    private readonly Button _add;
    private readonly Button _lot;
    private readonly Label _count = Ui.Text("");
    private readonly PictureBox _make = Ui.Picture(120, 56);
    private readonly PictureBox _model = Ui.Picture(240, 56);
    private readonly FlowLayoutPanel _heading = new() { AutoSize = true, WrapContents = false, BackColor = LogoPlate, Padding = new Padding(Ui.S(5)), Margin = new Padding(Ui.S(4)) };
    private readonly Label _title = new() { AutoSize = true, Font = new Font("Segoe UI", 11f, FontStyle.Bold), Margin = new Padding(Ui.S(4)) };
    private readonly Label _note = Ui.Note(240);
    private readonly DarkTabControl _sections = new() { Dock = DockStyle.Fill };
    private readonly TabPage _partsTab = Tab("Parts");
    private readonly TabPage _performanceTab = Tab("Performance");
    private readonly TabPage _tuningTab = Tab("Tuning");
    private readonly TabPage _visualsTab = Tab("Visuals");
    private readonly TabPage _policeTab = Tab("Police");
    private readonly CarPartsPanel _parts = new();
    private readonly CarTuningPanel _tuning = new();
    private readonly CarVisualsPanel _visuals = new();
    private readonly NumericUpDown _heat = Ui.Number(0, (decimal)CareerCar.MaxHeat, 120, 2, 0.1m);
    private readonly NumericUpDown _bounty = Ui.Number(0, CarStable.MaxBounty, 120);
    private readonly NumericUpDown _evaded = Ui.Number(0, ushort.MaxValue, 120);
    private readonly NumericUpDown _busted = Ui.Number(0, ushort.MaxValue, 120);
    private readonly ComboBox _impound = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = Ui.S(130) };
    private readonly NumericUpDown _strikes = Ui.Number(0, 10, 120);
    private readonly NumericUpDown _maxStrikes = Ui.Number(1, 10, 120);
    private readonly NumericUpDown _days = Ui.Number(0, 255, 120);
    private readonly ComboBox[] _levels = new ComboBox[CareerCar.PerformanceSlots];
    private readonly CheckBox[] _junkman = new CheckBox[CareerCar.PerformanceSlots];
    private readonly Panel _detail = new() { Dock = DockStyle.Fill, BackColor = Theme.Back, Padding = new Padding(Ui.S(4), 0, 0, 0) };
    private CareerCar? _car;
    private int? _selectSlot;

    public CarsPage()
    {
        _list.Columns.Add("Car");
        _list.Columns.Add("Bounty", -2, HorizontalAlignment.Right);
        _list.Columns.Add("Status");
        Ui.FitColumns(_list, 185, 72, 72);
        _list.Dock = DockStyle.Fill;
        _list.MultiSelect = true;
        _list.Groups.Add(_careerGroup);
        _list.Groups.Add(_myCarsGroup);
        _list.SelectedIndexChanged += (_, _) => SelectCar();
        Ui.RightClick(_list, BuildMenu);
        _impound.Items.AddRange(ImpoundNames);

        _add = Ui.Action("Add Car...", AddCar, 100);
        _lot = Ui.Action("Car Lot...", EditLot, 100);
        var addRow = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, WrapContents = true, BackColor = Theme.Back };
        addRow.Controls.AddRange([_add, _lot, _count]);
        var left = new Panel { Dock = DockStyle.Left, Width = Ui.S(340), BackColor = Theme.Back };
        left.Controls.Add(_list);
        left.Controls.Add(addRow);

        _make.BackColor = LogoPlate;
        _model.BackColor = LogoPlate;
        _heading.Controls.AddRange([_make, _model]);

        _partsTab.Controls.Add(_parts);
        _visualsTab.Controls.Add(_visuals);
        _performanceTab.Controls.Add(BuildPerformance());
        _policeTab.Controls.Add(BuildPolice());
        _tuningTab.Controls.Add(_tuning);
        _sections.TabPages.AddRange([_partsTab, _performanceTab, _tuningTab, _visualsTab, _policeTab]);
        _parts.Changed += () =>
        {
            if (_car is not null)
            {
                _visuals.ShowCar(_car, File!.Stable);
                _tuning.ShowCar(_car, File.Stable);
            }
            MarkEdited();
        };
        _visuals.Changed += MarkEdited;
        _tuning.Changed += MarkEdited;

        var top = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, BackColor = Theme.Back };
        top.Controls.AddRange([_heading, _title]);
        _detail.Controls.Add(_sections);
        _detail.Controls.Add(top);
        Controls.Add(_detail);
        Controls.Add(left);

        OnNumber(_heat, v => { if (_car is null) return; _car.Heat = (float)v; UpdateRow(); });
        OnNumber(_bounty, v =>
        {
            if (_car is null) return;
            _car.Bounty = (uint)v;
            UpdateRow();
            MarkBulkEdited();
        });
        OnNumber(_evaded, v => { if (_car is not null) _car.Evaded = (ushort)v; });
        OnNumber(_busted, v => { if (_car is not null) _car.Busted = (ushort)v; });
        OnNumber(_strikes, v => { if (_car is not null) _car.Strikes = (int)v; });
        OnNumber(_maxStrikes, v => { if (_car is not null) _car.MaxStrikes = (int)v; });
        OnNumber(_days, v => { if (_car is not null) _car.ImpoundDaysLeft = (int)v; });
        _impound.SelectedIndexChanged += (_, _) =>
        {
            if (!IsBinding && _car is not null && _impound.SelectedIndex >= 0)
            {
                _car.Impound = (ImpoundState)_impound.SelectedIndex;
                UpdateRow();
                MarkEdited();
            }
        };
    }

    public override string Title => "Cars";
    public override string IconName => "tab_cars";

    private static TabPage Tab(string title) => new(title) { BackColor = Theme.Back, UseVisualStyleBackColor = false, Padding = new Padding(Ui.S(4)) };

    private Control BuildPerformance()
    {
        var performance = Ui.Table(3);
        performance.RowCount = 1;
        performance.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        performance.Controls.Add(Ui.Text("Part"), 0, 0);
        performance.Controls.Add(Ui.Text("Level"), 1, 0);
        performance.Controls.Add(Ui.Text("Junkman"), 2, 0);
        foreach (var slot in PerformanceOrder)
        {
            _levels[slot] = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = Ui.S(112), Margin = new Padding(Ui.S(2)) };
            _junkman[slot] = Ui.Check("");
            _junkman[slot].Anchor = AnchorStyles.None;
            var row = performance.RowCount++;
            performance.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            performance.Controls.Add(Ui.Text(GameData.PerformanceNames[slot]), 0, row);
            performance.Controls.Add(_levels[slot], 1, row);
            performance.Controls.Add(_junkman[slot], 2, row);
            var index = slot;
            _levels[slot].SelectedIndexChanged += (_, _) =>
            {
                if (IsBinding || _car is null || _levels[index].SelectedIndex < 0)
                {
                    return;
                }
                var car = _car;
                car.SetLevel(index, _levels[index].SelectedIndex);
                Bound(() => ShowJunkman(index));
                _tuning.ShowCar(car, File!.Stable);
                MarkEdited();
            };
            OnCheck(_junkman[slot], v => { _car?.SetJunkman(index, v); });
        }
        Ui.Span(performance, Ui.Action("Max performance", MaxThisCar, 232));
        Ui.Span(performance, _note);
        return Ui.Page(Ui.Section("Performance", performance, SectionWidth));
    }

    private Control BuildPolice()
    {
        var police = Ui.Fields(LabelWidth);
        Ui.Row(police, "Heat", _heat);
        Ui.Row(police, "Bounty", _bounty);
        Ui.Row(police, "Pursuits evaded", _evaded);
        Ui.Row(police, "Times busted", _busted);
        Ui.Span(police, Ui.Action("Clear infractions", ClearInfractions, 232));

        var impound = Ui.Fields(LabelWidth);
        Ui.Row(impound, "State", _impound);
        Ui.Row(impound, "Strikes", _strikes);
        Ui.Row(impound, "Strike limit", _maxStrikes);
        Ui.Row(impound, "Days left", _days);
        Ui.Span(impound, Ui.Action("Release from impound", ReleaseThisCar, 232));
        return Ui.Page(Ui.Section("Police record", police, SectionWidth), Ui.Section("Impound", impound, SectionWidth));
    }

    protected override void Populate()
    {
        var keep = _selectSlot ?? _car?.Slot;
        _selectSlot = null;
        _car = null;
        _list.BeginUpdate();
        _list.Items.Clear();
        AddRows(File!.Stable.Cars, _careerGroup);
        AddRows(File.Stable.MyCars, _myCarsGroup);
        _list.EndUpdate();
        _detail.Visible = _list.Items.Count > 0;
        if (_list.Items.Count > 0)
        {
            var target = _list.Items.Cast<ListViewItem>().FirstOrDefault(i => ((CareerCar)i.Tag!).Slot == keep) ?? _list.Items[0];
            target.Selected = true;
            target.EnsureVisible();
            ShowCar((CareerCar)target.Tag!);
        }
        RefreshAddList();
    }

    private void AddRows(IEnumerable<CareerCar> cars, ListViewGroup group)
    {
        foreach (var car in cars)
        {
            var item = new ListViewItem(car.Name, group) { Tag = car };
            item.SubItems.Add("");
            item.SubItems.Add("");
            Fill(item, car);
            _list.Items.Add(item);
        }
    }

    private void RefreshAddList()
    {
        var problem = File!.Stable.AddProblem();
        _add.Enabled = problem is null;
        _count.Text = problem ?? $"{File.Stable.Cars.Count} of {File.Stable.MaxCars} career cars";
        _lot.Enabled = File.Stable.LotProblem() is null;
    }

    private List<CareerCar> SelectedCars() => _list.SelectedItems.Cast<ListViewItem>().Select(i => (CareerCar)i.Tag!).ToList();

    private void BuildMenu(ContextMenuStrip menu, ListViewItem? hit)
    {
        if (File is null)
        {
            return;
        }
        if (hit is null)
        {
            Ui.Item(menu, "Add Car...", AddCar, _add.Enabled);
            Ui.Item(menu, "Car Lot...", EditLot, _lot.Enabled);
            return;
        }
        var cars = SelectedCars();
        var one = cars.Count == 1 ? cars[0] : null;
        Ui.Item(menu, "Set as Current Car", () => MakeCurrent(one!), one is { IsCareer: true } && one.Handle != File.Career.CurrentCar && one.Impound == ImpoundState.None);
        Ui.Line(menu);
        Ui.Item(menu, "Edit Parts", () => _sections.SelectedTab = _partsTab, one is not null);
        Ui.Item(menu, "Edit Performance", () => _sections.SelectedTab = _performanceTab, one is not null);
        Ui.Item(menu, "Edit Tuning", () => _sections.SelectedTab = _tuningTab, one is not null);
        Ui.Item(menu, "Edit Visuals", () => _sections.SelectedTab = _visualsTab, one is not null);
        Ui.Line(menu);
        Ui.Item(menu, "Max Performance", () => ForSelected(c => c.MaxPerformance()));
        Ui.Item(menu, "Release from Impound", () => ForSelected(c => c.ReleaseFromImpound()), cars.Any(c => c.Impound != ImpoundState.None));
        Ui.Item(menu, "Clear Infractions", () => ForSelected(c => c.ClearInfractions()), cars.Any(c => c.UnservedInfractions > 0));
        Ui.Line(menu);
        Ui.Item(menu, cars.Count == 1 ? "Delete Car" : $"Delete {cars.Count} Cars", DeleteSelected, File.Map.CanAddCars);
    }

    private void MakeCurrent(CareerCar car)
    {
        Ui.Commit(_detail);
        File!.Career.CurrentCar = car.Handle;
        _selectSlot = car.Slot;
        MarkBulkEdited();
    }

    private void ForSelected(Action<CareerCar> action)
    {
        Ui.Commit(_detail);
        foreach (var car in SelectedCars())
        {
            action(car);
        }
        MarkBulkEdited();
    }

    private void DeleteSelected()
    {
        if (File is null)
        {
            return;
        }
        Ui.Commit(_detail);
        var cars = SelectedCars();
        var problem = File.Stable.DeleteProblem(cars);
        if (problem is not null)
        {
            MessageBox.Show(this, problem, "Delete Car", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        var what = cars.Count == 1 ? $"Delete {cars[0].Name} from this save?" : $"Delete these {cars.Count} cars from this save?\n\n{string.Join("\n", cars.Take(8).Select(c => c.Name))}{(cars.Count > 8 ? $"\nand {cars.Count - 8} more" : "")}";
        var current = cars.Any(c => c.Handle == File.Career.CurrentCar) ? "\n\nThis includes your current car, so you'll start in another car." : "";
        var career = cars.Any(c => c.IsCareer) ? "\n\nBounty and Rap Sheet totals stay on your career, like when you sell a car in the game. No cash is paid." : "";
        var answer = MessageBox.Show(this, $"{what}{current}{career}\n\nNothing is written until you save.", "Delete Car", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
        if (answer != DialogResult.Yes)
        {
            return;
        }
        File.Stable.DeleteCars(cars);
        _car = null;
        MarkBulkEdited();
    }

    private void EditLot()
    {
        if (File is null || File.Stable.LotProblem() is not null)
        {
            return;
        }
        Ui.Commit(_detail);
        using var dialog = new CarLotDialog(File.Stable.LotChoices());
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }
        try
        {
            if (File.Stable.SetLot(dialog.Wanted))
            {
                MarkBulkEdited();
            }
        }
        catch (InvalidOperationException error)
        {
            MessageBox.Show(this, error.Message, "Car Lot", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void AddCar()
    {
        if (File is null || File.Stable.AddProblem() is not null)
        {
            return;
        }
        Ui.Commit(_detail);
        using var dialog = new AddCarDialog(File.Stable.AddableVehicles(), File.Stable.FreeGarageSlots, File.Stable.CatalogLabel);
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }
        CareerCar? last = null;
        foreach (var vehicle in dialog.Selected)
        {
            if (File.Stable.AddProblem() is not null)
            {
                break;
            }
            last = File.Stable.AddVehicle(vehicle);
        }
        if (last is not null)
        {
            _selectSlot = last.Slot;
            MarkBulkEdited();
        }
    }

    private static void Fill(ListViewItem item, CareerCar car)
    {
        item.SubItems[1].Text = car.IsCareer ? car.Bounty.ToString("N0") : "";
        item.SubItems[2].Text = !car.IsCareer ? "" : car.Impound is ImpoundState.None ? "OK" : "Impounded";
    }

    private void UpdateRow()
    {
        var item = _list.Items.Cast<ListViewItem>().FirstOrDefault(i => ReferenceEquals(i.Tag, _car));
        if (item is not null && _car is not null)
        {
            Fill(item, _car);
        }
    }

    private void SelectCar()
    {
        if (IsBinding || _list.SelectedItems.Count == 0 || _list.SelectedItems[0].Tag is not CareerCar car || ReferenceEquals(car, _car))
        {
            return;
        }
        Ui.Commit(_detail);
        Bound(() => ShowCar(car));
    }

    private void ShowCar(CareerCar car)
    {
        _car = car;
        _make.Image = car.Info is null ? null : Assets.Get("make_" + car.Info.Make);
        _model.Image = car.Info is null ? null : Assets.Get("model_" + car.Info.Key.ToUpperInvariant());
        _heading.Visible = _make.Image is not null || _model.Image is not null;
        _title.Text = car.Name;
        _title.Visible = _model.Image is null;
        ShowPoliceTab(car.IsCareer);
        Ui.Show(_heat, (decimal)car.Heat);
        Ui.Show(_bounty, car.Bounty);
        Ui.Show(_evaded, car.Evaded);
        Ui.Show(_busted, car.Busted);
        _impound.SelectedIndex = (int)car.Impound < ImpoundNames.Length ? (int)car.Impound : -1;
        Ui.Show(_strikes, car.Strikes);
        Ui.Show(_maxStrikes, car.MaxStrikes);
        Ui.Show(_days, car.ImpoundDaysLeft);
        for (var i = 0; i < CareerCar.PerformanceSlots; i++)
        {
            _levels[i].BeginUpdate();
            _levels[i].Items.Clear();
            _levels[i].Items.AddRange(car.LevelNames(i).Cast<object>().ToArray());
            _levels[i].EndUpdate();
            _levels[i].SelectedIndex = Math.Min(car.GetLevel(i), _levels[i].Items.Count - 1);
            _levels[i].Enabled = car.HasCustomization && _levels[i].Items.Count > 1;
            ShowJunkman(i);
        }
        _parts.ShowCar(car, File!.Stable);
        _tuning.ShowCar(car, File.Stable);
        _visuals.ShowCar(car, File.Stable);
        var confirmed = car.LimitsConfirmed;
        _note.Text = confirmed ? "" : "Upgrade limits for this car are not confirmed, so they are kept low.";
        _note.Visible = !confirmed;
    }

    private void ShowPoliceTab(bool show)
    {
        var present = _sections.TabPages.Contains(_policeTab);
        if (show && !present)
        {
            _sections.TabPages.Add(_policeTab);
        }
        else if (!show && present)
        {
            _sections.TabPages.Remove(_policeTab);
        }
    }

    private void ShowJunkman(int slot)
    {
        if (_car is null)
        {
            return;
        }
        _junkman[slot].Checked = _car.GetJunkman(slot);
        _junkman[slot].Enabled = _car.CanSetJunkman(slot);
    }

    private void MaxThisCar()
    {
        if (_car is null) return;
        _car.MaxPerformance();
        MarkBulkEdited();
    }

    private void ReleaseThisCar()
    {
        if (_car is null) return;
        _car.ReleaseFromImpound();
        MarkBulkEdited();
    }

    private void ClearInfractions()
    {
        if (_car is null) return;
        _car.ClearInfractions();
        MarkBulkEdited();
    }
}
