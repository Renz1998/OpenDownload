using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using OpenDownload.Core;

namespace OpenDownload.Desktop;

internal static class DialogUi
{
    public static void Label(Panel panel, string text) => panel.Children.Add(new TextBlock { Text = text, Margin = new Thickness(0, 8, 0, 0), TextWrapping = TextWrapping.Wrap });
    public static TextBox Input(Panel panel, string text = "") { var box = new TextBox { Text = text }; panel.Children.Add(box); return box; }
    public static CheckBox Check(Panel panel, string text, bool value)
    { var box = new CheckBox { Content = text, IsChecked = value, Margin = new Thickness(0, 10, 0, 10) }; panel.Children.Add(box); return box; }
    public static void Buttons(Panel panel, Window window, string action, Action apply)
    {
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 14, 0, 0) };
        var cancel = new Button { Content = "Cancel", IsCancel = true }; cancel.Click += (_, _) => window.Close();
        var save = new Button { Content = action, IsDefault = true, Style=(Style)window.FindResource("PrimaryButton") }; save.Click += (_, _) => apply();
        buttons.Children.Add(cancel); buttons.Children.Add(save); panel.Children.Add(buttons);
    }
    public static StackPanel Setup(Window window, string title, double width)
    {
        window.Style = (Style)Application.Current.FindResource(typeof(Window));
        window.Title = title; window.Width = width; window.SizeToContent = SizeToContent.Height; window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
        window.ResizeMode = ResizeMode.NoResize; window.MaxHeight = SystemParameters.WorkArea.Height - 40;
        var panel = new StackPanel { Margin = new Thickness(24) };
        window.Content = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        return panel;
    }
}
public sealed class AddDialog : Window
{
    public List<BrowserRequest> Requests { get; private set; } = [];
    public string DirectoryPath { get; private set; } = "";
    public bool QueueOnly { get; private set; }
    public AddDialog(Settings settings, bool replace = false)
    {
        var panel = DialogUi.Setup(this, replace ? "Replace download URL" : "Add downloads", 640);
        DialogUi.Label(panel, replace ? "Fresh URL for the same file" : "HTTP / HTTPS URLs — one per line");
        var urls = DialogUi.Input(panel); urls.AcceptsReturn = !replace; urls.Height = replace ? 42 : 115; urls.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        DialogUi.Label(panel, "File name (optional; single download only)"); var name = DialogUi.Input(panel);
        DialogUi.Label(panel, "Save to folder"); var directory = DialogUi.Input(panel, settings.DownloadDirectory); directory.IsEnabled = !replace;
        var browse = new Button { Content = "Browse…", HorizontalAlignment = HorizontalAlignment.Left, IsEnabled = !replace };
        browse.Click += (_, _) => { using var picker = new System.Windows.Forms.FolderBrowserDialog { InitialDirectory = directory.Text }; if (picker.ShowDialog() == System.Windows.Forms.DialogResult.OK) directory.Text = picker.SelectedPath; };
        panel.Children.Add(browse);
        var rememberFolder = DialogUi.Check(panel,"Always save this file type here (single URL only)",false);
        bool folderEdited=false, updatingFolder=false;
        directory.TextChanged+=(_,_)=> { if(!updatingFolder) folderEdited=true; };
        void SuggestFolder()
        {
            var entries=urls.Text.Split(['\r','\n'],StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries);
            try
            {
                string fileName=entries.Length==1 ? string.IsNullOrWhiteSpace(name.Text) ? DownloadNames.FromUrl(DownloadNames.ValidateUrl(entries[0])) : name.Text : "";
                string type=DownloadFolders.TypeKey(fileName);
                rememberFolder.IsEnabled=!replace && entries.Length==1 && type.Length>0;
                rememberFolder.Content=type.Length>0 ? "Always save "+type+" files here" : "Always save this file type here (enter one URL first)";
                if(!folderEdited && !replace) { updatingFolder=true; directory.Text=DownloadFolders.ForType(settings,fileName); updatingFolder=false; }
            }
            catch(ArgumentException) { rememberFolder.IsEnabled=false; }
        }
        rememberFolder.IsEnabled=false; urls.TextChanged+=(_,_)=>SuggestFolder(); name.TextChanged+=(_,_)=>SuggestFolder();

        var queue = DialogUi.Check(panel, "Add paused — start with Resume", false);
        var advanced = new Expander { Header = "Authentication (optional)", Margin = new Thickness(0, 10, 0, 0) };
        var inner = new StackPanel(); advanced.Content = inner; panel.Children.Add(advanced);
        DialogUi.Label(inner, "Authorization header (for example: Bearer your-token)"); var auth = new PasswordBox { Padding = new Thickness(8), Margin = new Thickness(0, 4, 0, 8) }; inner.Children.Add(auth);
        DialogUi.Label(inner, "Cookie header (only for this server)"); var cookie = new PasswordBox { Padding = new Thickness(8), Margin = new Thickness(0, 4, 0, 8) }; inner.Children.Add(cookie);
        DialogUi.Label(inner, "These values are encrypted for your Windows account. Redirects to other origins do not receive them.");
        DialogUi.Buttons(panel, this, replace ? "Replace URL" : "Add downloads", () =>
        {
            try
            {
                var lines = urls.Text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (lines.Length == 0 || lines.Length > 200 || (replace && lines.Length != 1)) throw new ArgumentException("Enter between 1 and 200 URLs; replacing a link requires exactly one.");
                if (lines.Length > 1 && (!string.IsNullOrWhiteSpace(name.Text) || auth.Password.Length > 0 || cookie.Password.Length > 0)) throw new ArgumentException("Custom filenames and authentication apply to a single URL only.");
                if (auth.Password.Length > 32768 || cookie.Password.Length > 32768 || auth.Password.Contains('\r') || auth.Password.Contains('\n') || cookie.Password.Contains('\r') || cookie.Password.Contains('\n')) throw new ArgumentException("Invalid authentication header.");
                DirectoryPath = Path.GetFullPath(directory.Text); if (!Path.IsPathFullyQualified(directory.Text)) throw new ArgumentException("Choose an absolute download folder.");
                Directory.CreateDirectory(DirectoryPath);
                Requests = lines.Select(line => new BrowserRequest { Url = DownloadNames.ValidateUrl(line).AbsoluteUri, FileName = name.Text,
                    Headers = new Dictionary<string, string>() }).ToList();
                if (auth.Password.Length > 0) Requests[0].Headers!["Authorization"] = auth.Password;
                if (cookie.Password.Length > 0) Requests[0].Headers!["Cookie"] = cookie.Password;
                if(rememberFolder.IsChecked==true)
                {
                    if(Requests.Count!=1) throw new ArgumentException("Remembering a file-type folder requires one URL.");
                    string fileName=string.IsNullOrWhiteSpace(name.Text) ? DownloadNames.FromUrl(new Uri(Requests[0].Url)) : name.Text;
                    DownloadFolders.Remember(settings,fileName,DirectoryPath);
                }
                QueueOnly = queue.IsChecked == true; DialogResult = true;
            }
            catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException or NotSupportedException)
            { MessageBox.Show(this, ex.Message, "Check download details"); }
        });
        Loaded += (_, _) => urls.Focus();
    }
}
public sealed class OptionsDialog : Window
{
    public Settings Result { get; private set; }
    public OptionsDialog(Settings current)
    {
        Result = current;
        var panel = DialogUi.Setup(this, "Options and scheduler", 580);
        DialogUi.Label(panel, "Default download folder"); var directory = DialogUi.Input(panel, current.DownloadDirectory);
        var browse = new Button { Content = "Browse…", HorizontalAlignment = HorizontalAlignment.Left };
        browse.Click += (_, _) => { using var picker = new System.Windows.Forms.FolderBrowserDialog { InitialDirectory = directory.Text }; if (picker.ShowDialog() == System.Windows.Forms.DialogResult.OK) directory.Text = picker.SelectedPath; }; panel.Children.Add(browse);
        DialogUi.Label(panel, "Connections per file (1–8)"); var connections = DialogUi.Input(panel, current.Connections.ToString());
        DialogUi.Label(panel, "Concurrent downloads (1–8)"); var concurrent = DialogUi.Input(panel, current.ConcurrentDownloads.ToString());
        DialogUi.Label(panel, "Global speed limit in KiB/s (0 = unlimited)"); var speed = DialogUi.Input(panel, current.SpeedLimitKiB.ToString());
        DialogUi.Label(panel, "Gaming mode shared cap in KiB/s"); var gaming = DialogUi.Input(panel, current.GamingLimitKiB.ToString());
        var progressPopup=DialogUi.Check(panel,"Show the shared download activity window when downloads start",current.ShowProgressWindow);
        var completionAlerts=DialogUi.Check(panel,"Show completion notifications with Open file and Open folder",current.CompletionNotifications);
        var editedRules=new Dictionary<string,string>(current.TypeFolders);
        var rulePanel=new StackPanel(); DialogUi.Label(panel,"Remembered file-type folders"); panel.Children.Add(rulePanel);
        if(editedRules.Count==0) DialogUi.Label(rulePanel,"No remembered folders. Choose Always save this file type here in a download prompt.");
        foreach(var pair in current.TypeFolders)
        {
            var line=new WrapPanel(); rulePanel.Children.Add(line);
            line.Children.Add(new TextBlock { Text=pair.Key,Width=65,VerticalAlignment=VerticalAlignment.Center });
            var value=new TextBox { Text=pair.Value,Width=275,Margin=new Thickness(0,4,4,4) }; line.Children.Add(value);
            value.TextChanged+=(_,_)=>editedRules[pair.Key]=value.Text;
            var pick=new Button { Content="Browse",Padding=new Thickness(8,6,8,6) }; line.Children.Add(pick);
            pick.Click+=(_,_)=> { using var picker=new System.Windows.Forms.FolderBrowserDialog { InitialDirectory=value.Text }; if(picker.ShowDialog()==System.Windows.Forms.DialogResult.OK) value.Text=picker.SelectedPath; };
            var forget=new Button { Content="Forget",Padding=new Thickness(8,6,8,6) }; line.Children.Add(forget);
            forget.Click+=(_,_)=> { editedRules.Remove(pair.Key); line.Visibility=Visibility.Collapsed; };
        }
        var compact = DialogUi.Check(panel, "Compact Workbench rows", current.CompactRows);
        var schedule = DialogUi.Check(panel, "Run queued downloads only during a daily time window", current.ScheduleEnabled);
        DialogUi.Label(panel, "Start time (24-hour HH:mm, local time)"); var start = DialogUi.Input(panel, TimeSpan.FromMinutes(current.ScheduleStartMinute).ToString(@"hh\:mm"));
        DialogUi.Label(panel, "Stop time (24-hour HH:mm; same as start = all day)"); var stop = DialogUi.Input(panel, TimeSpan.FromMinutes(current.ScheduleEndMinute).ToString(@"hh\:mm"));
        DialogUi.Label(panel, "Windows must be awake and OpenDownload running. The app pauses transfers outside this window. Paused downloads only resume when you choose Resume.");
        var tray = DialogUi.Check(panel, "Close window to tray (use tray menu → Exit to stop the app)", current.MinimizeToTray);
        DialogUi.Buttons(panel, this, "Save options", () =>
        {
            try
            {
                if (!int.TryParse(connections.Text, out int c) || c is < 1 or > 8 || !int.TryParse(concurrent.Text, out int n) || n is < 1 or > 8 || !int.TryParse(speed.Text, out int s) || s is < 0 or > 1048576 || !int.TryParse(gaming.Text, out int g) || g is < 1 or > 1048576) throw new ArgumentException("Check connection counts (1–8), speed limit (0–1048576 KiB/s) and gaming cap (1–1048576 KiB/s).");
                if (!TimeSpan.TryParseExact(start.Text, @"hh\:mm", null, out var begin) || !TimeSpan.TryParseExact(stop.Text, @"hh\:mm", null, out var end) || begin.TotalHours >= 24 || end.TotalHours >= 24) throw new ArgumentException("Enter times as HH:mm, from 00:00 to 23:59.");
                if (!Path.IsPathFullyQualified(directory.Text)) throw new ArgumentException("Choose an absolute download folder.");
                Directory.CreateDirectory(directory.Text);
                if(editedRules.Values.Any(value=>!Path.IsPathFullyQualified(value))) throw new ArgumentException("Use an absolute path for every remembered folder.");
                Result = new Settings { DownloadDirectory = Path.GetFullPath(directory.Text), Connections = c, ConcurrentDownloads = n, SpeedLimitKiB = s, ScheduleEnabled = schedule.IsChecked == true,
                    ScheduleStartMinute = (int)begin.TotalMinutes, ScheduleEndMinute = (int)end.TotalMinutes, MinimizeToTray = tray.IsChecked == true,
                    Layout = current.Layout, LayoutDefaultVersion=current.LayoutDefaultVersion,TypeFolders=editedRules.ToDictionary(pair=>pair.Key,pair=>Path.GetFullPath(pair.Value)),ShowProgressWindow=progressPopup.IsChecked==true,SortMode=current.SortMode,SpeedUnit=current.SpeedUnit,CompletionNotifications=completionAlerts.IsChecked==true, PreviousSpeedLimitKiB=current.PreviousSpeedLimitKiB, LightTheme = current.LightTheme, CompactRows = compact.IsChecked == true, GamingLimitKiB = g, BrowserCheckedAt = current.BrowserCheckedAt };
                DialogResult = true;
            }
            catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException or NotSupportedException) { MessageBox.Show(this, ex.Message, "Check options"); }
        });
    }
}
public sealed class ProgressDialog : Window
{
    public ProgressDialog(DownloadRow row, Action? pause = null, Action? resume = null)
    {
        var panel = DialogUi.Setup(this, "Download details", 650);
        var title = new TextBlock { Text = row.FileName, FontSize = 20, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap }; panel.Children.Add(title);
        var bar = new ProgressBar { Height = 24, Margin = new Thickness(0, 18, 0, 12), Maximum = 100 }; panel.Children.Add(bar);
        var info = new TextBlock { TextWrapping = TextWrapping.Wrap }; panel.Children.Add(info);
        var segments = new TextBlock { FontFamily = new System.Windows.Media.FontFamily("Consolas"), Margin = new Thickness(0, 16, 0, 0) }; panel.Children.Add(segments);
        var controls=new WrapPanel(); panel.Children.Add(controls);
        var pauseButton=new Button { Content="Pause",IsEnabled=pause!=null }; pauseButton.Click+=(_,_)=>pause?.Invoke(); controls.Children.Add(pauseButton);
        var resumeButton=new Button { Content="Resume",IsEnabled=resume!=null }; resumeButton.Click+=(_,_)=>resume?.Invoke(); controls.Children.Add(resumeButton);
        var close=new Button { Content="Close progress window" }; close.Click+=(_,_)=>Close(); controls.Children.Add(close);
        DialogUi.Label(panel,"Closing this window keeps the download running. Pause stops its transfer.");
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        void Update()
        {
            title.Text=row.FileName; bar.Value = row.Progress;
            pauseButton.IsEnabled=pause!=null && row.Job.Status is DownloadStatus.Downloading or DownloadStatus.Connecting; resumeButton.IsEnabled=resume!=null && row.Job.Status is DownloadStatus.Paused or DownloadStatus.Failed;
            info.Text = $"{row.Status} · {row.Received} of {row.Size}\n{row.Speed} · Remaining {row.Remaining}\n\n{row.Detail}\n\n{row.Timing}";
            segments.Text = string.Join("\n", row.Job.Segments.Select((s, i) => $"Connection {i + 1}: {DownloadRow.FormatBytes(s.Committed)}"));
        }
        timer.Tick += (_, _) => Update(); Closed += (_, _) => timer.Stop(); Update(); timer.Start();
    }
}
