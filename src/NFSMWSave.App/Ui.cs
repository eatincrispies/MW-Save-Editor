using System.Reflection;

namespace NFSMWSave.App;

internal static class Assets
{
    private static readonly Dictionary<string, Image?> Cache = [];

    public static Image? Icon(string name)
    {
        var key = "white:" + name;
        if (Cache.TryGetValue(key, out var image))
        {
            return image;
        }
        image = Get(name) is { } source ? Theme.White(source) : null;
        Cache[key] = image;
        return image;
    }

    public static Image? Get(string name)
    {
        if (Cache.TryGetValue(name, out var image))
        {
            return image;
        }
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name + ".png");
        image = stream is null ? null : new Bitmap(stream);
        Cache[name] = image;
        return image;
    }
}

internal static class Ui
{
    private static readonly float DpiScale = ReadScale();

    private static float ReadScale()
    {
        using var graphics = Graphics.FromHwnd(IntPtr.Zero);
        return graphics.DpiX / 96f;
    }

    public static int S(int pixels) => (int)Math.Round(pixels * DpiScale);

    public static TableLayoutPanel Table(int columns)
    {
        var table = new TableLayoutPanel { ColumnCount = columns, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, BackColor = Theme.Back, Margin = new Padding(0) };
        for (var i = 0; i < columns; i++)
        {
            table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        }
        return table;
    }

    public static GroupBox Group(string title, Control content)
    {
        var group = new DarkGroupBox { Text = title, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, BackColor = Theme.Back, Padding = new Padding(S(8), S(4), S(8), S(6)), Margin = new Padding(S(4)) };
        content.Dock = DockStyle.Fill;
        group.Controls.Add(content);
        return group;
    }

    public static GroupBox Section(string title, Control content, int width)
    {
        var group = Group(title, content);
        group.MinimumSize = new Size(S(width), 0);
        return group;
    }

    public static TableLayoutPanel Fields(int labelWidth)
    {
        var table = Table(2);
        table.ColumnStyles[0] = new ColumnStyle(SizeType.Absolute, S(labelWidth));
        return table;
    }

    public static void Span(TableLayoutPanel table, Control control)
    {
        var row = table.RowCount++;
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        table.Controls.Add(control, 0, row);
        table.SetColumnSpan(control, table.ColumnCount);
    }

    public static FlowLayoutPanel Column(params Control[] controls)
    {
        var column = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = false, BackColor = Theme.Back, Margin = new Padding(0) };
        column.Controls.AddRange(controls);
        return column;
    }

    public static FlowLayoutPanel Across(params Control[] controls)
    {
        var row = new FlowLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = false, BackColor = Theme.Back, Margin = new Padding(0) };
        row.Controls.AddRange(controls);
        return row;
    }

    public static FlowLayoutPanel Page(params Control[] columns)
    {
        var page = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Theme.Back };
        page.Controls.AddRange(columns);
        return page;
    }

    public static Label Note(int width) => new() { AutoSize = true, MaximumSize = new Size(S(width), 0), ForeColor = Theme.Muted, Margin = new Padding(S(2), S(3), S(2), S(3)) };

    public static Label Text(string text) => new() { Text = text, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(S(2), S(5), S(10), S(5)) };

    public static void Row(TableLayoutPanel table, string label, Control control)
    {
        var row = table.RowCount++;
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        table.Controls.Add(Text(label), 0, row);
        control.Anchor = AnchorStyles.Left;
        table.Controls.Add(control, 1, row);
    }

    public static NumericUpDown Number(decimal min, decimal max, int width = 120, int decimals = 0, decimal? step = null) => new()
    {
        Minimum = min,
        Maximum = max,
        DecimalPlaces = decimals,
        Increment = step ?? 1,
        ThousandsSeparator = decimals == 0 && max > 9999,
        Width = S(width),
        TextAlign = HorizontalAlignment.Right,
        Margin = new Padding(S(2)),
    };

    public static void Show(NumericUpDown control, decimal value) => control.Value = Math.Clamp(value, control.Minimum, control.Maximum);

    public static Button Action(string text, Action action, int width = 0)
    {
        var button = new Button { Text = text, AutoSize = width == 0, AutoSizeMode = AutoSizeMode.GrowOnly, UseVisualStyleBackColor = true, Margin = new Padding(S(2), S(3), S(4), S(3)), MinimumSize = new Size(S(width > 0 ? width : 75), S(25)) };
        if (width > 0)
        {
            button.Width = S(width);
        }
        button.Click += (_, _) => action();
        return button;
    }

    public static CheckBox Check(string text) => new() { Text = text, AutoSize = true, Margin = new Padding(S(2), S(3), S(10), S(3)), UseVisualStyleBackColor = false, BackColor = Theme.Back };

    public static PictureBox Picture(int width, int height) => new() { Width = S(width), Height = S(height), SizeMode = PictureBoxSizeMode.Zoom, BackColor = Theme.Back, Margin = new Padding(S(2)) };

    public static ListView List()
    {
        var list = new DarkListView { View = View.Details, FullRowSelect = true, HideSelection = false, GridLines = false, MultiSelect = false, BackColor = Theme.Back };
        return list;
    }

    public static void DrawSwatch(DrawItemEventArgs e, System.Collections.IList items)
    {
        e.DrawBackground();
        if (e.Index >= 0 && items[e.Index] is NFSMWSave.Core.PaintSwatch swatch)
        {
            var chip = new Rectangle(e.Bounds.X + S(3), e.Bounds.Y + S(2), S(26), e.Bounds.Height - S(4));
            using (var brush = new SolidBrush(Color.FromArgb(swatch.R, swatch.G, swatch.B)))
            {
                e.Graphics.FillRectangle(brush, chip);
            }
            e.Graphics.DrawRectangle(Pens.Gray, chip);
            var text = new Rectangle(chip.Right + S(6), e.Bounds.Y, e.Bounds.Width - chip.Width - S(10), e.Bounds.Height);
            TextRenderer.DrawText(e.Graphics, swatch.Name, e.Font, text, e.ForeColor, TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
        }
        e.DrawFocusRectangle();
    }

    public static void RightClick(ListView list, Action<ContextMenuStrip, ListViewItem?> build)
    {
        var menu = new ContextMenuStrip { ShowImageMargin = false };
        var pressed = false;
        ListViewItem? hit = null;
        list.MouseDown += (_, e) =>
        {
            if (e.Button != MouseButtons.Right)
            {
                return;
            }
            pressed = true;
            hit = list.HitTest(e.Location).Item;
            if (hit is not null && !hit.Selected)
            {
                list.SelectedItems.Clear();
                hit.Selected = true;
                hit.Focused = true;
            }
        };
        menu.Opening += (_, e) =>
        {
            menu.Items.Clear();
            var target = pressed ? hit : list.SelectedItems.Count > 0 ? list.FocusedItem is { Selected: true } focused ? focused : list.SelectedItems[0] : null;
            pressed = false;
            build(menu, target);
            e.Cancel = menu.Items.Count == 0;
        };
        list.ContextMenuStrip = menu;
    }

    public static ToolStripMenuItem Item(ContextMenuStrip menu, string text, Action action, bool enabled = true)
    {
        var item = new ToolStripMenuItem(text) { Enabled = enabled };
        item.Click += (_, _) => action();
        menu.Items.Add(item);
        return item;
    }

    public static void Line(ContextMenuStrip menu) => menu.Items.Add(new ToolStripSeparator());

    public static void Commit(Control root)
    {
        foreach (Control child in root.Controls)
        {
            if (child is NumericUpDown number)
            {
                _ = number.Value;
            }
            Commit(child);
        }
    }

    public static void FitColumns(ListView list, params int[] widths)
    {
        for (var i = 0; i < widths.Length && i < list.Columns.Count; i++)
        {
            list.Columns[i].Width = S(widths[i]);
        }
    }
}
