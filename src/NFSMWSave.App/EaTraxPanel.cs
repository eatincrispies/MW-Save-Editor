using NFSMWSave.Core;

namespace NFSMWSave.App;

internal sealed class EaTraxPanel : Panel
{
    private const int PlayColumn = 3;

    private readonly ComboBox _playMode = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = Ui.S(175), Margin = new Padding(Ui.S(2)) };
    private readonly ListView _list = Ui.List();
    private readonly Label _note = Ui.Note(600);
    private ComboBox? _editor;
    private SaveFile? _file;
    private bool _binding;

    public EaTraxPanel()
    {
        Dock = DockStyle.Fill;
        BackColor = Theme.Back;

        _playMode.Items.AddRange(GameOptions.PlayModes.Cast<object>().ToArray());
        _playMode.SelectedIndexChanged += (_, _) =>
        {
            if (!_binding && _file is not null && _playMode.SelectedIndex >= 0)
            {
                _file.Options.PlayMode = _playMode.SelectedIndex;
                Changed?.Invoke();
            }
        };
        var fields = Ui.Fields(120);
        Ui.Row(fields, "Play Mode", _playMode);
        var hint = Ui.Note(600);
        hint.Text = "Double-click a song's Play setting to change it. Right-click to move songs up or down.";
        var top = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, BackColor = Theme.Back, WrapContents = false };
        top.Controls.AddRange([Ui.Section("EA™ TRAX", fields, 320), Ui.Column(hint, _note)]);

        _list.MultiSelect = true;
        _list.Columns.Add("#", -2, HorizontalAlignment.Right);
        _list.Columns.Add("Song");
        _list.Columns.Add("Artist");
        _list.Columns.Add("Play");
        Ui.FitColumns(_list, 40, 330, 260, 90);
        _list.Dock = DockStyle.Fill;
        _list.MouseDoubleClick += (_, e) => EditPlay(e.Location);
        _list.KeyDown += (_, e) =>
        {
            if (e.Control && e.KeyCode == Keys.A)
            {
                SelectAll();
                e.Handled = true;
            }
        };
        _list.ColumnWidthChanging += (_, _) => CloseEditor();
        Ui.RightClick(_list, BuildMenu);

        var middle = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Back, Padding = new Padding(0, Ui.S(4), 0, 0) };
        middle.Controls.Add(_list);
        Controls.Add(middle);
        Controls.Add(top);
    }

    public event Action? Changed;

    public void ShowSave(SaveFile file)
    {
        CloseEditor();
        var options = file.Options;
        _file = file;
        _binding = true;
        _playMode.Enabled = options.HasPlayMode;
        _playMode.SelectedIndex = options.HasPlayMode ? Math.Clamp(options.PlayMode, 0, GameOptions.PlayModes.Count - 1) : -1;
        _binding = false;
        var readable = options.HasPlaylist;
        _note.Text = readable ? "" : "This save's playlist couldn't be read, so it can't be changed here.";
        _note.Visible = !readable;
        _list.Enabled = readable;
        Fill();
    }

    private void Fill()
    {
        if (_file is null)
        {
            return;
        }
        var selected = Selected().ToHashSet();
        var top = _list.TopItem?.Index ?? 0;
        _list.BeginUpdate();
        _list.Items.Clear();
        if (_file.Options.HasPlaylist)
        {
            var entries = _file.Options.Playlist;
            for (var i = 0; i < entries.Count; i++)
            {
                var song = GameOptions.Songs[entries[i].Song];
                var item = new ListViewItem((i + 1).ToString("00")) { Tag = entries[i].Song };
                item.SubItems.Add(song.Title);
                item.SubItems.Add(song.Artist);
                item.SubItems.Add(GameOptions.SongPlay[entries[i].Play]);
                _list.Items.Add(item);
                item.Selected = selected.Contains(entries[i].Song);
            }
        }
        _list.EndUpdate();
        if (top > 0 && top < _list.Items.Count)
        {
            _list.TopItem = _list.Items[top];
        }
    }

    private List<int> Selected() => _list.SelectedItems.Cast<ListViewItem>().Select(i => (int)i.Tag!).ToList();

    private void SelectAll()
    {
        _list.BeginUpdate();
        foreach (ListViewItem item in _list.Items)
        {
            item.Selected = true;
        }
        _list.EndUpdate();
        _list.Focus();
    }

    private void EditPlay(Point location)
    {
        var hit = _list.HitTest(location);
        if (_file is null || hit.Item is null || hit.SubItem is null || hit.Item.SubItems.IndexOf(hit.SubItem) != PlayColumn)
        {
            return;
        }
        CloseEditor();
        var song = (int)hit.Item.Tag!;
        var current = _file.Options.Playlist.First(e => e.Song == song).Play;
        var editor = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Bounds = hit.SubItem.Bounds, IntegralHeight = false };
        editor.Items.AddRange(GameOptions.SongPlay.Cast<object>().ToArray());
        editor.SelectedIndex = current;
        editor.SelectionChangeCommitted += (_, _) =>
        {
            var chosen = editor.SelectedIndex;
            BeginInvoke(() =>
            {
                CloseEditor();
                SetPlay([song], chosen);
            });
        };
        editor.DropDownClosed += (_, _) => BeginInvoke(CloseEditor);
        editor.LostFocus += (_, _) => BeginInvoke(CloseEditor);
        _editor = editor;
        _list.Controls.Add(editor);
        editor.Focus();
        editor.DroppedDown = true;
    }

    private void CloseEditor()
    {
        if (_editor is null)
        {
            return;
        }
        var editor = _editor;
        _editor = null;
        _list.Controls.Remove(editor);
        editor.Dispose();
    }

    private void SetPlay(IReadOnlyCollection<int> songs, int play)
    {
        if (_file is null || songs.Count == 0)
        {
            return;
        }
        var chosen = songs.ToHashSet();
        var entries = _file.Options.Playlist.Select(e => chosen.Contains(e.Song) ? e with { Play = play } : e).ToList();
        Save(entries);
    }

    private void Move(int direction)
    {
        if (_file is null)
        {
            return;
        }
        var chosen = Selected().ToHashSet();
        var entries = _file.Options.Playlist.ToList();
        var order = direction < 0 ? Enumerable.Range(0, entries.Count) : Enumerable.Range(0, entries.Count).Reverse();
        foreach (var i in order)
        {
            var target = i + direction;
            if (chosen.Contains(entries[i].Song) && target >= 0 && target < entries.Count && !chosen.Contains(entries[target].Song))
            {
                (entries[i], entries[target]) = (entries[target], entries[i]);
            }
        }
        Save(entries);
    }

    private void Save(List<PlaylistEntry> entries)
    {
        _file!.Options.SetPlaylist(entries);
        Fill();
        Changed?.Invoke();
    }

    private void BuildMenu(ContextMenuStrip menu, ListViewItem? hit)
    {
        if (_file is null || !_file.Options.HasPlaylist)
        {
            return;
        }
        if (hit is not null)
        {
            for (var i = 0; i < GameOptions.SongPlay.Count; i++)
            {
                var value = i;
                Ui.Item(menu, $"Play: {GameOptions.SongPlay[i]}", () => SetPlay(Selected(), value));
            }
            Ui.Line(menu);
            Ui.Item(menu, "Move Up", () => Move(-1));
            Ui.Item(menu, "Move Down", () => Move(1));
            Ui.Line(menu);
        }
        Ui.Item(menu, "Select All", SelectAll, _list.Items.Count > 0);
    }
}
