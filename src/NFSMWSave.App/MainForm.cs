using System.Diagnostics;
using NFSMWSave.Core;

namespace NFSMWSave.App;

public sealed class MainForm : Form
{
    private const string AppName = "NFSMW Save Editor";
    private const string SaveFilter = "All Most Wanted saves|*.*|GameCube save (*.gci)|*.gci|PS2 save (*.psu;*.max;*.cbs;*.psv)|*.psu;*.max;*.cbs;*.psv";
    private readonly DarkTabControl _tabs = new() { Dock = DockStyle.Fill };
    private readonly List<EditorPage> _pages = [new CareerPage(), new BlacklistPage(), new CarsPage(), new RacesPage(), new MarkersPage(), new RapSheetPage(), new OptionsPage()];
    private readonly ToolStripStatusLabel _status = new() { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
    private readonly ToolStripStatusLabel _integrity = new();
    private readonly FlowLayoutPanel _summary = new() { Dock = DockStyle.Top, AutoSize = true, WrapContents = false, BackColor = Theme.Back, Padding = new Padding(Ui.S(6), Ui.S(3), 0, Ui.S(4)) };
    private readonly Label _sumProfile = SummaryValue();
    private readonly Label _sumPlatform = SummaryValue();
    private readonly Label _sumRank = SummaryValue();
    private readonly Label _sumCash = SummaryValue();
    private readonly Label _sumBounty = SummaryValue();
    private readonly Label _sumCar = SummaryValue();
    private readonly ToolStripMenuItem _profiles = new("Open from &game folder");
    private readonly ToolStripMenuItem _save = new("&Save", null, null, Keys.Control | Keys.S);
    private readonly ToolStripMenuItem _saveAs = new("Save &As...", null, null, Keys.Control | Keys.Shift | Keys.S);
    private readonly ToolStripMenuItem _import = new("&Import");
    private readonly ToolStripMenuItem _export = new("&Export");
    private readonly ToolStripMenuItem _verify = new("&Verify Save");
    private readonly ToolStripMenuItem _match = new("&Match My Game's Car List...");
    private readonly HashSet<string> _backedUp = new(StringComparer.OrdinalIgnoreCase);
    private SaveFile? _file;
    private bool _dirty;

    public MainForm(string? path = null)
    {
        Text = AppName;
        BackColor = Theme.Back;
        Load += (_, _) => Theme.Apply(this);
        Font = new Font("Segoe UI", 9f);
        ClientSize = new Size(Ui.S(960), Ui.S(640));
        MinimumSize = Size;
        StartPosition = FormStartPosition.CenterScreen;
        AllowDrop = true;
        using (var iconStream = typeof(MainForm).Assembly.GetManifestResourceStream("app.ico"))
        {
            if (iconStream is not null)
            {
                Icon = new Icon(iconStream);
            }
        }

        var images = new ImageList { ImageSize = new Size(Ui.S(20), Ui.S(20)), ColorDepth = ColorDepth.Depth32Bit };
        foreach (var page in _pages)
        {
            images.Images.Add(page.IconName, Centered(Assets.Icon(page.IconName), images.ImageSize));
            var tab = new TabPage(page.Title) { ImageKey = page.IconName, BackColor = Theme.Back, UseVisualStyleBackColor = false };
            tab.Controls.Add(page);
            _tabs.TabPages.Add(tab);
            page.Edited += MarkDirty;
            page.BulkEdited += () =>
            {
                MarkDirty();
                ReloadPages();
            };
        }
        _tabs.ImageList = images;
        _tabs.Enabled = false;

        var statusStrip = new StatusStrip { SizingGrip = true };
        statusStrip.Items.AddRange([_status, _integrity]);

        _summary.Controls.AddRange([
            SummaryCell("Profile", _sumProfile, 95),
            SummaryCell("Platform", _sumPlatform, 120),
            SummaryCell("Blacklist", _sumRank, 110),
            SummaryCell("Cash", _sumCash, 125),
            SummaryCell("Bounty", _sumBounty, 125),
            SummaryCell("Current car", _sumCar, 260),
        ]);

        var menu = BuildMenu();
        Controls.Add(_tabs);
        Controls.Add(_summary);
        Controls.Add(menu);
        Controls.Add(statusStrip);
        MainMenuStrip = menu;

        DragEnter += (_, e) => e.Effect = e.Data?.GetDataPresent(DataFormats.FileDrop) == true ? DragDropEffects.Copy : DragDropEffects.None;
        DragDrop += (_, e) =>
        {
            if (e.Data?.GetData(DataFormats.FileDrop) is string[] { Length: > 0 } files && ConfirmDiscard())
            {
                OpenFile(files[0]);
            }
        };
        FormClosing += (_, e) => e.Cancel = !ConfirmDiscard();
        UpdateChrome();
        _status.Text = "Open a Most Wanted PC profile to begin.";
        if (path is not null)
        {
            Shown += (_, _) => OpenFile(path);
        }
    }

    public TabControl Tabs => _tabs;

    private static Label SummaryValue() => new() { AutoSize = true, Font = new Font("Segoe UI Semibold", 10f), Margin = new Padding(0) };

    private static Control SummaryCell(string caption, Label value, int width)
    {
        var title = new Label { Text = caption, AutoSize = true, Font = new Font("Segoe UI", 8f), ForeColor = Theme.Muted, Margin = new Padding(0) };
        var cell = Ui.Column(title, value);
        cell.MinimumSize = new Size(Ui.S(width), 0);
        cell.Margin = new Padding(Ui.S(4), 0, Ui.S(4), 0);
        return cell;
    }

    private MenuStrip BuildMenu()
    {
        var menu = new MenuStrip();
        var file = new ToolStripMenuItem("&File");
        var open = new ToolStripMenuItem("&Open...", null, (_, _) => BrowseOpen(), Keys.Control | Keys.O);
        _save.Click += (_, _) => SaveCurrent();
        _saveAs.Click += (_, _) => SaveCurrentAs();
        var exit = new ToolStripMenuItem("E&xit", null, (_, _) => Close());
        _profiles.DropDownOpening += (_, _) => FillProfiles();
        _profiles.DropDownItems.Add(new ToolStripMenuItem("(none)") { Enabled = false });
        _export.DropDownItems.Add(new ToolStripMenuItem("&JSON...", null, (_, _) => ExportSave(ExportFormat.Json)));
        _export.DropDownItems.Add(new ToolStripMenuItem("&XML...", null, (_, _) => ExportSave(ExportFormat.Xml)));
        _import.DropDownItems.Add(new ToolStripMenuItem("&JSON...", null, (_, _) => ImportSave(ExportFormat.Json)));
        _import.DropDownItems.Add(new ToolStripMenuItem("&XML...", null, (_, _) => ImportSave(ExportFormat.Xml)));
        file.DropDownItems.AddRange([open, _profiles, new ToolStripSeparator(), _save, _saveAs, new ToolStripSeparator(), _import, _export, new ToolStripSeparator(), exit]);

        var tools = new ToolStripMenuItem("&Tools");
        _verify.Click += (_, _) => ShowVerify();
        _match.Click += (_, _) => MatchCarList();
        tools.DropDownItems.AddRange([_verify, _match]);

        var help = new ToolStripMenuItem("&Help");
        var howTo = new ToolStripMenuItem("&How to Use");
        foreach (var guide in HowToDialog.Guides)
        {
            howTo.DropDownItems.Add(new ToolStripMenuItem(guide.Platform, null, (_, _) => OpenWindow(new HowToDialog(guide))));
        }
        help.DropDownItems.AddRange([howTo, new ToolStripSeparator(), new ToolStripMenuItem("&About", null, (_, _) => OpenWindow(new AboutDialog(AppName)))]);
        menu.Items.AddRange([file, tools, help]);
        return menu;
    }

    private void OpenWindow(Form window)
    {
        using (window)
        {
            window.ShowDialog(this);
        }
    }

    private static Bitmap Centered(Image? image, Size size)
    {
        var bitmap = new Bitmap(size.Width, size.Height);
        if (image is null)
        {
            return bitmap;
        }
        using var graphics = Graphics.FromImage(bitmap);
        graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
        var scale = Math.Min((float)size.Width / image.Width, (float)size.Height / image.Height);
        var width = image.Width * scale;
        var height = image.Height * scale;
        graphics.DrawImage(image, (size.Width - width) / 2, (size.Height - height) / 2, width, height);
        return bitmap;
    }

    private void FillProfiles()
    {
        _profiles.DropDownItems.Clear();
        var profiles = GameFolder.FindProfiles();
        foreach (var profile in profiles)
        {
            _profiles.DropDownItems.Add(new ToolStripMenuItem(profile.Name, null, (_, _) =>
            {
                if (ConfirmDiscard())
                {
                    OpenFile(profile.Path);
                }
            }));
        }
        if (profiles.Count == 0)
        {
            _profiles.DropDownItems.Add(new ToolStripMenuItem("(none found)") { Enabled = false });
        }
    }

    private void BrowseOpen()
    {
        if (!ConfirmDiscard())
        {
            return;
        }
        using var dialog = new OpenFileDialog { Title = "Open Most Wanted save", Filter = SaveFilter, InitialDirectory = Directory.Exists(GameFolder.Root) ? GameFolder.Root : "" };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            OpenFile(dialog.FileName);
        }
    }

    public bool OpenFile(string path)
    {
        try
        {
            var file = SaveFile.Open(path);
            _file = file;
            _dirty = false;
            foreach (var page in _pages)
            {
                page.Attach(file);
            }
            _tabs.Enabled = true;
            UpdateChrome();
            var report = file.Verify();
            _status.Text = report.AllValid ? $"Opened {path}" : "This save has failed checks. Saving will repair them.";
            WarnIfForeign();
            if (file.OpenNotice is { } notice)
            {
                _status.Text = notice;
            }
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SaveFormatException)
        {
            MessageBox.Show(this, ex.Message, "Could not open file", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }
    }

    private void WarnIfForeign()
    {
        if (_file is null)
        {
            return;
        }
        if (_file.Map.Platform != SavePlatform.PC)
        {
            return;
        }
        var installed = GameFolder.InstalledStamp(_file.Path);
        if (installed is not null && installed != _file.Career.StableStamp)
        {
            _status.Text = "This save was made with a different car list. Use Tools > Match My Game's Car List so your game accepts it.";
        }
    }

    private void MatchCarList()
    {
        if (_file is null)
        {
            return;
        }
        Ui.Commit(this);
        var installed = GameFolder.InstalledStamp(_file.Path);
        if (installed is null)
        {
            MessageBox.Show(this, "No other profiles were found in your game's save folder, so your game's car list stamp is unknown.", "Match car list", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        if (installed == _file.Career.StableStamp)
        {
            MessageBox.Show(this, "This save already matches your game.", "Match car list", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        var answer = MessageBox.Show(this, $"Change this save's car list stamp from {_file.Career.StableStamp:X8} to {installed:X8}?\n\nYour game refuses a save whose stamp differs. Matching it lets the save load, but if your game has car mods the save's garage may not line up with them.", "Match car list", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
        if (answer == DialogResult.Yes)
        {
            _file.Career.StableStamp = installed.Value;
            MarkDirty();
            _status.Text = "Car list stamp updated. Save to apply it.";
        }
    }

    private void ShowVerify()
    {
        if (_file is null)
        {
            return;
        }
        Ui.Commit(this);
        var r = _file.Verify();
        var pending = _dirty ? "\n\nYou have unsaved edits, so the checks below will pass again after saving." : "";
        MessageBox.Show(this, $"File header: {Pass(r.Header)}\nFile info: {Pass(r.Info)}\nProfile data: {Pass(r.Profile)}\nProfile MD5: {Pass(r.ProfileHash)}\nCareer block MD5: {Pass(r.GameplayHash)}{pending}", "Verify save", MessageBoxButtons.OK, r.AllValid ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
    }

    private static string Pass(bool value) => value ? "OK" : "Mismatch";

    private bool SaveCurrent()
    {
        if (_file?.Path is null)
        {
            return SaveCurrentAs();
        }
        return WriteTo(_file.Path);
    }

    private bool SaveCurrentAs()
    {
        if (_file is null)
        {
            return false;
        }
        using var dialog = new SaveFileDialog { Title = "Save as", Filter = SaveFilter, FileName = Path.GetFileName(_file.Path) ?? _file.Career.ProfileName, InitialDirectory = Path.GetDirectoryName(_file.Path) ?? "", AddExtension = false };
        return dialog.ShowDialog(this) == DialogResult.OK && WriteTo(dialog.FileName);
    }

    private void ImportSave(ExportFormat format)
    {
        if (_file is null)
        {
            return;
        }
        Ui.Commit(this);
        var extension = format == ExportFormat.Json ? "json" : "xml";
        var label = extension.ToUpperInvariant();
        using var picker = new OpenFileDialog
        {
            Title = $"Import from {label}",
            Filter = $"{label} file (*.{extension})|*.{extension}",
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        };
        if (picker.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }
        try
        {
            var source = SaveImport.Load(picker.FileName);
            using var dialog = new ImportDialog(source, _file);
            if (dialog.ShowDialog(this) != DialogResult.OK)
            {
                return;
            }
            var report = _file.Import(source, dialog.Sections);
            MarkDirty();
            ReloadPages();
            _status.Text = "Imported. Press Ctrl+S to write it to the save.";
            var notes = report.Notes.Count > 0 ? "\n\n" + string.Join("\n", report.Notes) : "";
            MessageBox.Show(this, $"Import finished. Press Ctrl+S to write it to the save.{notes}", "Import", MessageBoxButtons.OK, report.Notes.Count > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SaveFormatException or InvalidOperationException)
        {
            MessageBox.Show(this, $"{ex.Message}\n\nThe open save was not changed.", "Could not import", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        catch (Exception ex) when (ex is FormatException or OverflowException or ArgumentException)
        {
            MessageBox.Show(this, "This file is missing data or has invalid values.\n\nThe open save was not changed.", "Could not import", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ExportSave(ExportFormat format)
    {
        if (_file is null)
        {
            return;
        }
        Ui.Commit(this);
        var extension = format == ExportFormat.Json ? "json" : "xml";
        var label = extension.ToUpperInvariant();
        using var dialog = new SaveFileDialog
        {
            Title = $"Export as {label}",
            Filter = $"{label} file (*.{extension})|*.{extension}",
            DefaultExt = extension,
            AddExtension = true,
            FileName = $"{_file.Career.ProfileName} {_file.PlatformName}.{extension}",
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }
        try
        {
            SaveExport.Write(_file, format, dialog.FileName);
            _status.Text = $"Exported {dialog.FileName}";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(this, ex.Message, "Could not export", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    public bool WriteTo(string path)
    {
        if (_file is null)
        {
            return false;
        }
        Ui.Commit(this);
        if (_file.Map.Platform == SavePlatform.PC && Process.GetProcessesByName("speed").Length > 0)
        {
            var answer = MessageBox.Show(this, "Most Wanted is running and may overwrite this save. Save anyway?", "Game is running", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (answer != DialogResult.Yes)
            {
                return false;
            }
        }
        try
        {
            var renameTo = GameFolder.RenameTarget(path, _file.OriginalAlias, _file.Career.ProfileName);
            if (renameTo is not null && !string.Equals(Path.GetDirectoryName(renameTo), Path.GetDirectoryName(Path.GetFullPath(path)), StringComparison.OrdinalIgnoreCase) && Directory.Exists(Path.GetDirectoryName(renameTo)))
            {
                throw new IOException($"A profile named {_file.Career.ProfileName} already exists.");
            }
            var backup = _file.Save(path, _backedUp.Add(path));
            if (!SaveFile.Open(path).Verify().AllValid)
            {
                throw new IOException("The written file failed its checks.");
            }
            var finalPath = renameTo is null ? path : GameFolder.MoveProfile(path, _file.Career.ProfileName);
            _file.MarkSavedAt(finalPath);
            _backedUp.Add(finalPath);
            _dirty = false;
            UpdateChrome();
            _status.Text = _file.ResignNotice ?? (backup is null ? $"Saved {finalPath}" : $"Saved. Original backed up to {Path.GetFileName(backup)}");
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SaveFormatException)
        {
            MessageBox.Show(this, ex.Message, "Could not save", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }
    }

    private bool ConfirmDiscard()
    {
        if (!_dirty)
        {
            return true;
        }
        var answer = MessageBox.Show(this, "Save changes to this profile?", AppName, MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
        return answer switch
        {
            DialogResult.Yes => SaveCurrent(),
            DialogResult.No => true,
            _ => false,
        };
    }

    private void MarkDirty()
    {
        _dirty = true;
        UpdateChrome();
    }

    private void ReloadPages()
    {
        foreach (var page in _pages)
        {
            page.Reload();
        }
        UpdateChrome();
    }

    private void UpdateChrome()
    {
        var open = _file is not null;
        _save.Enabled = open;
        _saveAs.Enabled = open;
        _import.Enabled = open;
        _export.Enabled = open;
        _verify.Enabled = open;
        _match.Enabled = open && _file!.Map.Platform == SavePlatform.PC;
        Text = open ? $"{_file!.Career.ProfileName}{(_dirty ? " *" : "")} - {AppName}" : AppName;
        _summary.Visible = open;
        if (open)
        {
            var save = _file!;
            var rival = GameData.Rivals.FirstOrDefault(r => r.Rank == save.Career.Rank);
            _sumProfile.Text = save.Career.ProfileName;
            _sumPlatform.Text = save.PlatformName;
            _sumRank.Text = rival is null ? "Prologue" : $"#{rival.Rank} {rival.Name}";
            _sumCash.Text = $"${save.Career.Cash:N0}";
            _sumBounty.Text = save.Stable.TotalBounty.ToString("N0");
            _sumCar.Text = save.Stable.Cars.FirstOrDefault(c => c.Handle == save.Career.CurrentCar)?.Name ?? "None";
        }
        _integrity.Text = open ? (_dirty ? "Unsaved changes" : _file!.FormatName) : "";
    }
}
