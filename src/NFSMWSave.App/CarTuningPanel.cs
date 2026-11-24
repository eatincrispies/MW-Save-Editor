using NFSMWSave.Core;

namespace NFSMWSave.App;

internal sealed class CarTuningPanel : Panel
{
    private const int SectionWidth = 560;
    private const int LabelWidth = 150;

    private readonly ComboBox _setting = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = Ui.S(140), Margin = new Padding(Ui.S(2)) };
    private readonly TrackBar[] _bars = new TrackBar[CareerCar.TuningSliders];
    private readonly Label[] _values = new Label[CareerCar.TuningSliders];
    private readonly Label[] _needs = new Label[CareerCar.TuningSliders];
    private readonly Label _note = Ui.Note(530);
    private CareerCar? _car;
    private CarStable? _stable;
    private bool _binding;

    public CarTuningPanel()
    {
        Dock = DockStyle.Fill;
        AutoScroll = true;
        BackColor = Theme.Back;

        for (var i = 1; i <= CareerCar.TuningSettings; i++)
        {
            _setting.Items.Add($"Setting {i}");
        }
        var table = Ui.Fields(LabelWidth);
        Ui.Row(table, "Setting", _setting);
        for (var slider = 0; slider < CareerCar.TuningSliders; slider++)
        {
            var index = slider;
            _bars[slider] = new TrackBar
            {
                Minimum = -CareerCar.TuningLimit,
                Maximum = CareerCar.TuningLimit,
                SmallChange = 1,
                LargeChange = 1,
                TickFrequency = 1,
                TickStyle = TickStyle.None,
                AutoSize = false,
                Width = Ui.S(190),
                Height = Ui.S(26),
                BackColor = Theme.Back,
                Margin = new Padding(0, Ui.S(2), 0, 0),
            };
            _values[slider] = new Label { AutoSize = false, Width = Ui.S(34), Height = Ui.S(24), TextAlign = ContentAlignment.MiddleRight, Margin = new Padding(0, Ui.S(2), Ui.S(8), 0) };
            _needs[slider] = new Label { AutoSize = true, ForeColor = Theme.Muted, Margin = new Padding(0, Ui.S(7), 0, 0) };
            _bars[slider].ValueChanged += (_, _) => SliderMoved(index);
            Ui.Row(table, CareerCar.TuningNames[slider], Ui.Across(_bars[slider], _values[slider], _needs[slider]));
        }
        _setting.SelectedIndexChanged += (_, _) => SettingChosen();

        var stack = Ui.Column(Ui.Section("Performance Tuning", table, SectionWidth), _note);
        stack.Location = new Point(Ui.S(2), Ui.S(2));
        Controls.Add(stack);
    }

    public event Action? Changed;

    public void ShowCar(CareerCar car, CarStable stable)
    {
        _car = car;
        _stable = stable;
        _binding = true;
        _setting.Enabled = car.HasCustomization;
        _setting.SelectedIndex = car.ActiveTuning;
        ShowValues();
        _binding = false;
    }

    private void ShowValues()
    {
        if (_car is null || _stable is null)
        {
            return;
        }
        var anyLocked = false;
        for (var slider = 0; slider < CareerCar.TuningSliders; slider++)
        {
            var value = _car.GetTuning(_car.ActiveTuning, slider);
            var open = _stable.CanTune(_car, slider);
            anyLocked |= !open;
            _bars[slider].Value = value;
            _bars[slider].Enabled = open;
            _values[slider].Text = Format(value);
            _needs[slider].Text = open ? "" : CareerCar.TuningNeeds[slider];
        }
        _note.Text = anyLocked ? "Stock parts can't be tuned. Each slider opens once its matching upgrade or body part is installed." : "";
        _note.Visible = anyLocked;
    }

    private void SettingChosen()
    {
        if (_binding || _car is null || _setting.SelectedIndex < 0)
        {
            return;
        }
        _car.ActiveTuning = _setting.SelectedIndex;
        _binding = true;
        ShowValues();
        _binding = false;
        Changed?.Invoke();
    }

    private void SliderMoved(int slider)
    {
        _values[slider].Text = Format(_bars[slider].Value);
        if (_binding || _car is null)
        {
            return;
        }
        _car.SetTuning(_car.ActiveTuning, slider, _bars[slider].Value);
        Changed?.Invoke();
    }

    private static string Format(int value) => value > 0 ? $"+{value}" : value.ToString();
}
