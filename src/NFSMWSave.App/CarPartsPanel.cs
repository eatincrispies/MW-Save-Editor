using NFSMWSave.Core;

namespace NFSMWSave.App;

internal sealed class CarPartsPanel : Panel
{
    private const int SectionWidth = 276;
    private const int LabelWidth = 72;
    private const int PickerWidth = 178;
    private const uint HeroCarKey = 0x4E085FB3;

    private readonly PartPicker _body = new(PickerWidth);
    private readonly PartPicker _spoiler = new(PickerWidth, CarParts.SpoilerTypes);
    private readonly PartPicker _rim = new(PickerWidth);
    private readonly PartPicker _hood = new(PickerWidth, CarParts.ScoopTypes);
    private readonly PartPicker _roof = new(PickerWidth, CarParts.ScoopTypes);
    private readonly Button _stock;
    private readonly Label _note = Ui.Note(530);
    private CareerCar? _car;
    private CarStable? _stable;

    public CarPartsPanel()
    {
        Dock = DockStyle.Fill;
        AutoScroll = true;
        BackColor = Theme.Back;

        var body = Ui.Fields(LabelWidth);
        Ui.Row(body, "Body Kit", _body.Item);
        var spoiler = Ui.Fields(LabelWidth);
        Ui.Row(spoiler, "Type", _spoiler.Type);
        Ui.Row(spoiler, "Spoiler", _spoiler.Item);
        var rim = Ui.Fields(LabelWidth);
        Ui.Row(rim, "Brand", _rim.Type);
        Ui.Row(rim, "Rim", _rim.Item);
        var hood = Ui.Fields(LabelWidth);
        Ui.Row(hood, "Type", _hood.Type);
        Ui.Row(hood, "Hood", _hood.Item);
        var roof = Ui.Fields(LabelWidth);
        Ui.Row(roof, "Type", _roof.Type);
        Ui.Row(roof, "Roof Scoop", _roof.Item);

        _stock = Ui.Action("Set all to Stock", SetAllStock, 150);
        var left = Ui.Column(Ui.Section("Body Kits", body, SectionWidth), Ui.Section("Spoilers", spoiler, SectionWidth), Ui.Section("Rims", rim, SectionWidth));
        var right = Ui.Column(Ui.Section("Hoods", hood, SectionWidth), Ui.Section("Roof Scoops", roof, SectionWidth), _stock);
        var stack = Ui.Column(Ui.Across(left, right), _note);
        stack.Location = new Point(Ui.S(2), Ui.S(2));
        Controls.Add(stack);

        Wire(_body, CarParts.BodySlot);
        Wire(_spoiler, CarParts.SpoilerSlot);
        Wire(_rim, CarParts.RimSlot);
        Wire(_hood, CarParts.HoodSlot);
        Wire(_roof, CarParts.RoofSlot);
    }

    public event Action? Changed;

    public void ShowCar(CareerCar car, CarStable stable)
    {
        _car = car;
        _stable = stable;
        var usable = stable.CanChangeParts(car);
        foreach (var (picker, slot) in Pickers())
        {
            if (usable)
            {
                picker.Load(stable.PartsFor(car, slot), car.GetPart(slot));
            }
            else
            {
                picker.Clear();
            }
        }
        _stock.Enabled = usable;
        _note.Text = !usable
            ? "The game has no shop parts for this vehicle."
            : CarCatalog.PartsKey(car.VehicleKey) == HeroCarKey ? "The game's shop doesn't offer parts for this car, so keep a backup and test it in game." : "";
        _note.Visible = _note.Text.Length > 0;
    }

    private IEnumerable<(PartPicker Picker, int Slot)> Pickers()
    {
        yield return (_body, CarParts.BodySlot);
        yield return (_spoiler, CarParts.SpoilerSlot);
        yield return (_rim, CarParts.RimSlot);
        yield return (_hood, CarParts.HoodSlot);
        yield return (_roof, CarParts.RoofSlot);
    }

    private void Wire(PartPicker picker, int slot) => picker.Picked += part =>
    {
        if (_car is null || _stable is null || part == _car.GetPart(slot))
        {
            return;
        }
        _stable.SetParts(_car, new Dictionary<int, ushort> { [slot] = part });
        Changed?.Invoke();
    };

    private void SetAllStock()
    {
        if (_car is null || _stable is null)
        {
            return;
        }
        var parts = new Dictionary<int, ushort>();
        foreach (var (_, slot) in Pickers())
        {
            var list = _stable.PartsFor(_car, slot);
            if (list.Count > 0)
            {
                parts[slot] = list[0].Part;
            }
        }
        _stable.SetParts(_car, parts);
        ShowCar(_car, _stable);
        Changed?.Invoke();
    }
}
