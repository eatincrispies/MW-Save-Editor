using NFSMWSave.Core;

namespace NFSMWSave.App;

internal sealed class RacesPage : EditorPage
{
    private const string AllGroups = "All events";
    private readonly ListBox _groups = new() { Dock = DockStyle.Fill, IntegralHeight = false };
    private readonly ListView _list = Ui.List();
    private readonly Label _selection = new() { AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(Ui.S(4), Ui.S(6), Ui.S(8), Ui.S(2)) };
    private readonly List<Button> _actions = [];
    private readonly MilestonesPanel _milestones = new();

    public RacesPage()
    {
        _list.MultiSelect = true;
        _list.Columns.Add("Event");
        _list.Columns.Add("Group");
        _list.Columns.Add("Career complete");
        _list.Columns.Add("Quick Race complete");
        _list.Columns.Add("Quick Race unlocked");
        Ui.FitColumns(_list, 80, 125, 105, 130, 130);
        _list.Dock = DockStyle.Fill;
        _list.KeyDown += (_, e) =>
        {
            if (e.Control && e.KeyCode == Keys.A)
            {
                SelectAll();
                e.Handled = true;
            }
        };
        _list.SelectedIndexChanged += (_, _) => ShowSelection();
        Ui.RightClick(_list, BuildMenu);
        _groups.SelectedIndexChanged += (_, _) => { if (!IsBinding) Bound(Fill); };

        var groupsBox = new DarkGroupBox { Text = "Show", Dock = DockStyle.Left, Width = Ui.S(190), BackColor = Theme.Back, Padding = new Padding(Ui.S(6), Ui.S(4), Ui.S(6), Ui.S(6)) };
        groupsBox.Controls.Add(_groups);

        var selectAll = Ui.Action("Select all", SelectAll, 85);
        var picking = Ui.Column(selectAll, _selection);
        var career = Ui.Across(Action("Mark complete", RaceFlags.CareerCompleted | RaceFlags.CareerUnlocked, true, 110), Action("Mark not complete", RaceFlags.CareerCompleted, false, 125));
        var quick = Ui.Across(Action("Unlock", RaceFlags.QuickRaceUnlocked, true, 80), Action("Lock", RaceFlags.QuickRaceUnlocked, false, 80));
        var bar = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, WrapContents = true, BackColor = Theme.Back };
        bar.Controls.AddRange([picking, Ui.Group("Career", career), Ui.Group("Quick Race", quick)]);

        var right = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Back, Padding = new Padding(Ui.S(6), Ui.S(6), 0, 0) };
        right.Controls.Add(_list);
        right.Controls.Add(bar);

        var racing = new TabPage("Racing") { BackColor = Theme.Back, UseVisualStyleBackColor = false, Padding = new Padding(Ui.S(4)) };
        racing.Controls.Add(right);
        racing.Controls.Add(groupsBox);
        var milestones = new TabPage("Milestones") { BackColor = Theme.Back, UseVisualStyleBackColor = false, Padding = new Padding(Ui.S(4)) };
        milestones.Controls.Add(_milestones);
        _milestones.Changed += MarkBulkEdited;
        var sections = new DarkTabControl { Dock = DockStyle.Fill };
        sections.TabPages.AddRange([racing, milestones]);
        Controls.Add(sections);
    }

    public override string Title => "Races";
    public override string IconName => "tab_races";

    private Button Action(string text, RaceFlags flags, bool on, int width)
    {
        var button = Ui.Action(text, () => Apply(flags, on), width);
        _actions.Add(button);
        return button;
    }

    protected override void Populate()
    {
        RefreshGroups();
        Fill();
        _milestones.ShowSave(File!);
    }

    private void BuildMenu(ContextMenuStrip menu, ListViewItem? hit)
    {
        if (File is null)
        {
            return;
        }
        if (hit is not null)
        {
            Ui.Item(menu, "Mark Complete", () => Apply(RaceFlags.CareerCompleted | RaceFlags.CareerUnlocked, true));
            Ui.Item(menu, "Mark Not Complete", () => Apply(RaceFlags.CareerCompleted, false));
            Ui.Line(menu);
            Ui.Item(menu, "Unlock in Quick Race", () => Apply(RaceFlags.QuickRaceUnlocked, true));
            Ui.Item(menu, "Lock in Quick Race", () => Apply(RaceFlags.QuickRaceUnlocked, false));
            Ui.Line(menu);
        }
        Ui.Item(menu, "Select All", SelectAll, _list.Items.Count > 0);
    }

    private void RefreshGroups()
    {
        var current = (_groups.SelectedItem as GroupChoice)?.Name ?? AllGroups;
        var races = File!.Races.Races;
        _groups.BeginUpdate();
        _groups.Items.Clear();
        _groups.Items.Add(Choice(AllGroups, races));
        foreach (var group in races.GroupBy(r => Career.RaceGroupName(r.Group)).OrderBy(g => g.Key == "Other" ? 1 : 0))
        {
            _groups.Items.Add(Choice(group.Key, group));
        }
        _groups.EndUpdate();
        _groups.SelectedItem = _groups.Items.Cast<GroupChoice>().FirstOrDefault(g => g.Name == current) ?? _groups.Items[0];
    }

    private static GroupChoice Choice(string name, IEnumerable<RaceRecord> races)
    {
        var list = races.ToList();
        return new GroupChoice(name, list.Count(r => r.Has(RaceFlags.CareerCompleted) || r.Has(RaceFlags.QuickRaceCompleted)), list.Count);
    }

    private void Fill()
    {
        if (File is null)
        {
            return;
        }
        var group = (_groups.SelectedItem as GroupChoice)?.Name ?? AllGroups;
        var selected = _list.SelectedItems.Cast<ListViewItem>().Select(i => ((RaceRecord)i.Tag!).Id).ToHashSet();
        var top = _list.TopItem?.Index ?? 0;
        _list.BeginUpdate();
        _list.Items.Clear();
        foreach (var race in File.Races.Races.Where(r => group == AllGroups || Career.RaceGroupName(r.Group) == group))
        {
            var item = new ListViewItem(race.Id) { Tag = race };
            item.SubItems.Add(Career.RaceGroupName(race.Group));
            item.SubItems.Add(YesNo(race.Has(RaceFlags.CareerCompleted)));
            item.SubItems.Add(YesNo(race.Has(RaceFlags.QuickRaceCompleted)));
            item.SubItems.Add(YesNo(race.Has(RaceFlags.QuickRaceUnlocked)));
            _list.Items.Add(item);
            item.Selected = selected.Contains(race.Id);
        }
        _list.EndUpdate();
        if (top > 0 && top < _list.Items.Count)
        {
            _list.TopItem = _list.Items[top];
        }
        ShowSelection();
    }

    private void SelectAll()
    {
        _list.BeginUpdate();
        foreach (ListViewItem item in _list.Items)
        {
            item.Selected = true;
        }
        _list.EndUpdate();
        _list.Focus();
        ShowSelection();
    }

    private void ShowSelection()
    {
        var count = _list.SelectedItems.Count;
        _selection.Text = count == 0 ? "None selected" : $"{count} selected";
        foreach (var button in _actions)
        {
            button.Enabled = count > 0;
        }
    }

    private List<ListViewItem> AllowedCareerMarks(List<ListViewItem> items)
    {
        var progress = File!.Progress;
        var held = items.Select(i => (RaceRecord)i.Tag!).Where(r => progress.HeldBackName(r) is not null).ToList();
        var allowed = items.Where(i => progress.HeldBackName((RaceRecord)i.Tag!) is null).ToList();
        var ahead = allowed.Count(i => progress.IsAheadOfRank((RaceRecord)i.Tag!));
        if (held.Count == 0 && ahead == 0)
        {
            return allowed;
        }
        var notes = new List<string>();
        if (ahead > 0)
        {
            var rank = File.Career.Rank;
            var where = rank == Career.PrologueRank ? "the prologue" : $"Blacklist #{rank}";
            var move = progress.CanChangeRank ? " To move up properly, change your rank on the Blacklist tab." : "";
            notes.Add($"{ahead} of these events are ahead of where this career is ({where}). Marking them complete won't change your rank and can confuse career progress.{move}");
        }
        if (held.Count > 0)
        {
            var names = string.Join("\n", held.Select(r => $"{r.Id}  {progress.HeldBackName(r)}"));
            notes.Add($"These will be left out, because marking them now can leave the save stuck on the loading screen:\n\n{names}");
        }
        if (allowed.Count == 0)
        {
            MessageBox.Show(this, string.Join("\n\n", notes), "Mark complete", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return allowed;
        }
        notes.Add(allowed.Count == 1 ? "Mark the other event complete?" : $"Mark the other {allowed.Count} events complete?");
        var answer = MessageBox.Show(this, string.Join("\n\n", notes), "Mark complete", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
        return answer == DialogResult.Yes ? allowed : [];
    }

    private void Apply(RaceFlags flags, bool on)
    {
        var items = _list.SelectedItems.Cast<ListViewItem>().ToList();
        if (on && flags.HasFlag(RaceFlags.CareerCompleted))
        {
            items = AllowedCareerMarks(items);
        }
        if (items.Count == 0)
        {
            return;
        }
        foreach (var item in items)
        {
            var race = (RaceRecord)item.Tag!;
            race.Set(flags, on);
            item.SubItems[2].Text = YesNo(race.Has(RaceFlags.CareerCompleted));
            item.SubItems[3].Text = YesNo(race.Has(RaceFlags.QuickRaceCompleted));
            item.SubItems[4].Text = YesNo(race.Has(RaceFlags.QuickRaceUnlocked));
        }
        Bound(RefreshGroups);
        if (flags.HasFlag(RaceFlags.CareerCompleted))
        {
            File!.Progress.RecountRaceWins(items.Select(i => (RaceRecord)i.Tag!));
            MarkBulkEdited();
            return;
        }
        MarkEdited();
    }

    private static string YesNo(bool value) => value ? "Yes" : "No";

    private sealed record GroupChoice(string Name, int Done, int Total)
    {
        public override string ToString() => $"{Name}  ({Done}/{Total})";
    }
}
