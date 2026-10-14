using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using OpenDownload.Core;
namespace OpenDownload.Desktop;
public partial class MainWindow
{
    public async Task RunUiTestsAsync(string output)
    {
        int passed = 0;
        void Check(bool value, string label) { if (!value) throw new Exception(label); File.AppendAllText(Path.Combine(output, "ui-results.txt"), "PASS " + label + "\n"); passed++; }
        try
        {
            if (Environment.GetEnvironmentVariable("OPENDOWNLOAD_TEST_MODE") != "1" || !App.DataDirectory.Contains("test-results", StringComparison.OrdinalIgnoreCase) || allJobs.Count != 0) throw new InvalidOperationException("UI tests require a fresh isolated test profile.");
            timer.Stop(); queuePaused = true; Directory.CreateDirectory(output); File.WriteAllText(Path.Combine(output, "ui-results.txt"), "");
            var bindingErrors = new BindingErrorListener(); System.Diagnostics.PresentationTraceSources.DataBindingSource.Listeners.Add(bindingErrors);
            foreach (var (name, state, size, received) in new[] { ("flux-model.safetensors", DownloadStatus.Paused, 9_600_000_000L, 6_240_000_000L), ("game-update.7z", DownloadStatus.Paused, 38_200_000_000L, 10_314_000_000L), ("training-data.zip", DownloadStatus.Paused, 5_300_000_000L, 2_226_000_000L), ("adapter-model.gguf", DownloadStatus.Failed, 2_100_000_000L, 378_000_000L), ("reference-video.mp4", DownloadStatus.Completed, 410_000_000L, 410_000_000L) })
            {
                var job = new DownloadJob { FileName = name, Url = "https://example.invalid/" + name, Destination = Path.Combine(App.DataDirectory, name), Status = state, TotalBytes = size, SupportsRanges = true, Segments = [new Segment { Start = 0, End = size - 1, Committed = received }], Error = state == DownloadStatus.Failed ? "Server returned HTTP 403. The link may have expired or need authentication." : null };
                allJobs.Add(job); rows.Add(new DownloadRow(job));
            }
            settings.Layout = "Workbench"; ApplyLayout(); SelectOne(rows[0]); UpdateEmptyState(); UpdateSettingsLabels();
            Summary.Text = "5 example downloads · no network activity";
            await Dispatcher.InvokeAsync(UpdateLayout, DispatcherPriority.ApplicationIdle);
            Check(Downloads.Visibility == Visibility.Visible && FocusDownloads.Visibility == Visibility.Collapsed, "Workbench uses the download table");
            Theme.ApplyTitleBar(this); Check(Theme.TitleBarMatches(this),"Windows accepts the themed title bar: " + Theme.LastTitleBarResult.ToString("X8"));
            Check(BrowserIntegration.BrowserPage("Chrome",false)=="chrome://extensions/" && BrowserIntegration.BrowserPage("Edge",false)=="edge://extensions/","Browser navigation uses the correct extensions page");
            Check(((System.Windows.Media.SolidColorBrush)SelectionTitle.Foreground).Color == ((System.Windows.Media.SolidColorBrush)FindResource("Ink")).Color, "Dark theme details text matches readable foreground");
            Capture("workbench.png");
            settings.Layout = "Focus"; ApplyLayout(); SelectOne(rows[0]); await Dispatcher.InvokeAsync(UpdateLayout, DispatcherPriority.ApplicationIdle);
            Check(FocusDownloads.Visibility == Visibility.Visible && LibrarySidebar.Visibility == Visibility.Collapsed && Grid.GetColumn(DetailsPanel) == 1, "Focus uses transfer cards and side details");
            Capture("focus.png");
            var welcome = new LayoutChooser { Owner = this };
            _ = Dispatcher.BeginInvoke(new Action(() => FindButtons((DependencyObject)welcome.Content).Single(b => b.Content?.ToString() == "Use Focus").RaiseEvent(new RoutedEventArgs(Button.ClickEvent))), DispatcherPriority.ApplicationIdle);
            Check(welcome.ShowDialog() == true && welcome.Choice == "Focus", "Opening chooser accepts the Focus selection");
            _ = Dispatcher.BeginInvoke(new Action(() => { var switcher = Application.Current.Windows.OfType<LayoutChooser>().Single(); FindButtons((DependencyObject)switcher.Content).Single(b => b.Content?.ToString() == "Use Workbench").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); }), DispatcherPriority.ApplicationIdle);
            LayoutClick(this, new()); Check(settings.Layout == "Workbench" && store.LoadSettings().Layout == "Workbench", "Layout switch uses the chooser and saves the new selection");
            settings.Layout = "Focus"; ApplyLayout(); SelectOne(rows[0]);
            settings.SpeedLimitKiB = 777; GamingClick(this, new()); Check(settings.SpeedLimitKiB == 5120 && settings.PreviousSpeedLimitKiB == 777, "Gaming mode applies one shared cap"); GamingClick(this, new()); Check(settings.SpeedLimitKiB == 777 && settings.PreviousSpeedLimitKiB == null, "Gaming mode restores previous cap");
            SelectOne(rows[0]); RemoveClick(this, new()); Check(rows.Count == 4 && allJobs.Count == 5 && allJobs[0].Hidden, "Remove hides history without deleting files or state"); UndoClick(this, new()); Check(rows.Count == 5 && !allJobs[0].Hidden, "Undo restores removed entry");
            DismissNoticeClick(this,new()); Check(NoticeBar.Visibility==Visibility.Collapsed,"Notification can be dismissed independently of undo");
            var recycleJob = new DownloadJob { Destination=Path.Combine(App.DataDirectory,"recycle-test.txt") };
            string unrelated=Path.Combine(App.DataDirectory,"unrelated.txt");
            File.WriteAllText(recycleJob.Destination,"completed"); File.WriteAllText(recycleJob.PartialPath,"partial"); File.WriteAllText(unrelated,"keep");
            RecycleDownloadFiles(recycleJob); Check(!File.Exists(recycleJob.Destination) && !File.Exists(recycleJob.PartialPath) && File.ReadAllText(unrelated)=="keep","Delete recycles only the selected download and its own partial file");
            var timed = new DownloadRow(new DownloadJob { StartedAt=DateTimeOffset.Now.AddMinutes(-2),FinishedAt=DateTimeOffset.Now,ActiveSeconds=90,Status=DownloadStatus.Completed });
            Check(timed.Timing.Contains("Started:") && timed.Timing.Contains("Finished:") && timed.Remaining.Contains("1m 30s"),"Completed downloads display timestamps and duration");
            settings.Layout = "Focus"; Save(); Check(store.LoadSettings().Layout == "Focus", "Layout selection persists");
            SearchBox.Text = "not-present"; UpdateEmptyState(); Check(EmptyState.Visibility == Visibility.Visible, "Search empty state is visible"); SearchBox.Text = "";
            Theme.Apply(true); await Dispatcher.InvokeAsync(UpdateLayout, DispatcherPriority.ApplicationIdle); Check(((System.Windows.Media.SolidColorBrush)FindResource("Ink")).Color.R < 50, "Light theme applies readable dark text"); Theme.Apply(false);
            using (var chooser = new WindowCapture(new LayoutChooser())) { await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle); GuidePictures.Save(chooser.Window.Content as System.Windows.Media.Visual ?? chooser.Window, (int)chooser.Window.ActualWidth, (int)chooser.Window.ActualHeight, Path.Combine(output, "layout-choice.png")); }
            GuidePictures.Create(output);
            Check(bindingErrors.Errors.Count == 0, "Both layouts render without binding errors: " + string.Join(";", bindingErrors.Errors));
            File.AppendAllText(Path.Combine(output, "ui-results.txt"), $"{passed} UI checks passed.\n");
            await ExitAsync();
        }
        catch (Exception ex) { Directory.CreateDirectory(output); File.WriteAllText(Path.Combine(output, "ui-failure.txt"), ex.ToString()); await ExitAsync(); Environment.Exit(1); }
        void Capture(string name) { UpdateLayout(); GuidePictures.Save(Content as System.Windows.Media.Visual ?? this, (int)ActualWidth, (int)ActualHeight, Path.Combine(output, name)); }
    }
    private sealed class BindingErrorListener : TraceListener
    { public List<string> Errors { get; } = []; public override void Write(string? message) { if (message?.Contains("Error:") == true) Errors.Add(message); } public override void WriteLine(string? message) => Write(message); }
    private sealed class WindowCapture : IDisposable
    { public Window Window { get; } public WindowCapture(Window window) { Window = window; window.Show(); } public void Dispose() => Window.Close(); }
    private static IEnumerable<Button> FindButtons(DependencyObject root)
    {
        if (root is Button button) yield return button;
        foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>()) foreach (var found in FindButtons(child)) yield return found;
    }
}
