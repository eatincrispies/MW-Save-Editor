namespace NFSMWSave.App;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        Theme.Install();
        Application.Run(new MainForm(args.FirstOrDefault()));
    }
}
