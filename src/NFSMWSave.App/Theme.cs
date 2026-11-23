using System.Drawing.Imaging;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace NFSMWSave.App;

internal static class Theme
{
    private const int DarkPixelLimit = 96;
    private const int DarkShareDivisor = 20;

    public static readonly Color Back = Color.FromArgb(30, 30, 32);
    public static readonly Color Surface = Color.FromArgb(40, 40, 43);
    public static readonly Color Field = Color.FromArgb(51, 51, 55);
    public static readonly Color Hover = Color.FromArgb(66, 66, 72);
    public static readonly Color Line = Color.FromArgb(82, 82, 90);
    public static readonly Color Ink = Color.FromArgb(240, 240, 240);
    public static readonly Color Muted = Color.FromArgb(165, 165, 172);
    public static readonly Color Accent = Color.FromArgb(0, 120, 212);
    public static readonly Color Error = Color.FromArgb(120, 40, 44);

    private static readonly ConditionalWeakTable<Control, object> Hooked = [];

    public static void Install()
    {
#pragma warning disable WFO5001
        Application.SetColorMode(SystemColorMode.Dark);
#pragma warning restore WFO5001
        ToolStripManager.Renderer = new DarkRenderer();
    }

    public static void Apply(Control control)
    {
        Style(control);
        if (Hooked.TryAdd(control, control))
        {
            control.ControlAdded += (_, e) =>
            {
                if (e.Control is not null)
                {
                    Apply(e.Control);
                }
            };
            if (control is Label label)
            {
                label.Paint += PaintDisabledLabel;
            }
            if (control is CheckBox check)
            {
                check.Paint += PaintDisabledCheck;
            }
        }
        foreach (Control child in control.Controls)
        {
            Apply(child);
        }
    }

    private static void PaintDisabledLabel(object? sender, PaintEventArgs e)
    {
        if (sender is not Label { Enabled: false } label)
        {
            return;
        }
        e.Graphics.Clear(label.BackColor);
        TextRenderer.DrawText(e.Graphics, label.Text, label.Font, label.ClientRectangle, label.ForeColor, TextFormatFlags.TextBoxControl | TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
    }

    private static void PaintDisabledCheck(object? sender, PaintEventArgs e)
    {
        if (sender is not CheckBox { Enabled: false } check)
        {
            return;
        }
        var left = CheckBoxRenderer.GetGlyphSize(e.Graphics, System.Windows.Forms.VisualStyles.CheckBoxState.UncheckedDisabled).Width + Ui.S(3);
        var area = new Rectangle(left, 0, Math.Max(check.Width - left, 0), check.Height);
        using (var brush = new SolidBrush(check.BackColor))
        {
            e.Graphics.FillRectangle(brush, area);
        }
        TextRenderer.DrawText(e.Graphics, check.Text, check.Font, area, Ink, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
    }

    private static void Style(Control control)
    {
        switch (control)
        {
            case Form form:
                form.BackColor = Back;
                form.ForeColor = Ink;
                break;
            case Button button:
                button.UseVisualStyleBackColor = false;
                button.FlatStyle = FlatStyle.Flat;
                button.BackColor = Field;
                button.ForeColor = Ink;
                button.FlatAppearance.BorderColor = Line;
                button.FlatAppearance.MouseOverBackColor = Hover;
                button.FlatAppearance.MouseDownBackColor = Accent;
                break;
            case ComboBox combo:
                combo.FlatStyle = FlatStyle.Flat;
                combo.BackColor = Field;
                combo.ForeColor = Ink;
                break;
            case TextBox text:
                text.BorderStyle = BorderStyle.FixedSingle;
                if (text.BackColor != Error)
                {
                    text.BackColor = Field;
                }
                text.ForeColor = Ink;
                break;
            case NumericUpDown number:
                number.BorderStyle = BorderStyle.FixedSingle;
                number.BackColor = Field;
                number.ForeColor = Ink;
                break;
            case ListView list:
                list.BackColor = Surface;
                list.ForeColor = Ink;
                break;
            case CheckBox check:
                check.UseVisualStyleBackColor = false;
                check.BackColor = Back;
                check.ForeColor = Ink;
                break;
            case TrackBar bar:
                bar.BackColor = Back;
                break;
            case ToolStrip strip:
                strip.BackColor = Surface;
                strip.ForeColor = Ink;
                break;
        }
    }

    public static Bitmap White(Image source)
    {
        var bitmap = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppArgb);
        var area = new Rectangle(Point.Empty, bitmap.Size);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.DrawImage(source, area);
        }
        var data = bitmap.LockBits(area, ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
        try
        {
            var pixels = new byte[data.Stride * data.Height];
            Marshal.Copy(data.Scan0, pixels, 0, pixels.Length);
            var visible = 0;
            var dark = 0;
            for (var i = 0; i < pixels.Length; i += 4)
            {
                if (pixels[i + 3] > 128)
                {
                    visible++;
                    dark += Brightness(pixels, i) < DarkPixelLimit ? 1 : 0;
                }
            }
            var lineArt = visible > 0 && dark * DarkShareDivisor >= visible;
            for (var i = 0; i < pixels.Length; i += 4)
            {
                var alpha = pixels[i + 3];
                pixels[i + 3] = lineArt ? (byte)(alpha * (255 - Brightness(pixels, i)) / 255) : alpha;
                pixels[i] = 255;
                pixels[i + 1] = 255;
                pixels[i + 2] = 255;
            }
            Marshal.Copy(pixels, 0, data.Scan0, pixels.Length);
        }
        finally
        {
            bitmap.UnlockBits(data);
        }
        return bitmap;
    }

    private static int Brightness(byte[] pixels, int at) => (pixels[at] + pixels[at + 1] + pixels[at + 2]) / 3;
}

internal sealed class DarkColors : ProfessionalColorTable
{
    public DarkColors() => UseSystemColors = false;

    public override Color MenuStripGradientBegin => Theme.Surface;
    public override Color MenuStripGradientEnd => Theme.Surface;
    public override Color StatusStripGradientBegin => Theme.Surface;
    public override Color StatusStripGradientEnd => Theme.Surface;
    public override Color ToolStripDropDownBackground => Theme.Surface;
    public override Color ToolStripBorder => Theme.Surface;
    public override Color ImageMarginGradientBegin => Theme.Surface;
    public override Color ImageMarginGradientMiddle => Theme.Surface;
    public override Color ImageMarginGradientEnd => Theme.Surface;
    public override Color MenuBorder => Theme.Line;
    public override Color MenuItemBorder => Theme.Line;
    public override Color MenuItemSelected => Theme.Hover;
    public override Color MenuItemSelectedGradientBegin => Theme.Hover;
    public override Color MenuItemSelectedGradientEnd => Theme.Hover;
    public override Color MenuItemPressedGradientBegin => Theme.Field;
    public override Color MenuItemPressedGradientMiddle => Theme.Field;
    public override Color MenuItemPressedGradientEnd => Theme.Field;
    public override Color SeparatorDark => Theme.Line;
    public override Color SeparatorLight => Theme.Surface;
}

internal sealed class DarkRenderer : ToolStripProfessionalRenderer
{
    public DarkRenderer()
        : base(new DarkColors())
    {
        RoundedEdges = false;
    }

    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        e.TextColor = e.Item?.Enabled == false ? Theme.Muted : Theme.Ink;
        base.OnRenderItemText(e);
    }

    protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
    {
        e.ArrowColor = e.Item?.Enabled == false ? Theme.Muted : Theme.Ink;
        base.OnRenderArrow(e);
    }
}

internal sealed class DarkGroupBox : GroupBox
{
    protected override void OnPaint(PaintEventArgs e)
    {
        const TextFormatFlags Flags = TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.NoClipping;
        e.Graphics.Clear(Theme.Back);
        var size = TextRenderer.MeasureText(e.Graphics, Text, Font, Size.Empty, Flags);
        var height = Math.Max(size.Height, Font.Height);
        var top = height / 2;
        using (var pen = new Pen(Theme.Line))
        {
            e.Graphics.DrawRectangle(pen, 0, top, Width - 1, Height - top - 1);
        }
        var caption = new Rectangle(Ui.S(7), 0, size.Width + Ui.S(2), height + Ui.S(1));
        using (var brush = new SolidBrush(Theme.Back))
        {
            e.Graphics.FillRectangle(brush, caption);
        }
        TextRenderer.DrawText(e.Graphics, Text, Font, caption, Theme.Ink, Flags);
    }
}

internal sealed class DarkListView : ListView
{
    private const int ReflectedNotify = 0x204E;
    private const int CustomDrawCode = -12;
    private const uint PrePaint = 1;
    private const uint GroupItem = 1;
    private const int SkipDefault = 4;
    private const int GetGroupInfo = 0x1095;
    private const uint GroupHeader = 1;
    private const int HeaderChars = 128;

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NotifyHeader
    {
        public IntPtr From;
        public UIntPtr Id;
        public int Code;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ListDraw
    {
        public NotifyHeader Header;
        public uint Stage;
        public IntPtr Dc;
        public NativeRect Bounds;
        public UIntPtr ItemSpec;
        public uint ItemState;
        public IntPtr ItemParam;
        public uint TextColor;
        public uint TextBack;
        public int SubItem;
        public uint ItemType;
        public uint Face;
        public int IconEffect;
        public int IconPhase;
        public int PartId;
        public int StateId;
        public NativeRect TextBounds;
        public uint Align;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct GroupInfo
    {
        public int Size;
        public uint Mask;
        public IntPtr Header;
        public int HeaderLength;
        public IntPtr Footer;
        public int FooterLength;
        public int Id;
        public uint StateMask;
        public uint State;
        public uint Align;
    }

    [DllImport("user32.dll", EntryPoint = "SendMessageW")]
    private static extern IntPtr SendGroupMessage(IntPtr window, int message, IntPtr id, ref GroupInfo info);

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == ReflectedNotify && Marshal.PtrToStructure<NotifyHeader>(m.LParam).Code == CustomDrawCode)
        {
            var draw = Marshal.PtrToStructure<ListDraw>(m.LParam);
            if (draw.ItemType == GroupItem && draw.Stage == PrePaint)
            {
                DrawGroup(draw);
                m.Result = SkipDefault;
                return;
            }
        }
        base.WndProc(ref m);
    }

    private void DrawGroup(ListDraw draw)
    {
        var title = GroupTitle((int)draw.ItemSpec.ToUInt32());
        var text = Rectangle.FromLTRB(draw.TextBounds.Left, draw.TextBounds.Top, draw.TextBounds.Right, draw.TextBounds.Bottom);
        if (text.Height <= 0)
        {
            text = new Rectangle(draw.Bounds.Left + Ui.S(6), draw.Bounds.Top, ClientSize.Width, Font.Height + Ui.S(6));
        }
        var row = new Rectangle(0, text.Top, ClientSize.Width, text.Height);
        using var graphics = Graphics.FromHdc(draw.Dc);
        using (var brush = new SolidBrush(BackColor))
        {
            graphics.FillRectangle(brush, row);
        }
        using (var pen = new Pen(Theme.Line))
        {
            graphics.DrawLine(pen, 0, row.Bottom - 1, ClientSize.Width, row.Bottom - 1);
        }
        TextRenderer.DrawText(graphics, title, Font, text, Theme.Ink, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
    }

    private string GroupTitle(int id)
    {
        var buffer = Marshal.AllocHGlobal(HeaderChars * 2);
        try
        {
            var info = new GroupInfo { Size = Marshal.SizeOf<GroupInfo>(), Mask = GroupHeader, Header = buffer, HeaderLength = HeaderChars };
            return SendGroupMessage(Handle, GetGroupInfo, id, ref info) == -1 ? "" : Marshal.PtrToStringUni(buffer) ?? "";
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }
}

internal sealed class DarkTabControl : TabControl
{
    private const int WmSetFont = 0x30;

    private IntPtr _font;

    public DarkTabControl()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
    }

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr window, int message, IntPtr wParam, IntPtr lParam);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr handle);

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        ApplyFont();
    }

    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        if (IsHandleCreated)
        {
            ApplyFont();
        }
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        ReleaseFont();
    }

    private void ApplyFont()
    {
        var next = Font.ToHfont();
        SendMessage(Handle, WmSetFont, next, new IntPtr(1));
        ReleaseFont();
        _font = next;
    }

    private void ReleaseFont()
    {
        if (_font != IntPtr.Zero)
        {
            DeleteObject(_font);
            _font = IntPtr.Zero;
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(Theme.Back);
        if (TabCount == 0)
        {
            return;
        }
        var body = DisplayRectangle;
        body.Inflate(1, 1);
        using (var pen = new Pen(Theme.Line))
        {
            e.Graphics.DrawRectangle(pen, body);
        }
        for (var i = 0; i < TabCount; i++)
        {
            DrawTab(e.Graphics, i);
        }
    }

    private void DrawTab(Graphics graphics, int index)
    {
        var bounds = GetTabRect(index);
        var selected = index == SelectedIndex;
        using (var brush = new SolidBrush(selected ? Theme.Field : Theme.Surface))
        {
            graphics.FillRectangle(brush, bounds);
        }
        if (selected)
        {
            using var accent = new SolidBrush(Theme.Accent);
            graphics.FillRectangle(accent, bounds.X, bounds.Y, bounds.Width, Ui.S(2));
        }
        var left = bounds.X + Ui.S(5);
        var page = TabPages[index];
        if (ImageList is not null && page.ImageKey.Length > 0 && ImageList.Images[page.ImageKey] is { } image)
        {
            graphics.DrawImage(image, left, bounds.Y + (bounds.Height - image.Height) / 2, image.Width, image.Height);
            left += image.Width + Ui.S(4);
        }
        var text = new Rectangle(left, bounds.Y, bounds.Right - left, bounds.Height);
        TextRenderer.DrawText(graphics, page.Text, Font, text, Theme.Ink, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
    }
}
