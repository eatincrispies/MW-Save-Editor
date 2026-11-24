using System.Globalization;
using NFSMWSave.Core;

namespace NFSMWSave.App;

internal sealed class RapSheetPage : EditorPage
{
    private const int HeaderLabelWidth = 135;
    private const int HeaderValueWidth = 190;
    private const int SectionWidth = 380;
    private const int LabelWidth = 200;

    private readonly Label _caseFile = Ui.Text("");
    private readonly Label _name = Value();
    private readonly Label _bounty = Value();
    private readonly Label _costToState = Value();
    private readonly Label _finesDue = Value();
    private readonly Label _impounded = Value();
    private readonly Label _evaded = Value();
    private readonly Label _monitored = Value();
    private readonly Label _busted = Value();
    private readonly Label _unserved = Value();
    private readonly Label _served = Value();
    private readonly Label _timesEvaded = Value();
    private readonly Label _timesBusted = Value();
    private readonly Label _summaryFinesDue = Value();
    private readonly Label _finesPaid = Value();
    private readonly Label _damaged = Value();
    private readonly Label _immobilized = Value();
    private readonly Label _preferred = Ui.Text("");
    private readonly Label _recent = Ui.Text("");
    private readonly Label _infractionTotals = Ui.Text("");
    private readonly Label _costTotal = Value();
    private readonly Label _noPursuits = Ui.Note(500);
    private readonly Label _note = Ui.Note(600);
    private readonly ListView _vehicles = Ui.List();
    private readonly ListView _infractions = Ui.List();
    private readonly ListView _pursuits = Ui.List();
    private readonly ListView _rankings = Ui.List();
    private readonly List<(StatField Field, NumericUpDown Control)> _fields = [];
    private readonly List<(StatField Field, NumericUpDown Control)> _costFields = [];
    private readonly List<(RapSheetCostKey Key, Label Value)> _costValues = [];

    public RapSheetPage()
    {
        var left = Ui.Fields(HeaderLabelWidth);
        HeaderRow(left, "Name", _name);
        HeaderRow(left, "Cost to State", _costToState);
        HeaderRow(left, "Cars Impounded", _impounded);
        HeaderRow(left, "Cars Monitored", _monitored);
        var right = Ui.Fields(HeaderLabelWidth);
        HeaderRow(right, "Bounty", _bounty);
        HeaderRow(right, "Fines Due", _finesDue);
        HeaderRow(right, "Pursuits Evaded", _evaded);
        HeaderRow(right, "Busted", _busted);
        _caseFile.Margin = new Padding(Ui.S(2), Ui.S(2), Ui.S(10), Ui.S(4));
        var header = Ui.Section("Rap Sheet", Ui.Column(_caseFile, Ui.Across(left, right)), 680);
        var top = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, BackColor = Theme.Back, WrapContents = false };
        top.Controls.AddRange([header, _note]);

        var sections = new DarkTabControl { Dock = DockStyle.Fill };
        sections.TabPages.AddRange([
            Tab("Summary", BuildSummary()),
            Tab("Vehicle Database", BuildVehicles()),
            Tab("Infractions", BuildInfractions()),
            Tab("Cost to State", BuildCosts()),
            Tab("Top 5 Pursuits", BuildPursuits()),
            Tab("Rankings", BuildRankings()),
        ]);
        Controls.Add(sections);
        Controls.Add(top);
    }

    public override string Title => "Rap Sheet";
    public override string IconName => "tab_rapsheet";

    private static Label Value() => new() { AutoSize = false, Width = Ui.S(HeaderValueWidth), Height = Ui.S(24), TextAlign = ContentAlignment.MiddleLeft, Margin = new Padding(Ui.S(2)) };

    private static void HeaderRow(TableLayoutPanel table, string text, Label value)
    {
        var row = table.RowCount++;
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var label = new Label { Text = text, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(Ui.S(2), Ui.S(2), Ui.S(10), Ui.S(2)) };
        value.Height = Ui.S(20);
        value.Margin = new Padding(Ui.S(2), Ui.S(1), Ui.S(2), Ui.S(1));
        value.Anchor = AnchorStyles.Left;
        table.Controls.Add(label, 0, row);
        table.Controls.Add(value, 1, row);
    }

    private static TabPage Tab(string title, Control content)
    {
        var page = new TabPage(title) { BackColor = Theme.Back, UseVisualStyleBackColor = false, Padding = new Padding(Ui.S(4)) };
        page.Controls.Add(content);
        return page;
    }

    private static string N(long value) => value.ToString("N0", CultureInfo.InvariantCulture);

    private Control BuildSummary()
    {
        var record = Ui.Fields(LabelWidth);
        Ui.Row(record, "Unserved Infractions", _unserved);
        Ui.Row(record, "Served Infractions", _served);
        Ui.Row(record, "Times Evaded", _timesEvaded);
        Ui.Row(record, "Times Busted", _timesBusted);
        Ui.Row(record, "Fines Due", _summaryFinesDue);
        Ui.Row(record, "Fines Paid", _finesPaid);
        Ui.Row(record, "Police Vehicles Damaged", _damaged);
        Ui.Row(record, "Police Vehicles Immobilized", _immobilized);
        var totals = Ui.Fields(LabelWidth);
        foreach (var field in RapSheet.PursuitFields)
        {
            var max = field.IsTime ? int.MaxValue / RapSheet.TicksPerSecond : int.MaxValue;
            var control = Ui.Number(0, max, 130);
            Ui.Row(totals, field.IsTime ? field.Name + " (seconds)" : field.Name, control);
            _fields.Add((field, control));
            OnNumber(control, v => Change(field, field.IsTime ? (int)v * RapSheet.TicksPerSecond : (int)v));
        }
        return Ui.Page(Ui.Section("Record", record, SectionWidth), Ui.Section("Lifetime Pursuit Totals", totals, SectionWidth));
    }

    private Control BuildVehicles()
    {
        _vehicles.Columns.Add("Vehicle");
        _vehicles.Columns.Add("Bounty", -2, HorizontalAlignment.Right);
        _vehicles.Columns.Add("Fines Due", -2, HorizontalAlignment.Right);
        _vehicles.Columns.Add("Unserved", -2, HorizontalAlignment.Right);
        _vehicles.Columns.Add("Evaded", -2, HorizontalAlignment.Right);
        _vehicles.Columns.Add("Busted", -2, HorizontalAlignment.Right);
        _vehicles.Columns.Add("Status");
        Ui.FitColumns(_vehicles, 230, 110, 85, 75, 70, 65, 150);
        return Listing(_vehicles, Ui.Column(_preferred, _recent));
    }

    private Control BuildInfractions()
    {
        _infractions.Columns.Add("Infraction");
        _infractions.Columns.Add("Unserved", -2, HorizontalAlignment.Right);
        _infractions.Columns.Add("Served", -2, HorizontalAlignment.Right);
        _infractions.Columns.Add("Total", -2, HorizontalAlignment.Right);
        _infractions.Columns.Add("Fine Each", -2, HorizontalAlignment.Right);
        Ui.FitColumns(_infractions, 230, 85, 85, 85, 85);
        var hint = Ui.Note(600);
        hint.Text = "Change infractions for each car in Cars > Police.";
        return Listing(_infractions, Ui.Column(_infractionTotals, hint));
    }

    private Control BuildCosts()
    {
        var table = Ui.Table(3);
        Heading(table, "Category", "Times", "Value");
        var lines = RapSheetReport.CostLines;
        for (var i = 0; i < lines.Count; i++)
        {
            var row = lines[i];
            var line = table.RowCount++;
            table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            table.Controls.Add(Ui.Text(row.Name), 0, line);
            var times = Ui.Number(0, int.MaxValue, 120);
            var timesField = row.TimesField;
            OnNumber(times, v => Change(timesField, (int)v));
            _costFields.Add((timesField, times));
            table.Controls.Add(times, 1, line);
            if (row.ValueField is { } valueField)
            {
                var value = Ui.Number(0, int.MaxValue, 140);
                OnNumber(value, v => Change(valueField, (int)v));
                _costFields.Add((valueField, value));
                table.Controls.Add(value, 2, line);
            }
            else
            {
                var value = Value();
                value.TextAlign = ContentAlignment.MiddleRight;
                value.Width = Ui.S(140);
                _costValues.Add((new RapSheetCostKey(i), value));
                table.Controls.Add(value, 2, line);
            }
        }
        var total = table.RowCount++;
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        table.Controls.Add(Ui.Text("Total"), 0, total);
        _costTotal.TextAlign = ContentAlignment.MiddleRight;
        _costTotal.Width = Ui.S(140);
        table.Controls.Add(_costTotal, 2, total);
        var hint = Ui.Note(520);
        hint.Text = "Damaged and immobilized police vehicles and helicopters are the same numbers as the Lifetime Pursuit Totals on the Summary tab.";
        return Ui.Page(Ui.Column(Ui.Section("Cost to State", table, 520), hint));
    }

    private Control BuildPursuits()
    {
        foreach (var (title, right) in new[] { ("Pursuit ID", false), ("Car Used", false), ("Bounty", true), ("Length", true), ("Cops", true), ("Damaged", true), ("Immobilized", true), ("Roadblocks", true), ("Spike Strips", true), ("Cost to State", true), ("Infractions", true), ("Helicopters", true) })
        {
            _pursuits.Columns.Add(title, -2, right ? HorizontalAlignment.Right : HorizontalAlignment.Left);
        }
        Ui.FitColumns(_pursuits, 108, 158, 76, 52, 44, 60, 74, 72, 74, 84, 66, 74);
        _noPursuits.Text = "No evaded pursuits on record yet.";
        return Listing(_pursuits, _noPursuits);
    }

    private Control BuildRankings()
    {
        _rankings.Columns.Add("Category");
        _rankings.Columns.Add("Lifetime", -2, HorizontalAlignment.Right);
        _rankings.Columns.Add("Lifetime Rank", -2, HorizontalAlignment.Right);
        _rankings.Columns.Add("Single Pursuit", -2, HorizontalAlignment.Right);
        _rankings.Columns.Add("Single Pursuit Rank", -2, HorizontalAlignment.Right);
        Ui.FitColumns(_rankings, 200, 120, 105, 120, 140);
        var hint = Ui.Note(600);
        hint.Text = "Ranks go from #1 to #15, worked out the same way as the game's Rankings screen.";
        return Listing(_rankings, hint);
    }

    private static Panel Listing(ListView list, Control top)
    {
        var panel = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Back };
        list.Dock = DockStyle.Fill;
        var holder = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, BackColor = Theme.Back };
        holder.Controls.Add(top);
        panel.Controls.Add(list);
        panel.Controls.Add(holder);
        return panel;
    }

    private static void Heading(TableLayoutPanel table, params string[] titles)
    {
        var line = table.RowCount++;
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        for (var i = 0; i < titles.Length; i++)
        {
            var label = Ui.Text(titles[i]);
            label.ForeColor = Theme.Muted;
            if (i > 0)
            {
                label.Anchor = AnchorStyles.Right;
            }
            table.Controls.Add(label, i, line);
        }
    }

    private void Change(StatField field, int value)
    {
        File!.Stats.Set(field, value);
        Bound(Populate);
    }

    protected override void Populate()
    {
        var save = File!;
        var available = save.Stats.IsAvailable;
        var report = new RapSheetReport(save);
        _note.Text = available ? "" : "Pursuit stats aren't available for this PS2 save version, so only car records are shown.";
        _note.Visible = !available;

        _caseFile.Text = $"Case File: {report.CaseFile}";
        _name.Text = report.Name;
        _bounty.Text = N(report.Bounty);
        _costToState.Text = N(report.CostToState);
        _finesDue.Text = N(report.FinesDue);
        _impounded.Text = N(report.Impounded);
        _evaded.Text = N(report.Evaded);
        _monitored.Text = N(report.Monitored);
        _busted.Text = N(report.Busted);

        _unserved.Text = N(report.Unserved);
        _served.Text = N(report.Served);
        _timesEvaded.Text = N(report.Evaded);
        _timesBusted.Text = N(report.Busted);
        _summaryFinesDue.Text = N(report.FinesDue);
        _finesPaid.Text = N(report.FinesPaid);
        _damaged.Text = N(report.CopsDamaged);
        _immobilized.Text = N(report.CopsImmobilized);
        foreach (var (field, control) in _fields.Concat(_costFields))
        {
            var value = save.Stats.Get(field);
            Ui.Show(control, field.IsTime ? value / RapSheet.TicksPerSecond : value);
            control.Enabled = available;
        }
        var costs = report.Costs;
        foreach (var (key, label) in _costValues)
        {
            label.Text = N(costs[key.Index].Value);
        }
        _costTotal.Text = N(costs.Sum(c => c.Value));

        _preferred.Text = $"Preferred Vehicle: {report.PreferredVehicle}";
        _recent.Text = $"Recent Vehicle: {report.RecentVehicle}";
        Fill(_vehicles, report.Vehicles.Select(v => new[] { v.Name, N(v.Bounty), N(v.FinesDue), N(v.Unserved), N(v.Evaded), N(v.Busted), v.Status }));

        var infractions = report.Infractions;
        _infractionTotals.Text = $"Unserved: {N(report.Unserved)}    Served: {N(report.Served)}    Fines Due: {N(report.FinesDue)}    Fines Paid: {N(report.FinesPaid)}";
        Fill(_infractions, infractions.Select(i => new[] { i.Name, N(i.Unserved), N(i.Served), N(i.Unserved + i.Served), N(i.Fine) }));

        var pursuits = report.Pursuits;
        Fill(_pursuits, pursuits.Select(p => new[] { p.Id, p.Car, N(p.Bounty), p.Length, N(p.Cops), N(p.Damaged), N(p.Destroyed), N(p.Roadblocks), N(p.Spikes), N(p.CostToState), N(p.Infractions), N(p.Helicopters) }));
        _noPursuits.Visible = pursuits.Count == 0;

        Fill(_rankings, available ? report.Rankings.Select(r => new[] { r.Name, r.Lifetime, r.LifetimeRank, r.Single, r.SingleRank }) : []);
    }

    private static void Fill(ListView list, IEnumerable<string[]> rows)
    {
        list.BeginUpdate();
        list.Items.Clear();
        foreach (var row in rows)
        {
            list.Items.Add(new ListViewItem(row));
        }
        list.EndUpdate();
    }

    private readonly record struct RapSheetCostKey(int Index);
}
