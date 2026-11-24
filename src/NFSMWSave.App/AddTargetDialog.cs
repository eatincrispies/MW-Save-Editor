namespace NFSMWSave.App;

internal sealed class AddTargetDialog : Form
{
    public AddTargetDialog(string careerStatus, bool careerOpen, string myStatus, bool myOpen)
    {
        Text = "Add Car";
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

        var question = new Label { Text = "Where should the new car go?", AutoSize = true, Margin = new Padding(Ui.S(2), Ui.S(4), Ui.S(2), Ui.S(8)) };
        var table = Ui.Table(2);
        table.Padding = new Padding(Ui.S(8));
        Ui.Span(table, question);
        AddChoice(table, "Career Cars", careerStatus, careerOpen, false);
        AddChoice(table, "My Cars", myStatus, myOpen, true);
        var cancel = Ui.Action("Cancel", () => DialogResult = DialogResult.Cancel, 80);
        cancel.Anchor = AnchorStyles.Right;
        CancelButton = cancel;
        Ui.Span(table, cancel);
        Controls.Add(table);
    }

    public bool MyCars { get; private set; }

    private void AddChoice(TableLayoutPanel table, string title, string status, bool open, bool myCars)
    {
        var row = table.RowCount++;
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var button = Ui.Action(title, () =>
        {
            MyCars = myCars;
            DialogResult = DialogResult.OK;
        }, 130);
        button.Enabled = open;
        button.Height = Ui.S(32);
        var note = new Label { Text = status, AutoSize = true, Anchor = AnchorStyles.Left, ForeColor = Theme.Muted, Margin = new Padding(Ui.S(6), Ui.S(4), Ui.S(4), Ui.S(4)) };
        table.Controls.Add(button, 0, row);
        table.Controls.Add(note, 1, row);
    }
}
