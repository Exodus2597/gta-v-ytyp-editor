namespace YtypEditor;

internal static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        if (args.Contains("--self-test"))
        {
            try
            {
                Tests.Run();
                Environment.Exit(0);
            }
            catch (Exception ex)
            {
                File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "test-results.txt"), ex.ToString());
                Environment.Exit(1);
            }
            return;
        }
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm(args.FirstOrDefault(a => !a.StartsWith("--"))));
    }
}
