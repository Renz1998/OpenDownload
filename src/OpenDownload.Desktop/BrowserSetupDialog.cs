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
        panel.Children.Add(new TextBlock { Text = "Connect your browser to OpenDownload", FontSize = 23, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap });
        DialogUi.Label(panel, "Follow these three steps once. Keep this window open so you can return to it after each step. Start by choosing the browser you use below.");
        var browser = new ComboBox { ItemsSource = new[] { "Chrome", "Edge" }, SelectedIndex = 0, Margin = new Thickness(0, 16, 0, 10), Width = 160, HorizontalAlignment = HorizontalAlignment.Left }; panel.Children.Add(browser);
        var status = new TextBlock { Foreground = (Brush)FindResource("Accent"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 12, 0, 8) };
        void Action(Action action) { try { action(); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or System.Text.Json.JsonException or System.ComponentModel.Win32Exception) { status.Text = ex.Message; } }
        async Task BrowserAction(bool check) { try { BrowserIntegration.Register(); await BrowserIntegration.OpenBrowser(browser.SelectedItem!.ToString()!, check); status.Text = check ? "Testing the connection. Look for Connected to OpenDownload in the browser tab." : "Step 1 is ready. In the browser tab that opened, turn on Developer mode. Then return here and follow Step 2."; } catch (Exception ex) { status.Text = ex.Message; } }
        var picture = new Image { Height = 230, Stretch = Stretch.Uniform, Margin = new Thickness(0, 12, 0, 12) };
        void UpdatePicture()
        {
            string file = Path.Combine(AppContext.BaseDirectory, "docs", "images", browser.SelectedItem?.ToString() == "Edge" ? "edge-setup.png" : "chrome-setup.png");
            if (File.Exists(file)) picture.Source = new BitmapImage(new Uri(file));
        }
        browser.SelectionChanged += (_, _) => UpdatePicture(); UpdatePicture();
        Heading("1", "Choose your browser, then open its extensions page");
        var open = new Button { Content = "1. Open browser setup", Style = (Style)FindResource("PrimaryButton"), HorizontalAlignment = HorizontalAlignment.Left };
        open.Click += async (_, _) => await BrowserAction(false); panel.Children.Add(open);
        panel.Children.Add(picture);
        DialogUi.Label(panel, "Illustration: Chrome places Developer mode at the top right; Edge normally places it in the left sidebar. Controls may move between browser versions.");
        Heading("2", "Add OpenDownload to your browser");
        DialogUi.Label(panel, "In the browser tab: turn on Developer mode, then click Load unpacked. A folder picker will open. Click Copy extension folder below, paste the copied path into the picker’s address bar, press Enter, then click Select Folder. You do not need to open or edit any files.");
        var folder = DialogUi.Input(panel, BrowserIntegration.ExtensionFolder); folder.IsReadOnly = true;
        var controls = new WrapPanel(); panel.Children.Add(controls);
        var copy = new Button { Content = "2. Copy extension folder" }; copy.Click += (_, _) => Action(() => { Clipboard.SetText(BrowserIntegration.ExtensionFolder); status.Text = "Copied! Return to the browser, click Load unpacked, paste this path into the folder picker’s address bar, press Enter, then click Select Folder."; }); controls.Children.Add(copy);
        var show = new Button { Content = "View extension folder" }; show.Click += (_, _) => Action(() => Process.Start(new ProcessStartInfo("explorer.exe") { UseShellExecute = true, ArgumentList = { BrowserIntegration.ExtensionFolder } })); controls.Children.Add(show);
        Heading("3", "Check the connection");
        DialogUi.Label(panel, "When you see OpenDownload Integration on the browser’s extensions page, return here and click 3. Test connection below. A browser tab will open to test the connection. If that tab does not open, click the puzzle-piece Extensions icon in your browser toolbar, choose OpenDownload Integration, then click Check connection.");
        var check = new Button { Content = "3. Test connection", HorizontalAlignment = HorizontalAlignment.Left };
        check.Click += async (_, _) => await BrowserAction(true); panel.Children.Add(check); panel.Children.Add(status);
        var initialCheck = checkedAt();
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        timer.Tick += (_, _) => { var seen = checkedAt(); if (seen.HasValue && seen != initialCheck) status.Text = "Connected! Your browser is ready. Checked at " + seen.Value.LocalDateTime.ToString("t") + ". Try it: right-click a download link in your browser and choose Download with OpenDownload."; }; timer.Start(); Closed += (_, _) => timer.Stop();
        Heading("4", "Use a website's Download button (optional)");
        DialogUi.Label(panel, "Connection alone does not change your browser downloads. In Chrome or Edge, click the puzzle-piece Extensions icon, choose OpenDownload Integration, then turn on Use OpenDownload when I click a website's Download button. Leave the website list empty to include all sites. You can turn it off any time. Then click Test a real browser download in the extension. It downloads a tiny public test file; OpenDownload reports whether capture, transfer and checksum verification succeeded.");
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
