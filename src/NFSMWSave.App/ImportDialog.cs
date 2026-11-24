using NFSMWSave.Core;

namespace NFSMWSave.App;

internal sealed class ImportDialog : Form
{
    private const int TextWidth = 380;

    private readonly CheckBox _alias;
    private readonly CheckBox _money = Ui.Check("Cash and bounty");
    private readonly CheckBox _progress = Ui.Check("Blacklist, races and unlocks");
    private readonly CheckBox _cars;
    private readonly CheckBox _markers = Ui.Check("Markers");
    private readonly CheckBox _rapSheet = Ui.Check("Rap Sheet");
    private readonly CheckBox _options = Ui.Check("Options");

    public ImportDialog(SaveImport source, SaveFile target)
    {
        Text = "Import Save Data";
        Font = new Font("Segoe UI", 9f);
        BackColor = Theme.Back;
        Load += (_, _) => Theme.Apply(this);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;

        _alias = Ui.Check(target.Map.Platform == SavePlatform.PC ? $"Alias: {source.Alias} (renames the PC profile folder)" : $"Alias: {source.Alias}");
        _alias.Enabled = target.CanRenameAlias;
        _cars = Ui.Check($"Cars: {source.CareerCarCount} career, {source.MyCarCount} My Cars (replaces this save's cars)");
        foreach (var box in new[] { _money, _progress, _cars, _markers, _rapSheet, _options })
        {
            box.Checked = true;
        }

        var from = Ui.Fields(70);
        Ui.Row(from, "Profile", Ui.Text(source.Alias));
        Ui.Row(from, "Platform", Ui.Text(source.Platform));
        Ui.Row(from, "Blacklist", Ui.Text(source.RankName));
        Ui.Row(from, "Cash", Ui.Text($"${source.Cash:N0}"));

        var choices = Ui.Column(_alias, _money, _progress, _cars, _markers, _rapSheet, _options);
        var note = Ui.Note(TextWidth);
        note.Text = $"This replaces the chosen data in {target.Career.ProfileName} ({target.PlatformName}). Nothing is written to disk until you save.";

        var import = Ui.Action("Import", Confirm, 80);
        var cancel = Ui.Action("Cancel", () => DialogResult = DialogResult.Cancel, 80);
        AcceptButton = import;
        CancelButton = cancel;
        var buttons = Ui.Across(import, cancel);
        buttons.Anchor = AnchorStyles.Right;

        var layout = Ui.Column(Ui.Section("From File", from, TextWidth), Ui.Section("Import", choices, TextWidth), note, buttons);
        layout.Padding = new Padding(Ui.S(6));
        Controls.Add(layout);
    }

    public ImportSections Sections { get; private set; }

    private void Confirm()
    {
        var sections = ImportSections.None;
        sections |= _alias.Checked ? ImportSections.Alias : 0;
        sections |= _money.Checked ? ImportSections.Money : 0;
        sections |= _progress.Checked ? ImportSections.Progress : 0;
        sections |= _cars.Checked ? ImportSections.Cars : 0;
        sections |= _markers.Checked ? ImportSections.Markers : 0;
        sections |= _rapSheet.Checked ? ImportSections.RapSheet : 0;
        sections |= _options.Checked ? ImportSections.Options : 0;
        if (sections == ImportSections.None)
        {
            MessageBox.Show(this, "Choose at least one thing to import.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        Sections = sections;
        DialogResult = DialogResult.OK;
    }
}
