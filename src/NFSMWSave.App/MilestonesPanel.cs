using NFSMWSave.Core;

namespace NFSMWSave.App;

internal sealed class MilestonesPanel : Panel
{
    private const int AllRanks = 0;

    private readonly ListBox _groups = new() { Dock = DockStyle.Fill, IntegralHeight = false };
    private readonly ListView _list = Ui.List();
    private readonly Label _selection = new() { AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(Ui.S(4), Ui.S(6), Ui.S(8), Ui.S(2)) };
    private readonly Button _complete;
    private readonly Button _notComplete;
    private SaveFile? _file;
    private IReadOnlyList<ChallengeRecord> _challenges = [];
    private bool _binding;

    public MilestonesPanel()
    {
        Dock = DockStyle.Fill;
        BackColor = Theme.Back;

        _list.MultiSelect = true;
        _list.Columns.Add("Challenge");
        _list.Columns.Add("Blacklist");
        _list.Columns.Add("Goal");
        _list.Columns.Add("Status");
        Ui.FitColumns(_list, 180, 130, 230, 95);
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
        _groups.SelectedIndexChanged += (_, _) =>
        {
            if (!_binding)
            {
                Fill();
            }
        };

        var groupsBox = new DarkGroupBox { Text = "Show", Dock = DockStyle.Left, Width = Ui.S(190), BackColor = Theme.Back, Padding = new Padding(Ui.S(6), Ui.S(4), Ui.S(6), Ui.S(6)) };
        groupsBox.Controls.Add(_groups);

        _complete = Ui.Action("Mark complete", () => Apply(true), 110);
        _notComplete = Ui.Action("Mark not complete", () => Apply(false), 125);
        var picking = Ui.Column(Ui.Action("Select all", SelectAll, 85), _selection);
        var bar = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, WrapContents = true, BackColor = Theme.Back };
        bar.Controls.AddRange([picking, Ui.Group("Milestones", Ui.Across(_complete, _notComplete))]);

        var right = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Back, Padding = new Padding(Ui.S(6), Ui.S(6), 0, 0) };
        right.Controls.Add(_list);
        right.Controls.Add(bar);
        Controls.Add(right);
        Controls.Add(groupsBox);
    }

    public event Action? Changed;

    public void ShowSave(SaveFile file)
    {
        var rank = ReferenceEquals(_file, file)
            ? (_groups.SelectedItem as GroupChoice)?.Rank ?? AllRanks
            : file.Career.Rank is >= Career.FirstRank and <= Career.LastRank ? file.Career.Rank : AllRanks;
        _file = file;
        _challenges = file.Progress.Challenges;
        _binding = true;
        _groups.BeginUpdate();
        _groups.Items.Clear();
        _groups.Items.Add(Choice(AllRanks, "All ranks", _challenges));
        foreach (var rival in GameData.Rivals)
        {
            _groups.Items.Add(Choice(rival.Rank, $"Blacklist #{rival.Rank}", _challenges.Where(c => c.Rank == rival.Rank)));
        }
        _groups.EndUpdate();
        _groups.SelectedItem = _groups.Items.Cast<GroupChoice>().First(g => g.Rank == rank);
        _binding = false;
        Fill();
    }

    private static GroupChoice Choice(int rank, string name, IEnumerable<ChallengeRecord> challenges)
    {
        var list = challenges.ToList();
        return new GroupChoice(rank, name, list.Count(c => c.IsDone), list.Count);
    }

    private void Fill()
    {
        if (_file is null)
        {
            return;
        }
        var rank = (_groups.SelectedItem as GroupChoice)?.Rank ?? AllRanks;
        var selected = Selected().Select(c => c.Id).ToHashSet();
        var top = _list.TopItem?.Index ?? 0;
        _list.BeginUpdate();
        _list.Items.Clear();
        foreach (var challenge in _challenges.Where(c => rank == AllRanks || c.Rank == rank))
        {
            var open = _file.Progress.CanEditMilestones(challenge.Rank);
            var item = new ListViewItem(challenge.Name) { Tag = challenge };
            item.SubItems.Add($"#{challenge.Rank} {GameData.Rivals[challenge.Rank - Career.FirstRank].Name}");
            item.SubItems.Add(challenge.Goal);
            item.SubItems.Add(!open ? "Locked" : challenge.IsDone ? "Complete" : "Not complete");
            if (!open)
            {
                item.ForeColor = Theme.Muted;
            }
            _list.Items.Add(item);
            item.Selected = selected.Contains(challenge.Id);
        }
        _list.EndUpdate();
        if (top > 0 && top < _list.Items.Count)
        {
            _list.TopItem = _list.Items[top];
        }
        ShowSelection();
    }

    private List<ChallengeRecord> Selected() => _list.SelectedItems.Cast<ListViewItem>().Select(i => (ChallengeRecord)i.Tag!).ToList();

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
        if (_file is null)
        {
            return;
        }
        var selected = Selected();
        var open = selected.Where(c => _file.Progress.CanEditMilestones(c.Rank)).ToList();
        var locked = selected.Count - open.Count;
        _selection.Text = selected.Count == 0 ? "None selected" : locked > 0 ? $"{selected.Count} selected, {locked} locked" : $"{selected.Count} selected";
        _complete.Enabled = open.Any(c => !c.IsDone);
        _notComplete.Enabled = open.Any(c => c.IsDone);
    }

    private void Apply(bool complete)
    {
        if (_file is null)
        {
            return;
        }
        _file.Progress.SetChallenges(Selected(), complete);
        Changed?.Invoke();
    }

    private void BuildMenu(ContextMenuStrip menu, ListViewItem? hit)
    {
        if (_file is null)
        {
            return;
        }
        if (hit is not null)
        {
            Ui.Item(menu, "Mark Complete", () => Apply(true), _complete.Enabled);
            Ui.Item(menu, "Mark Not Complete", () => Apply(false), _notComplete.Enabled);
            Ui.Line(menu);
        }
        Ui.Item(menu, "Select All", SelectAll, _list.Items.Count > 0);
    }

    private sealed record GroupChoice(int Rank, string Name, int Done, int Total)
    {
        public override string ToString() => $"{Name}  ({Done}/{Total})";
    }
}
