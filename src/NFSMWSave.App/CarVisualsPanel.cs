using NFSMWSave.Core;

namespace NFSMWSave.App;

internal sealed class CarVisualsPanel : Panel
{
    private const int SectionWidth = 276;
    private const int LabelWidth = 84;
    private const int PickerWidth = 166;
    private const string NoVinyl = "None";
    private const string CurrentVinyl = "Current";
    private const string NoNumbers = "None";

    private readonly SwatchPicker _paint = new(PickerWidth);
    private readonly ComboBox _vinylGroup = Combo();
    private readonly ComboBox _vinyl = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = Ui.S(PickerWidth), DisplayMember = nameof(VinylChoice.Name), MaxDropDownItems = 18, Margin = new Padding(Ui.S(2)) };
    private readonly ComboBox[] _vinylColours = new ComboBox[CarPaints.MaxVinylColours];
    private readonly SwatchPicker _rimPaint = new(PickerWidth, true);
    private readonly Label _rimNote = Ui.Note(250);
    private readonly PartPicker _tint = new(PickerWidth);
    private readonly ComboBox _decalPlace = Combo();
    private readonly ComboBox _decalSlot = Combo();
    private readonly PartPicker _decal = new(PickerWidth);
    private readonly ComboBox _numberLeft = Combo();
    private readonly ComboBox _numberRight = Combo();
    private readonly PartPicker _gauge = new(PickerWidth);
    private readonly ComboBox _gaugeFace = SwatchPicker.SwatchCombo(PickerWidth);
    private readonly ComboBox _gaugeNeedle = SwatchPicker.SwatchCombo(PickerWidth);
    private readonly ComboBox _gaugeLettering = SwatchPicker.SwatchCombo(PickerWidth);
    private readonly Control _content;
    private readonly Label _unavailable = Ui.Note(500);
    private CareerCar? _car;
    private CarStable? _stable;
    private CarVisualChoices? _choices;
    private IReadOnlyList<VinylChoice> _vinyls = [];
    private ushort _paintPart;
    private bool _loading;

    public CarVisualsPanel()
    {
        Dock = DockStyle.Fill;
        AutoScroll = true;
        BackColor = Theme.Back;

        var paint = Ui.Fields(LabelWidth);
        Ui.Row(paint, "Type", _paint.Type);
        Ui.Row(paint, "Color", _paint.Colour);

        var vinyl = Ui.Fields(LabelWidth);
        Ui.Row(vinyl, "Type", _vinylGroup);
        Ui.Row(vinyl, "Vinyl", _vinyl);
        for (var i = 0; i < _vinylColours.Length; i++)
        {
            var colour = SwatchPicker.SwatchCombo(PickerWidth);
            colour.Items.AddRange(CarPaints.VinylColours.Cast<object>().ToArray());
            colour.SelectedIndexChanged += (_, _) => WriteLook();
            _vinylColours[i] = colour;
            Ui.Row(vinyl, $"Color {i + 1}", colour);
        }

        var rimPaint = Ui.Fields(LabelWidth);
        Ui.Row(rimPaint, "Type", _rimPaint.Type);
        Ui.Row(rimPaint, "Color", _rimPaint.Colour);
        Ui.Span(rimPaint, _rimNote);

        var tint = Ui.Fields(LabelWidth);
        Ui.Row(tint, "Tint", _tint.Item);

        var decals = Ui.Fields(LabelWidth);
        _decalPlace.Items.AddRange(CarParts.DecalPlaces.Select(p => p.Title).Cast<object>().ToArray());
        Ui.Row(decals, "Location", _decalPlace);
        Ui.Row(decals, "Position", _decalSlot);
        Ui.Row(decals, "Color", _decal.Type);
        Ui.Row(decals, "Decal", _decal.Item);

        var numbers = Ui.Fields(LabelWidth);
        Ui.Row(numbers, "Left digit", _numberLeft);
        Ui.Row(numbers, "Right digit", _numberRight);

        var gauges = Ui.Fields(LabelWidth);
        Ui.Row(gauges, "Style", _gauge.Item);
        Ui.Row(gauges, "Gauge Face", _gaugeFace);
        Ui.Row(gauges, "Needle", _gaugeNeedle);
        Ui.Row(gauges, "Lettering", _gaugeLettering);

        var left = Ui.Column(Ui.Section("Paint", paint, SectionWidth), Ui.Section("Vinyls", vinyl, SectionWidth), Ui.Section("Rim Paint", rimPaint, SectionWidth), Ui.Section("Window Tint", tint, SectionWidth));
        var right = Ui.Column(Ui.Section("Decals", decals, SectionWidth), Ui.Section("Numbers", numbers, SectionWidth), Ui.Section("Custom Gauges", gauges, SectionWidth));
        _content = Ui.Across(left, right);
        _content.Location = new Point(Ui.S(2), Ui.S(2));
        _unavailable.Location = new Point(Ui.S(6), Ui.S(6));
        _unavailable.Text = "The game has no paint or visual options for this vehicle.";
        Controls.Add(_content);
        Controls.Add(_unavailable);

        _paint.Picked += part =>
        {
            if (part != CarPaints.NoPart)
            {
                _paintPart = part;
                WriteLook();
            }
        };
        _vinylGroup.SelectedIndexChanged += (_, _) =>
        {
            if (_loading)
            {
                return;
            }
            FillVinyls(null);
            ShowVinylColours();
            WriteLook();
        };
        _vinyl.SelectedIndexChanged += (_, _) =>
        {
            if (_loading)
            {
                return;
            }
            ShowVinylColours();
            WriteLook();
        };
        _rimPaint.Picked += part => Write(() => _stable!.SetParts(_car!, new Dictionary<int, ushort> { [CarPaints.RimPaintSlot] = part }));
        _tint.Picked += part => Write(() => _stable!.SetVisual(_car!, CarParts.WindowTintSlot, part));
        _decalPlace.SelectedIndexChanged += (_, _) =>
        {
            if (!_loading)
            {
                FillDecalSlots();
            }
        };
        _decalSlot.SelectedIndexChanged += (_, _) =>
        {
            if (!_loading)
            {
                LoadDecal();
            }
        };
        _decal.Picked += part => Write(() => _stable!.SetVisual(_car!, CurrentDecalSlot, part));
        _numberLeft.SelectedIndexChanged += (_, _) => WriteNumbers(_numberLeft);
        _numberRight.SelectedIndexChanged += (_, _) => WriteNumbers(_numberRight);
        _gauge.Picked += part => Write(() => _stable!.SetVisual(_car!, CarParts.GaugeSlot, part));
        WireGaugeColour(_gaugeFace, CarParts.GaugeFaceSlot);
        WireGaugeColour(_gaugeNeedle, CarParts.GaugeNeedleSlot);
        WireGaugeColour(_gaugeLettering, CarParts.GaugeLetteringSlot);
    }

    public event Action? Changed;

    private int CurrentDecalSlot => CarParts.DecalPlaces[Math.Max(0, _decalPlace.SelectedIndex)].FirstSlot + Math.Max(0, _decalSlot.SelectedIndex);

    private static ComboBox Combo() => new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = Ui.S(PickerWidth), MaxDropDownItems = 14, Margin = new Padding(Ui.S(2)) };

    public void ShowCar(CareerCar car, CarStable stable)
    {
        _car = car;
        _stable = stable;
        _choices = stable.CanPaint(car) ? stable.VisualChoices(car) : null;
        _content.Visible = _choices is not null;
        _unavailable.Visible = _choices is null;
        if (_choices is null)
        {
            return;
        }
        _loading = true;
        _paintPart = car.PaintPart;
        _paint.Load(CarPaints.Paints, car.PaintPart);
        _vinyls = CarPaints.VinylsFor(car.VehicleKey);
        LoadVinyl(car);
        for (var i = 0; i < _vinylColours.Length; i++)
        {
            var current = car.GetVinylColour(i);
            _vinylColours[i].SelectedItem = CarPaints.VinylColours.FirstOrDefault(c => c.Part == current);
        }
        ShowVinylColours();
        _rimPaint.Load(CarPaints.RimPaints, car.GetPart(CarPaints.RimPaintSlot));
        var paintable = !stable.IsStockRim(car, car.GetPart(CarParts.RimSlot));
        _rimPaint.SetEnabled(paintable);
        _rimNote.Text = paintable ? "" : "The game can't paint stock rims. Pick another rim under Parts first.";
        _rimNote.Visible = !paintable;
        _tint.Load(_choices.Tints, car.GetPart(CarParts.WindowTintSlot));
        if (_decalPlace.SelectedIndex < 0)
        {
            _decalPlace.SelectedIndex = 0;
        }
        FillDecalSlots();
        LoadNumbers(car);
        _gauge.Load(_choices.Gauges, car.GetPart(CarParts.GaugeSlot));
        LoadGaugeColour(_gaugeFace, CarParts.GaugeFaceSlot);
        LoadGaugeColour(_gaugeNeedle, CarParts.GaugeNeedleSlot);
        LoadGaugeColour(_gaugeLettering, CarParts.GaugeLetteringSlot);
        _loading = false;
    }

    private void LoadVinyl(CareerCar car)
    {
        _vinylGroup.BeginUpdate();
        _vinylGroup.Items.Clear();
        _vinylGroup.Items.Add(NoVinyl);
        var known = _vinyls.FirstOrDefault(v => v.Part == car.VinylPart);
        if (car.VinylPart != CarPaints.NoPart && known is null)
        {
            _vinylGroup.Items.Add(CurrentVinyl);
        }
        foreach (var index in _vinyls.Select(v => v.Group).Distinct().Order())
        {
            _vinylGroup.Items.Add(CarPaints.VinylGroups[index]);
        }
        _vinylGroup.EndUpdate();
        _vinylGroup.SelectedItem = known is not null ? CarPaints.VinylGroups[known.Group] : car.VinylPart != CarPaints.NoPart ? CurrentVinyl : NoVinyl;
        FillVinyls(known);
    }

    private void FillVinyls(VinylChoice? select)
    {
        var wasLoading = _loading;
        _loading = true;
        var group = Array.IndexOf(CarPaints.VinylGroups, _vinylGroup.SelectedItem as string);
        _vinyl.BeginUpdate();
        _vinyl.Items.Clear();
        if (group >= 0)
        {
            _vinyl.Items.AddRange(_vinyls.Where(v => v.Group == group).Cast<object>().ToArray());
        }
        _vinyl.EndUpdate();
        _vinyl.Enabled = group >= 0;
        if (select is not null)
        {
            _vinyl.SelectedItem = select;
        }
        else if (_vinyl.Items.Count > 0)
        {
            _vinyl.SelectedIndex = 0;
        }
        _loading = wasLoading;
    }

    private void ShowVinylColours()
    {
        var count = _vinyl.SelectedItem is VinylChoice vinyl ? vinyl.Colours : 0;
        for (var i = 0; i < _vinylColours.Length; i++)
        {
            _vinylColours[i].Enabled = i < count;
        }
    }

    private void WriteLook()
    {
        if (_loading || _car is null || _stable is null)
        {
            return;
        }
        var group = _vinylGroup.SelectedItem as string;
        var vinyl = group == NoVinyl ? CarPaints.NoPart : group == CurrentVinyl ? _car.VinylPart : _vinyl.SelectedItem is VinylChoice choice ? choice.Part : CarPaints.NoPart;
        var colours = _vinylColours.Select(c => c.SelectedItem is PaintSwatch swatch ? swatch.Part : CarPaints.NoPart).ToArray();
        _stable.SetLook(_car, _paintPart, vinyl, colours);
        Changed?.Invoke();
    }

    private void Write(Action apply)
    {
        if (_loading || _car is null || _stable is null)
        {
            return;
        }
        apply();
        Changed?.Invoke();
    }

    private void FillDecalSlots()
    {
        var wasLoading = _loading;
        _loading = true;
        var place = CarParts.DecalPlaces[Math.Max(0, _decalPlace.SelectedIndex)];
        _decalSlot.BeginUpdate();
        _decalSlot.Items.Clear();
        for (var i = 0; i < place.SlotCount; i++)
        {
            _decalSlot.Items.Add($"Slot {i + 1}");
        }
        _decalSlot.EndUpdate();
        _decalSlot.SelectedIndex = 0;
        _decalSlot.Enabled = place.SlotCount > 1;
        LoadDecal();
        _loading = wasLoading;
    }

    private void LoadDecal()
    {
        if (_car is null || _choices is null)
        {
            return;
        }
        var wasLoading = _loading;
        _loading = true;
        var list = new List<PartChoice>();
        foreach (var group in new[] { "Black", "White" })
        {
            list.Add(new PartChoice(CarPaints.NoPart, group, "No Decal"));
            list.AddRange(_choices.Decals.Where(d => d.Group == group));
        }
        _decal.Load(list, _car.GetPart(CurrentDecalSlot));
        _loading = wasLoading;
    }

    private void LoadNumbers(CareerCar car)
    {
        FillNumber(_numberLeft, _choices!.LeftNumbers, car.GetPart(CarParts.RightDoorLeftNumber));
        FillNumber(_numberRight, _choices.RightNumbers, car.GetPart(CarParts.RightDoorRightNumber));
    }

    private static void FillNumber(ComboBox box, IReadOnlyList<PartChoice> choices, ushort current)
    {
        box.BeginUpdate();
        box.Items.Clear();
        box.Items.Add(NoNumbers);
        box.Items.AddRange(choices.Cast<object>().ToArray());
        box.DisplayMember = nameof(PartChoice.Name);
        box.EndUpdate();
        box.SelectedItem = choices.FirstOrDefault(c => c.Part == current) ?? (object)NoNumbers;
    }

    private void WriteNumbers(ComboBox changed)
    {
        if (_loading || _car is null || _stable is null)
        {
            return;
        }
        _loading = true;
        var other = ReferenceEquals(changed, _numberLeft) ? _numberRight : _numberLeft;
        if (changed.SelectedItem is not PartChoice)
        {
            other.SelectedItem = NoNumbers;
        }
        else if (other.SelectedItem is not PartChoice)
        {
            other.SelectedIndex = 1;
        }
        _loading = false;
        var left = _numberLeft.SelectedItem is PartChoice l ? l.Part : CarPaints.NoPart;
        var right = _numberRight.SelectedItem is PartChoice r ? r.Part : CarPaints.NoPart;
        _stable.SetNumbers(_car, left, right);
        Changed?.Invoke();
    }

    private void LoadGaugeColour(ComboBox box, int slot)
    {
        box.BeginUpdate();
        box.Items.Clear();
        box.Items.AddRange(_choices!.GaugeColours.Cast<object>().ToArray());
        box.EndUpdate();
        var current = _car!.GetPart(slot);
        box.SelectedItem = _choices.GaugeColours.FirstOrDefault(c => c.Part == current);
    }

    private void WireGaugeColour(ComboBox box, int slot) => box.SelectedIndexChanged += (_, _) =>
    {
        if (box.SelectedItem is PaintSwatch swatch)
        {
            Write(() => _stable!.SetVisual(_car!, slot, swatch.Part));
        }
    };
}
