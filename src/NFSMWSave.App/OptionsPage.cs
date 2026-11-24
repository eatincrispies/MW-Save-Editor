using NFSMWSave.Core;

namespace NFSMWSave.App;

internal sealed class OptionsPage : EditorPage
{
    private const int SectionWidth = 400;
    private const int LabelWidth = 170;
    private const int ChoiceWidth = 175;
    private const int VolumeStep = 10;
    private static readonly string[] OnOff = ["On", "Off"];

    private readonly List<(VolumeField Field, TrackBar Bar, Label Value)> _volumes = [];
    private readonly List<Action<GameOptions>> _refresh = [];
    private readonly List<Action<PlayerOptions>> _refreshPlayer = [];
    private readonly ComboBox _audioMode = Choice();
    private readonly ComboBox _player = Choice();
    private readonly ComboBox _controlsPlayer = Choice();
    private readonly EaTraxPanel _trax = new();
    private readonly GroupBox _video;
    private int _playerIndex;

    public OptionsPage()
    {
        var audio = Ui.Fields(LabelWidth);
        foreach (var field in GameOptions.MenuVolumes)
        {
            AddVolume(audio, field);
        }
        Toggle(audio, "Interactive Music", o => o.InteractiveMusic, (o, v) => o.InteractiveMusic = v);
        Toggle(audio, "EA™ TRAX", o => o.EaTrax, (o, v) => o.EaTrax = v);
        Ui.Row(audio, "Audio Mode", _audioMode);
        _audioMode.SelectedIndexChanged += (_, _) => Apply(_audioMode, o => o.AudioMode = _audioMode.SelectedIndex);

        var videoFields = Ui.Fields(LabelWidth);
        Toggle(videoFields, "Widescreen", o => o.Widescreen, (o, v) => o.Widescreen = v);
        _video = Ui.Section("Video", videoFields, SectionWidth);

        var gameplay = Ui.Fields(LabelWidth);
        Toggle(gameplay, "Autosave", o => o.AutoSave, (o, v) => o.AutoSave = v);
        Toggle(gameplay, "Game Moment Camera", o => o.JumpCam, (o, v) => o.JumpCam = v);
        Toggle(gameplay, "Car Damage", o => o.Damage, (o, v) => o.Damage = v);
        Toggle(gameplay, "Rearview Mirror", o => o.Rearview, (o, v) => o.Rearview = v);
        Pick(gameplay, "Units", ["Imperial", "Metric"], o => o.Kilometers ? 1 : 0, (o, v) => o.Kilometers = v == 1);
        Pick(gameplay, "Free Roam Map Mode", GameOptions.MapModes, o => o.FreeRoamMap, (o, v) => o.FreeRoamMap = v);
        Pick(gameplay, "Race Map Mode", GameOptions.MapModes, o => o.RaceMap, (o, v) => o.RaceMap = v);

        var player = Ui.Fields(LabelWidth);
        foreach (var box in new[] { _player, _controlsPlayer })
        {
            var source = box;
            source.Items.AddRange(["Player 1", "Player 2"]);
            source.SelectedIndex = 0;
            source.SelectedIndexChanged += (_, _) =>
            {
                if (!IsBinding)
                {
                    SelectPlayer(Math.Max(0, source.SelectedIndex));
                }
            };
        }
        Ui.Row(player, "Settings for", _player);
        PickPlayer(player, "Transmission", ["Auto", "Manual"], p => p.ManualTransmission ? 1 : 0, (p, v) => p.ManualTransmission = v == 1);
        PickPlayer(player, "Camera", GameOptions.Cameras, p => p.Camera, (p, v) => p.Camera = v);
        TogglePlayer(player, "Gauges", p => p.Gauges, (p, v) => p.Gauges = v);
        TogglePlayer(player, "Race Information", p => p.RaceInformation, (p, v) => p.RaceInformation = v);
        TogglePlayer(player, "Split Time", p => p.SplitTime, (p, v) => p.SplitTime = v);
        TogglePlayer(player, "Score", p => p.Score, (p, v) => p.Score = v);
        TogglePlayer(player, "Leaderboard", p => p.Leaderboard, (p, v) => p.Leaderboard = v);

        var controls = Ui.Fields(LabelWidth);
        Ui.Row(controls, "Settings for", _controlsPlayer);
        PickPlayer(controls, "Control Type", Enumerable.Range(1, PlayerOptions.ControlTypes).Select(n => n.ToString()).ToList(), p => p.ControlType, (p, v) => p.ControlType = v);
        TogglePlayer(controls, "Vibration", p => p.Vibration, (p, v) => p.Vibration = v);

        var left = Ui.Column(Ui.Section("Audio", audio, SectionWidth), Ui.Section("Controls", controls, SectionWidth), _video);
        var right = Ui.Column(Ui.Section("Gameplay", gameplay, SectionWidth), Ui.Section("Player", player, SectionWidth));

        var settings = new TabPage("Settings") { BackColor = Theme.Back, UseVisualStyleBackColor = false, Padding = new Padding(Ui.S(4)) };
        settings.Controls.Add(Ui.Page(left, right));
        var trax = new TabPage("EA Trax") { BackColor = Theme.Back, UseVisualStyleBackColor = false, Padding = new Padding(Ui.S(4)) };
        trax.Controls.Add(_trax);
        _trax.Changed += MarkEdited;
        var sections = new DarkTabControl { Dock = DockStyle.Fill };
        sections.TabPages.AddRange([settings, trax]);
        Controls.Add(sections);
    }

    private void SelectPlayer(int index)
    {
        _playerIndex = index;
        Bound(() =>
        {
            _player.SelectedIndex = index;
            _controlsPlayer.SelectedIndex = index;
            if (File is not null)
            {
                ShowPlayer();
            }
        });
    }

    public override string Title => "Options";
    public override string IconName => "tab_options";

    private static ComboBox Choice() => new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = Ui.S(ChoiceWidth), Margin = new Padding(Ui.S(2)) };

    private void AddVolume(TableLayoutPanel table, VolumeField field)
    {
        var bar = new TrackBar { Minimum = 0, Maximum = 100, TickFrequency = VolumeStep, SmallChange = VolumeStep, LargeChange = VolumeStep, TickStyle = TickStyle.None, AutoSize = false, Width = Ui.S(145), Height = Ui.S(26), BackColor = Theme.Back, Margin = new Padding(0, Ui.S(2), 0, 0) };
        var value = new Label { AutoSize = false, Width = Ui.S(42), Height = Ui.S(24), TextAlign = ContentAlignment.MiddleRight, Margin = new Padding(0, Ui.S(2), 0, 0) };
        _volumes.Add((field, bar, value));
        bar.ValueChanged += (_, _) =>
        {
            value.Text = $"{bar.Value}%";
            if (IsBinding || File is null)
            {
                return;
            }
            var stepped = (int)Math.Round(bar.Value / (double)VolumeStep) * VolumeStep;
            if (stepped != bar.Value)
            {
                bar.Value = stepped;
                return;
            }
            File.Options.SetVolume(field, stepped);
            MarkEdited();
        };
        Ui.Row(table, field.Name, Ui.Across(bar, value));
    }

    private void Apply(ComboBox box, Action<GameOptions> write)
    {
        if (!IsBinding && File is not null && box.SelectedIndex >= 0)
        {
            write(File.Options);
            MarkEdited();
        }
    }

    private void Toggle(TableLayoutPanel table, string title, Func<GameOptions, bool> read, Action<GameOptions, bool> write) =>
        Pick(table, title, OnOff, o => read(o) ? 0 : 1, (o, v) => write(o, v == 0));

    private void Pick(TableLayoutPanel table, string title, IReadOnlyList<string> names, Func<GameOptions, int> read, Action<GameOptions, int> write)
    {
        var box = Choice();
        box.Items.AddRange(names.Cast<object>().ToArray());
        Ui.Row(table, title, box);
        box.SelectedIndexChanged += (_, _) => Apply(box, o => write(o, box.SelectedIndex));
        _refresh.Add(o =>
        {
            var value = read(o);
            box.SelectedIndex = value >= 0 && value < names.Count ? value : -1;
        });
    }

    private void TogglePlayer(TableLayoutPanel table, string title, Func<PlayerOptions, bool> read, Action<PlayerOptions, bool> write) =>
        PickPlayer(table, title, OnOff, p => read(p) ? 0 : 1, (p, v) => write(p, v == 0));

    private void PickPlayer(TableLayoutPanel table, string title, IReadOnlyList<string> names, Func<PlayerOptions, int> read, Action<PlayerOptions, int> write)
    {
        var box = Choice();
        box.Items.AddRange(names.Cast<object>().ToArray());
        Ui.Row(table, title, box);
        box.SelectedIndexChanged += (_, _) => Apply(box, o => write(o.Player(_playerIndex), box.SelectedIndex));
        _refreshPlayer.Add(p =>
        {
            var value = read(p);
            box.SelectedIndex = value >= 0 && value < names.Count ? value : -1;
        });
    }

    private void ShowPlayer()
    {
        var player = File!.Options.Player(_playerIndex);
        foreach (var refresh in _refreshPlayer)
        {
            refresh(player);
        }
    }

    protected override void Populate()
    {
        var options = File!.Options;
        foreach (var (field, bar, value) in _volumes)
        {
            bar.Value = Math.Clamp(options.GetVolume(field), bar.Minimum, bar.Maximum);
            value.Text = $"{bar.Value}%";
        }
        foreach (var refresh in _refresh)
        {
            refresh(options);
        }
        _audioMode.Items.Clear();
        _audioMode.Items.AddRange(options.AudioModes.Cast<object>().ToArray());
        _audioMode.SelectedIndex = options.AudioMode >= 0 && options.AudioMode < _audioMode.Items.Count ? options.AudioMode : -1;
        _video.Visible = options.HasWidescreen;
        ShowPlayer();
        _trax.ShowSave(File);
    }
}
