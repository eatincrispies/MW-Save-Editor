using NFSMWSave.Core;

namespace NFSMWSave.App;

internal sealed class BlacklistPage : EditorPage
{
    private const int SideWidth = 262;

    private readonly ListView _list = Ui.List();
    private readonly CheckBox _unlock = Ui.Check("Also unlock all parts");
    private readonly Label _hint = Ui.Note(225);
    private readonly PictureBox _portrait = Ui.Picture(236, 190);
    private readonly Label _rival = new() { AutoSize = true, Font = new Font("Segoe UI", 11f, FontStyle.Bold), Margin = new Padding(Ui.S(4), Ui.S(2), Ui.S(2), 0) };
    private readonly Label _car = new() { AutoSize = true, Margin = new Padding(Ui.S(4), 0, Ui.S(2), Ui.S(4)) };
    private readonly Label _bounty = Ui.Text("");
    private readonly Label _races = Ui.Text("");
    private readonly Label _challenges = Ui.Text("");
    private readonly Button _set;

    public BlacklistPage()
    {
        _list.Columns.Add("Rank");
        _list.Columns.Add("Rival");
        _list.Columns.Add("Car");
        _list.Columns.Add("Bounty needed", -2, HorizontalAlignment.Right);
        _list.Columns.Add("Status");
        Ui.FitColumns(_list, 50, 85, 250, 105, 75);
        _list.Dock = DockStyle.Fill;
        _list.SelectedIndexChanged += (_, _) => ShowSelected();
        _list.DoubleClick += (_, _) => SetRank();
        Ui.RightClick(_list, (menu, hit) =>
        {
            if (File is not null && hit?.Tag is RivalInfo rival)
            {
                Ui.Item(menu, $"Set Rank to #{rival.Rank} {rival.Name}", SetRank, File.Progress.CanChangeRank && rival.Rank != File.Career.Rank);
            }
        });
        _unlock.Checked = true;
        _set = Ui.Action("Set rank", SetRank, 225);

        var needs = Ui.Fields(105);
        Ui.Row(needs, "Bounty needed", _bounty);
        Ui.Row(needs, "Races won", _races);
        Ui.Row(needs, "Challenges", _challenges);

        var change = Ui.Column(_set, _unlock, _hint);
        var details = Ui.Column(_portrait, _rival, _car, Ui.Section("Requirements", needs, 245), Ui.Section("Change rank", change, 245));
        var side = new Panel { Dock = DockStyle.Right, Width = Ui.S(SideWidth), AutoScroll = true, BackColor = Theme.Back };
        side.Controls.Add(details);

        var fill = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Back };
        fill.Controls.Add(_list);
        Controls.Add(fill);
        Controls.Add(side);
    }

    public override string Title => "Blacklist";
    public override string IconName => "tab_blacklist";

    protected override void Populate()
    {
        var save = File!;
        var selected = _list.SelectedIndices.Count > 0 ? _list.SelectedIndices[0] : Math.Clamp(save.Career.Rank, Career.FirstRank, Career.LastRank) - 1;
        _list.BeginUpdate();
        _list.Items.Clear();
        foreach (var rival in GameData.Rivals)
        {
            var item = new ListViewItem($"#{rival.Rank}") { Tag = rival };
            item.SubItems.Add(rival.Name);
            item.SubItems.Add(rival.Car);
            item.SubItems.Add(rival.RequiredBounty.ToString("N0"));
            item.SubItems.Add(StatusOf(save, rival.Rank));
            if (rival.Rank == save.Career.Rank)
            {
                item.Font = new Font(_list.Font, FontStyle.Bold);
            }
            _list.Items.Add(item);
        }
        _list.EndUpdate();
        _list.Items[selected].Selected = true;
        _list.Items[selected].EnsureVisible();
        _hint.Text = save.Progress.CanChangeRank ? "" : "Finish the game's prologue before changing rank.";
        _hint.Visible = !save.Progress.CanChangeRank;
        ShowSelected();
    }

    private static string StatusOf(SaveFile save, int rank)
    {
        if (save.Career.Rank > Career.LastRank)
        {
            return "Locked";
        }
        return rank > save.Career.Rank ? "Beaten" : rank == save.Career.Rank ? "Current" : "Locked";
    }

    private void ShowSelected()
    {
        if (_list.SelectedItems.Count == 0 || _list.SelectedItems[0].Tag is not RivalInfo rival || File is null)
        {
            return;
        }
        var bin = File.Progress.GetBin(rival.Rank);
        _portrait.Image = Assets.Get($"rival_{rival.Rank}");
        _rival.Text = $"#{rival.Rank} {rival.Name}";
        _car.Text = rival.Car;
        _bounty.Text = rival.RequiredBounty.ToString("N0");
        _races.Text = $"{bin.RaceWins} of {rival.RequiredWins}";
        _challenges.Text = $"{bin.Challenges} of {rival.RequiredChallenges}";
        var current = rival.Rank == File.Career.Rank;
        _set.Text = current ? "This is your current rank" : $"Set rank to #{rival.Rank}";
        _set.Enabled = File.Progress.CanChangeRank && !current;
    }

    private void SetRank()
    {
        if (File is null || !File.Progress.CanChangeRank || _list.SelectedItems.Count == 0 || _list.SelectedItems[0].Tag is not RivalInfo rival || rival.Rank == File.Career.Rank)
        {
            return;
        }
        var back = rival.Rank > File.Career.Rank;
        var detail = back
            ? $"Rivals #{rival.Rank} and higher go back to unbeaten, career progress for those ranks is cleared, and the Career beaten flag is turned off."
            : $"Every rival below #{rival.Rank} is marked beaten, along with their races and milestones.";
        var answer = MessageBox.Show(this, $"Move this career from #{File.Career.Rank} to #{rival.Rank} ({rival.Name})?\n\n{detail}\n\nNothing is written until you save.", "Set Blacklist rank", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        if (answer != DialogResult.Yes)
        {
            return;
        }
        File.Progress.SetRank(rival.Rank);
        if (_unlock.Checked)
        {
            File.Unlocks.UnlockAll();
        }
        MarkBulkEdited();
    }
}
