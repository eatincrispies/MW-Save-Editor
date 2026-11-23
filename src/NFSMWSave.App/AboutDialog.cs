namespace NFSMWSave.App;

internal sealed class AboutDialog : Form
{
    private const string Author = "eatincrispies";

    public AboutDialog(string appName)
    {
        Text = "About";
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

        var picture = Ui.Picture(128, 128);
        picture.Image = Assets.Get("author");

        var title = Ui.Text(appName);
        title.Font = new Font("Segoe UI Semibold", 13f);
        var version = Ui.Text($"Version {typeof(AboutDialog).Assembly.GetName().Version?.ToString(3)}");
        var author = Ui.Text($"by {Author}");
        var game = Ui.Text("Need for Speed: Most Wanted (2005)");
        var platforms = Ui.Text("PC, PlayStation 2, Xbox 360, GameCube and original Xbox");
        var details = Ui.Column(title, version, author, game, platforms);
        details.Margin = new Padding(Ui.S(10), 0, Ui.S(6), 0);

        var close = Ui.Action("Close", () => DialogResult = DialogResult.Cancel, 80);
        close.Anchor = AnchorStyles.Right;
        AcceptButton = close;
        CancelButton = close;

        var layout = Ui.Column(Ui.Across(picture, details), close);
        layout.Padding = new Padding(Ui.S(10));
        Controls.Add(layout);
    }
}
