using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
namespace OpenDownload.Desktop;
public sealed class BrowserSetupDialog : Window
{
    public BrowserSetupDialog(Func<DateTimeOffset?> checkedAt)
    {
        Style = (Style)Application.Current.FindResource(typeof(Window));
        Title = "Connect Chrome or Edge"; Width = 800; Height = 790; MaxHeight = SystemParameters.WorkArea.Height - 30; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var panel = new StackPanel { Margin = new Thickness(26) }; Content = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        panel.Children.Add(new TextBlock { Text = "A few clicks. Then download from your browser.", FontSize = 23, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap });
        DialogUi.Label(panel, "Your browser asks you to approve loading the extension. OpenDownload prepares everything else. No extension ID is normally needed.");
        var browser = new ComboBox { ItemsSource = new[] { "Chrome", "Edge" }, SelectedIndex = 0, Margin = new Thickness(0, 16, 0, 10), Width = 160, HorizontalAlignment = HorizontalAlignment.Left }; panel.Children.Add(browser);
        var status = new TextBlock { Foreground = (Brush)FindResource("Accent"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 12, 0, 8) };
        void Action(Action action) { try { action(); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or System.Text.Json.JsonException or System.ComponentModel.Win32Exception) { status.Text = ex.Message; } }
        async Task BrowserAction(bool check) { try { BrowserIntegration.Register(); await BrowserIntegration.OpenBrowser(browser.SelectedItem!.ToString()!, check); status.Text = check ? "Waiting for a new connection check..." : "Helper ready. In your browser, enable Developer mode and choose Load unpacked."; } catch (Exception ex) { status.Text = ex.Message; } }
        var picture = new Image { Height = 230, Stretch = Stretch.Uniform, Margin = new Thickness(0, 12, 0, 12) };
        void UpdatePicture()
        {
            string file = Path.Combine(AppContext.BaseDirectory, "docs", "images", browser.SelectedItem?.ToString() == "Edge" ? "edge-setup.png" : "chrome-setup.png");
            if (File.Exists(file)) picture.Source = new BitmapImage(new Uri(file));
        }
        browser.SelectionChanged += (_, _) => UpdatePicture(); UpdatePicture();
        Heading("1", "Open your browser’s extension page");
        var open = new Button { Content = "Prepare helper and open extensions", Style = (Style)FindResource("PrimaryButton"), HorizontalAlignment = HorizontalAlignment.Left };
        open.Click += async (_, _) => await BrowserAction(false); panel.Children.Add(open);
        panel.Children.Add(picture);
        DialogUi.Label(panel, "Illustration: Chrome places Developer mode at the top right; Edge normally places it in the left sidebar. Controls may move between browser versions.");
        Heading("2", "Load the included extension folder");
        DialogUi.Label(panel, "Turn on Developer mode → click Load unpacked → select this exact folder (the one containing manifest.json). Do not select the ZIP or the main application folder.");
        var folder = DialogUi.Input(panel, BrowserIntegration.ExtensionFolder); folder.IsReadOnly = true;
        var controls = new WrapPanel(); panel.Children.Add(controls);
        var copy = new Button { Content = "Copy folder path" }; copy.Click += (_, _) => Action(() => { Clipboard.SetText(BrowserIntegration.ExtensionFolder); status.Text = "Path copied. Paste it into the browser’s Select Folder dialog."; }); controls.Children.Add(copy);
        var show = new Button { Content = "Open extension folder" }; show.Click += (_, _) => Action(() => Process.Start(new ProcessStartInfo("explorer.exe") { UseShellExecute = true, ArgumentList = { BrowserIntegration.ExtensionFolder } })); controls.Children.Add(show);
        Heading("3", "Check the connection");
        DialogUi.Label(panel, "After OpenDownload Integration appears in your browser, open its popup and click Check connection. The button below opens that page and runs the check for you.");
        var check = new Button { Content = "Open extension and check connection", HorizontalAlignment = HorizontalAlignment.Left };
        check.Click += async (_, _) => await BrowserAction(true); panel.Children.Add(check); panel.Children.Add(status);
        var initialCheck = checkedAt();
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        timer.Tick += (_, _) => { var seen = checkedAt(); if (seen.HasValue && seen != initialCheck) status.Text = "Browser connection succeeded at " + seen.Value.LocalDateTime.ToString("t") + ". Right-click a link → Download with OpenDownload."; }; timer.Start(); Closed += (_, _) => timer.Stop();
        var guide = new Button { Content = "Open the full illustrated guide and troubleshooting", HorizontalAlignment = HorizontalAlignment.Left };
        guide.Click += (_, _) => Action(() => Process.Start(new ProcessStartInfo(Path.Combine(AppContext.BaseDirectory, "docs", "browser-setup.html")) { UseShellExecute = true })); panel.Children.Add(guide);
        var advanced = new Expander { Header = "Advanced: a different extension ID", Margin = new Thickness(0, 15, 0, 0) }; var inner = new StackPanel(); advanced.Content = inner; panel.Children.Add(advanced);
        DialogUi.Label(inner, "Only needed for older or custom builds. Copy the 32-letter ID from the browser’s extension card."); var id = DialogUi.Input(inner);
        var register = new Button { Content = "Register this ID" }; register.Click += (_, _) => Action(() => { BrowserIntegration.Register(id.Text.Trim()); status.Text = "Custom extension registered. Check connection in its popup."; }); inner.Children.Add(register);
        DialogUi.Label(panel, "If Developer mode or Load unpacked is missing on a work/school browser, contact its administrator. Automatic capture is optional and stays off by default.");
        var done = new Button { Content = "Done", HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 16, 0, 0) }; done.Click += (_, _) => Close(); panel.Children.Add(done);
        void Heading(string number, string text) => panel.Children.Add(new TextBlock { Text = number + ". " + text, FontSize = 17, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 22, 0, 10) });
    }
}
