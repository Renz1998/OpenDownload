using System.IO;
using System.Windows;
namespace OpenDownload.Setup;
public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        bool uninstall = e.Args.Contains("--uninstall"); bool test = e.Args.Contains("--test-mode"); bool quiet = e.Args.Contains("--quiet");
        string? target = null;
        for (int i = 0; i + 1 < e.Args.Length; i++) if (e.Args[i] is "--install" or "--uninstall") target = e.Args[i + 1];
        if ((test || quiet) && target != null)
        {
            try
            {
                if (test)
                {
                    string allowed = Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, "test-results")) + Path.DirectorySeparatorChar;
                    if (!Path.GetFullPath(target).StartsWith(allowed, StringComparison.OrdinalIgnoreCase)) throw new IOException("Installer tests must stay under this project's test-results directory.");
                }
                if (uninstall) await Task.Run(() => Installation.Remove(target, test)); else await Task.Run(() => Installation.Install(target, !test, test));
                Shutdown(0);
            }
            catch (Exception ex)
            {
                string errors = test ? Path.Combine(Environment.CurrentDirectory, "test-results") : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OpenDownload", "installer");
                Directory.CreateDirectory(errors); File.WriteAllText(Path.Combine(errors, "installer-error.txt"), ex.ToString()); Shutdown(1);
            }
            return;
        }
        MainWindow = new SetupWindow(target, uninstall); MainWindow.Show();
    }
}
