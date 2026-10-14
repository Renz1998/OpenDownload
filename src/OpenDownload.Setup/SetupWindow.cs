using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
namespace OpenDownload.Setup;
public sealed class SetupWindow : Window
{
    public SetupWindow(string? destination, bool uninstall)
    {
        Style = (Style)Application.Current.FindResource(typeof(Window));
        Title = uninstall ? "Uninstall OpenDownload" : "Install OpenDownload "+typeof(SetupWindow).Assembly.GetName().Version?.ToString(3); Width = 620; SizeToContent = SizeToContent.Height; ResizeMode = ResizeMode.NoResize; WindowStartupLocation = WindowStartupLocation.CenterScreen;
        var panel = new StackPanel { Margin = new Thickness(30) }; Content = panel;
        panel.Children.Add(new TextBlock { Text = uninstall ? "Remove OpenDownload" : "Your downloads. Your workspace.", FontSize = 26, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap });
        panel.Children.Add(new TextBlock { Text = uninstall ? "The application and its browser helper registration will be removed. Downloads, encrypted history and settings are kept." : "Workbench or Focus. Resumable downloads. A browser helper ready to connect. Installs for your Windows account without administrator rights.", TextWrapping = TextWrapping.Wrap, Foreground = (Brush)FindResource("Muted"), Margin = new Thickness(0, 12, 0, 22) });
        panel.Children.Add(new TextBlock { Text = "Installation folder" });
        var folder = new TextBox { Text = destination ?? Installation.DefaultDestination(), IsReadOnly = uninstall }; panel.Children.Add(folder);
        var shortcut = new CheckBox { Content = "Create a desktop shortcut", IsChecked = true, Margin = new Thickness(0, 8, 0, 15), Visibility = uninstall ? Visibility.Collapsed : Visibility.Visible }; panel.Children.Add(shortcut);
        panel.Children.Add(new TextBlock { Text = uninstall ? "Any unrelated files in the program folder are preserved." : "After installing, choose your layout. Connect browser opens the illustrated extension guide. Browser approval is still required to load the extension.", TextWrapping = TextWrapping.Wrap, Foreground = (Brush)FindResource("Muted") });
        var progress = new ProgressBar { Height = 5, IsIndeterminate = true, Visibility = Visibility.Collapsed, Margin = new Thickness(0, 20, 0, 8) }; panel.Children.Add(progress);
        var status = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 14, 0, 0), Foreground = (Brush)FindResource("Accent") }; panel.Children.Add(status);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 22, 0, 0) }; panel.Children.Add(actions);
        var cancel = new Button { Content = "Close", IsCancel = true }; cancel.Click += (_, _) => Close(); actions.Children.Add(cancel);
        var install = new Button { Content = uninstall ? "Uninstall" : "Install OpenDownload", Style = (Style)FindResource("PrimaryButton") }; actions.Children.Add(install);
        bool busy = false, finished = false;
        Closing += (_, e) => { if (busy) e.Cancel = true; };
        install.Click += async (_, _) =>
        {
            if (finished && !uninstall) { Process.Start(new ProcessStartInfo(System.IO.Path.Combine(folder.Text, "OpenDownload.Desktop.exe")) { UseShellExecute = true }); Close(); return; }
            busy = true; install.IsEnabled = false; cancel.IsEnabled = false; folder.IsEnabled = false; shortcut.IsEnabled = false; progress.Visibility = Visibility.Visible;
            try
            {
                string path = folder.Text; bool desktop = shortcut.IsChecked == true;
                status.Text = uninstall ? "Removing the application…" : "Preparing files and browser helper…";
                await Task.Run(() => { if (uninstall) Installation.Remove(path, false); else Installation.Install(path, desktop, false); });
                finished = true; status.Text = uninstall ? "Uninstalled. Your downloads, settings and history are preserved." : "Installed. Launch OpenDownload to choose Workbench or Focus and connect your browser.";
                install.Content = uninstall ? "Done" : "Launch OpenDownload";
                if (uninstall) { install.Visibility = Visibility.Collapsed; }
            }
            catch (Exception ex) { status.Text = ex.Message; folder.IsEnabled = !uninstall; shortcut.IsEnabled = true; }
            finally { busy = false; install.IsEnabled = true; cancel.IsEnabled = true; progress.Visibility = Visibility.Collapsed; }
        };
    }
}
