using NFSMWSave.Core;

namespace NFSMWSave.App;

internal abstract class EditorPage : UserControl
{
    private int _binding;

    protected EditorPage()
    {
        BackColor = Theme.Back;
        Dock = DockStyle.Fill;
        Padding = new Padding(Ui.S(6));
    }

    public event Action? Edited;
    public event Action? BulkEdited;

    public abstract string Title { get; }
    public abstract string IconName { get; }

    protected SaveFile? File { get; private set; }
    protected bool IsBinding => _binding > 0;

    public void Attach(SaveFile file)
    {
        File = file;
        Bound(Populate);
    }

    public void Reload()
    {
        if (File is not null)
        {
            Bound(Populate);
        }
    }

    protected abstract void Populate();

    protected void Bound(Action action)
    {
        _binding++;
        try
        {
            action();
        }
        finally
        {
            _binding--;
        }
    }

    protected void MarkEdited()
    {
        if (!IsBinding)
        {
            Edited?.Invoke();
        }
    }

    protected void MarkBulkEdited() => BulkEdited?.Invoke();

    protected void OnNumber(NumericUpDown control, Action<decimal> apply)
    {
        control.ValueChanged += (_, _) =>
        {
            if (!IsBinding && File is not null)
            {
                apply(control.Value);
                MarkEdited();
            }
        };
    }

    protected void OnCheck(CheckBox control, Action<bool> apply)
    {
        control.CheckedChanged += (_, _) =>
        {
            if (!IsBinding && File is not null)
            {
                apply(control.Checked);
                MarkEdited();
            }
        };
    }
}
