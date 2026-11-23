using NFSMWSave.Core;

namespace NFSMWSave.App;

internal sealed class CareerPage : EditorPage
{
    private const int ColumnWidth = 370;
    private const int LabelWidth = 105;
    private const int StatLabelWidth = 130;
    private const string NotAvailable = "Not available";

    private readonly TextBox _alias = new() { MaxLength = SaveFile.MaxAliasLength, Width = Ui.S(140) };
    private readonly Label _caseFile = Ui.Text("");
    private readonly Label _rank = Ui.Text("");
    private readonly NumericUpDown _cash = Ui.Number(0, Career.MaxCash, 140);
    private readonly NumericUpDown _bounty = Ui.Number(0, CarStable.MaxBounty, 140);
    private readonly ComboBox _car = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = Ui.S(230) };
    private readonly Label _gameCompletion = Ui.Text("");
    private readonly Label _careerCompletion = Ui.Text("");
    private readonly Label _rapSheet = Ui.Text("");
    private readonly Label _challenge = Ui.Text("");
    private readonly Label _evaded = Ui.Text("");
    private readonly Label _busted = Ui.Text("");
    private readonly Label _infractions = Ui.Text("");
    private readonly Label _pursuitTime = Ui.Text("");

    public CareerPage()
    {
        var profile = Ui.Fields(LabelWidth);
        Ui.Row(profile, "Alias", _alias);
        Ui.Row(profile, "Case file", _caseFile);
        Ui.Row(profile, "Current car", _car);

        var money = Ui.Fields(LabelWidth);
        Ui.Row(money, "Cash", WithMax(_cash));
        Ui.Row(money, "Total bounty", WithMax(_bounty));

        var progress = Ui.Fields(StatLabelWidth);
        Ui.Row(progress, "Blacklist rank", _rank);
        Ui.Row(progress, "Game Completion", _gameCompletion);
        Ui.Row(progress, "Career Completion", _careerCompletion);
        Ui.Row(progress, "Rap Sheet", _rapSheet);
        Ui.Row(progress, "Challenge Series", _challenge);

        var stats = Ui.Fields(StatLabelWidth);
        Ui.Row(stats, "Pursuits Evaded", _evaded);
        Ui.Row(stats, "Times Busted", _busted);
        Ui.Row(stats, "Infractions", _infractions);
        Ui.Row(stats, "Time in Pursuit", _pursuitTime);

        var left = Ui.Column(Ui.Section("Profile", profile, ColumnWidth), Ui.Section("Money", money, ColumnWidth));
        var right = Ui.Column(Ui.Section("Progress", progress, ColumnWidth), Ui.Section("Game Stats", stats, ColumnWidth));
        Controls.Add(Ui.Page(left, right));

        VisibleChanged += (_, _) =>
        {
            if (Visible && File is not null)
            {
                ShowStats();
            }
        };
        _alias.KeyPress += (_, e) => e.Handled = !char.IsControl(e.KeyChar) && !char.IsAsciiLetterOrDigit(e.KeyChar);
        _alias.TextChanged += (_, _) =>
        {
            if (IsBinding || File is null)
            {
                return;
            }
            var problem = SaveFile.AliasProblem(_alias.Text);
            _alias.BackColor = problem is null ? Theme.Field : Theme.Error;
            if (problem is null && _alias.Text != File.Career.ProfileName)
            {
                File.SetAlias(_alias.Text);
                MarkEdited();
            }
        };
        OnNumber(_cash, v => File!.Career.Cash = (uint)v);
        OnNumber(_bounty, v =>
        {
            File!.Stable.SetTotalBounty((long)v);
            MarkBulkEdited();
        });
        _car.SelectedIndexChanged += (_, _) =>
        {
            if (!IsBinding && File is not null && _car.SelectedItem is CarChoice choice)
            {
                File.Career.CurrentCar = choice.Car.Handle;
                MarkEdited();
            }
        };
    }

    public override string Title => "Career";
    public override string IconName => "tab_career";

    private static FlowLayoutPanel WithMax(NumericUpDown number) => Ui.Across(number, Ui.Action("Max", () => number.Value = number.Maximum, 60));

    protected override void Populate()
    {
        var save = File!;
        _alias.CharacterCasing = save.Map.UppercaseAlias ? CharacterCasing.Upper : CharacterCasing.Normal;
        _alias.Text = save.Career.ProfileName;
        _alias.BackColor = Theme.Field;
        _alias.ReadOnly = !save.Map.CanRenameAlias;
        _caseFile.Text = save.Career.CaseFile;
        var rival = GameData.Rivals.FirstOrDefault(r => r.Rank == save.Career.Rank);
        _rank.Text = rival is null ? "Prologue" : $"#{rival.Rank} {rival.Name}";
        Ui.Show(_cash, save.Career.Cash);
        Ui.Show(_bounty, save.Stable.TotalBounty);
        _car.Items.Clear();
        foreach (var car in save.Stable.Cars)
        {
            _car.Items.Add(new CarChoice(car));
        }
        _car.Enabled = _car.Items.Count > 0;
        _car.SelectedItem = _car.Items.Cast<CarChoice>().FirstOrDefault(c => c.Car.Handle == save.Career.CurrentCar);
        ShowStats();
    }

    private void ShowStats()
    {
        var save = File!;
        var result = CompletionStats.Compute(save);
        _gameCompletion.Text = result is null ? NotAvailable : $"{result.Overall}%";
        _careerCompletion.Text = result is null ? NotAvailable : $"{result.Career}%";
        _rapSheet.Text = result is null ? NotAvailable : $"{result.RapSheet}%";
        _challenge.Text = result is null ? NotAvailable : $"{result.ChallengeDone}/{result.ChallengeTotal}   {result.Challenge}%";
        var available = save.Stats.IsAvailable;
        _evaded.Text = save.Stable.TotalEvaded.ToString("N0");
        _busted.Text = save.Stable.TotalBusted.ToString("N0");
        _infractions.Text = save.Stable.TotalInfractions.ToString("N0");
        var time = TimeSpan.FromSeconds(save.Stats.PursuitSeconds);
        _pursuitTime.Text = available ? $"{(int)time.TotalHours}:{time.Minutes:00}:{time.Seconds:00}" : NotAvailable;
    }

    private sealed record CarChoice(CareerCar Car)
    {
        public override string ToString() => Car.Name;
    }
}
