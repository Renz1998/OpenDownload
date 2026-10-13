using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
using OpenDownload.Core;

namespace OpenDownload.Desktop;

public partial class MainWindow : Window
{
    private readonly ObservableCollection<DownloadRow> rows = [];
    private readonly List<DownloadJob> allJobs = [];
    private readonly List<DownloadRow> removed = [];
    private readonly Dictionary<string, DownloadStatus> previousStates = [];
    private readonly Dictionary<string, (CancellationTokenSource Cts, Task Task)> running = [];
    private readonly Dictionary<string, long> lastBytes = [];
    private readonly DownloadEngine engine = new();
    private readonly Store store;
    private Settings settings;
    private readonly DispatcherTimer timer;
    private readonly System.Windows.Forms.NotifyIcon tray;
    private bool quitting, queuePaused;
    private DateTime lastTick = DateTime.UtcNow;
    private string category = "All downloads";
    public MainWindow()
    {
        InitializeComponent();
        store = new Store(App.DataDirectory); settings = store.LoadSettings();
        Theme.Apply(settings.LightTheme);
        allJobs.AddRange(store.Load());
        foreach (var job in allJobs.Where(j => !j.Hidden)) rows.Add(new DownloadRow(job));
        engine.Limiter.LimitKiB = () => settings.SpeedLimitKiB;
        Downloads.ItemsSource = rows;
        FocusDownloads.ItemsSource = rows;
        CollectionViewSource.GetDefaultView(rows).Filter = MatchesFilter;
        tray = new System.Windows.Forms.NotifyIcon { Text = "OpenDownload", Icon = System.Drawing.SystemIcons.Application, Visible = true };
        var menu = new System.Windows.Forms.ContextMenuStrip();
        menu.Items.Add("Show OpenDownload", null, (_, _) => Dispatcher.Invoke(ShowWindow));
        menu.Items.Add("Pause all downloads", null, (_, _) => Dispatcher.Invoke(PauseAll));
        menu.Items.Add("Resume queue", null, (_, _) => Dispatcher.Invoke(() => { queuePaused = false; foreach (var row in rows.Where(r => r.Job.Status == DownloadStatus.Paused)) row.Job.Status = DownloadStatus.Queued; Tick(); }));
        menu.Items.Add("Exit", null, (_, _) => Dispatcher.Invoke(async () => await ExitAsync()));
        tray.ContextMenuStrip = menu; tray.DoubleClick += (_, _) => Dispatcher.Invoke(ShowWindow);
        timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) }; timer.Tick += (_, _) => Tick(); timer.Start();
        PreviewKeyDown += (_, e) => { if (e.Key == Key.N && Keyboard.Modifiers == ModifierKeys.Control) { AddClick(this, new RoutedEventArgs()); e.Handled = true; } };
        UpdateSettingsLabels(); ApplyLayout(); Tick();
        ContentRendered += (_, _) =>
        {
            if (Environment.GetEnvironmentVariable("OPENDOWNLOAD_TEST_MODE") == "1") return;
            if (settings.Layout is not ("Workbench" or "Focus")) ChooseLayout();
        };
    }
    private void ShowWindow() { Show(); WindowState = WindowState.Normal; Activate(); }
    public void ShowFromRequest() => ShowWindow();
    private bool MatchesFilter(object value)
    {
        var row = (DownloadRow)value;
        bool match = category switch { "All downloads" => true, "Downloading" => row.Job.Status is DownloadStatus.Downloading or DownloadStatus.Connecting or DownloadStatus.Queued, "Paused" => row.Job.Status == DownloadStatus.Paused, "Needs attention" => row.Job.Status == DownloadStatus.Failed, "Completed" => row.Job.Status == DownloadStatus.Completed, _ => row.Category == category };
        return match && row.FileName.Contains(SearchBox?.Text ?? "", StringComparison.OrdinalIgnoreCase);
    }
    private void Tick()
    {
        double seconds = Math.Max(0.1, (DateTime.UtcNow - lastTick).TotalSeconds); lastTick = DateTime.UtcNow;
        foreach (var row in rows)
        {
            long value = row.Job.Downloaded;
            row.Job.BytesPerSecond = row.Job.Status == DownloadStatus.Downloading ? Math.Max(0, value - lastBytes.GetValueOrDefault(row.Job.Id, value)) / seconds : 0;
            lastBytes[row.Job.Id] = value; row.Refresh();
        }
        if (!settings.AllowedNow(DateTime.Now))
        {
            foreach (var row in rows.Where(r => running.ContainsKey(r.Job.Id) && r.Job.Status is DownloadStatus.Downloading or DownloadStatus.Connecting))
            { row.Job.Status = DownloadStatus.Queued; running[row.Job.Id].Cts.Cancel(); }
        }
        else if (!queuePaused && !quitting)
        {
            foreach (var row in rows.Where(r => r.Job.Status == DownloadStatus.Queued && !running.ContainsKey(r.Job.Id)).ToArray())
            { if (running.Count >= settings.ConcurrentDownloads) break; Start(row); }
        }
        bool filterChanged = rows.Any(r => !previousStates.TryGetValue(r.Job.Id, out var previous) || previous != r.Job.Status);
        foreach (var row in rows) previousStates[row.Job.Id] = row.Job.Status;
        if (running.Count > 0) Save();
        if (filterChanged) CollectionViewSource.GetDefaultView(rows).Refresh();
        UpdateSelection(); UpdateEmptyState();
        Summary.Text = $"{rows.Count} downloads · {running.Count} active · {DownloadRow.FormatBytes((long)rows.Sum(r => r.Job.BytesPerSecond))}/s";
        ScheduleStatus.Text = settings.ScheduleEnabled ? settings.AllowedNow(DateTime.Now) ? "Schedule: active" : "Schedule: waiting" : "Schedule: off";
    }
    private bool Save()
    {
        try { store.Save(allJobs, settings); return true; }
        catch (Exception) { PauseAll(); Summary.Text = "Could not save download state. Downloads paused; check disk space and permissions."; return false; }
    }
    private void Start(DownloadRow row)
    {
        var cts = new CancellationTokenSource(); row.Job.Status = DownloadStatus.Connecting;
        var task = RunAndFinishAsync(row, cts);
        running[row.Job.Id] = (cts, task);
    }
    private async Task RunAndFinishAsync(DownloadRow row, CancellationTokenSource cts)
    {
        await Task.Yield();
        await Task.Run(() => engine.RunAsync(row.Job, cts.Token, async () =>
        { if (!await Dispatcher.InvokeAsync(Save)) throw new IOException("Could not save the completed download state. Check disk space and try Resume."); }));
        running.Remove(row.Job.Id); cts.Dispose();
        if (row.Job.Status == DownloadStatus.Paused && settings.ScheduleEnabled && !settings.AllowedNow(DateTime.Now) && !queuePaused && !quitting) row.Job.Status = DownloadStatus.Queued;
        if (row.Job.Status == DownloadStatus.Completed)
        { tray.BalloonTipTitle = "Download complete"; tray.BalloonTipText = row.FileName; tray.ShowBalloonTip(3000); }
        Save(); row.Refresh();
    }
    private IEnumerable<DownloadRow> Selected() => settings.Layout == "Focus" ? FocusDownloads.SelectedItems.Cast<DownloadRow>().ToArray() : Downloads.SelectedItems.Cast<DownloadRow>().ToArray();
    private DownloadRow? CurrentRow => settings.Layout == "Focus" ? FocusDownloads.SelectedItem as DownloadRow : Downloads.SelectedItem as DownloadRow;
    private void AddClick(object sender, RoutedEventArgs e)
    {
        var dialog = new AddDialog(settings) { Owner = this };
        if (dialog.ShowDialog() != true) return;
        foreach (var request in dialog.Requests) AddJob(request, dialog.DirectoryPath, dialog.QueueOnly);
        Save(); Tick();
    }
    private void AddJob(BrowserRequest req, string directory, bool queueOnly = false)
    {
        var uri = DownloadNames.ValidateUrl(req.Url);
        string name = DownloadNames.Sanitize(string.IsNullOrWhiteSpace(req.FileName) ? DownloadNames.FromUrl(uri) : req.FileName);
        var destination = DownloadNames.ReservePath(directory, name, rows.Select(r => r.Job.Destination));
        var job = new DownloadJob { Url = uri.AbsoluteUri, Destination = destination, FileName = Path.GetFileName(destination), Connections = settings.Connections,
            Referer = req.Referer, Status = queueOnly ? DownloadStatus.Paused : DownloadStatus.Queued };
        if (req.Headers != null)
            foreach (var h in req.Headers.Where(h => h.Key is "Authorization" or "Cookie" or "User-Agent"))
                if (h.Value.Length <= 32768 && !h.Value.Contains('\r') && !h.Value.Contains('\n')) job.Headers[h.Key] = h.Value;
        allJobs.Add(job); rows.Add(new DownloadRow(job));
    }
    public void ReceiveBrowserRequest(BrowserRequest req)
    {
        AddJob(req, settings.DownloadDirectory);
        if (!Save()) { allJobs.Remove(rows[^1].Job); rows.RemoveAt(rows.Count - 1); throw new IOException("Cannot persist download request."); }
        ShowWindow(); Tick();
    }
    private void ResumeClick(object sender, RoutedEventArgs e)
    {
        queuePaused = false;
        foreach (var row in Selected().Where(r => !running.ContainsKey(r.Job.Id) && r.Job.Status != DownloadStatus.Completed)) row.Job.Status = DownloadStatus.Queued;
        Tick();
    }
    private void PauseClick(object sender, RoutedEventArgs e)
    {
        foreach (var row in Selected()) Pause(row);
        Save();
    }
    private void Pause(DownloadRow row)
    {
        if (running.TryGetValue(row.Job.Id, out var active)) active.Cts.Cancel();
        else if (row.Job.Status == DownloadStatus.Queued) row.Job.Status = DownloadStatus.Paused;
    }
    private void PauseAll() { queuePaused = true; foreach (var row in rows) Pause(row); }
    private void PauseAllClick(object sender, RoutedEventArgs e) => PauseAll();
    private void RestartClick(object sender, RoutedEventArgs e)
    {
        var selected = Selected().Where(r => r.Job.Status != DownloadStatus.Completed).ToArray();
        if (selected.Length == 0) return;
        if (selected.Any(r => running.ContainsKey(r.Job.Id))) { MessageBox.Show(this, "Pause the selected downloads and wait for them to stop first."); return; }
        if (MessageBox.Show(this, "Discard the selected partial downloads and start fresh?", "Restart downloads", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
        foreach (var row in selected) { try { DownloadEngine.Restart(row.Job); } catch (IOException ex) { MessageBox.Show(this, ex.Message); } }
        queuePaused = false; Tick();
    }
    private void RemoveClick(object sender, RoutedEventArgs e)
    {
        var selected = Selected().ToArray(); if (selected.Length == 0) return;
        if (selected.Any(r => running.ContainsKey(r.Job.Id))) { MessageBox.Show(this, "Pause these downloads and wait for them to stop before removing them."); return; }
        removed.Clear();
        foreach (var row in selected)
        {
            row.Job.Hidden = true; removed.Add(row); rows.Remove(row); lastBytes.Remove(row.Job.Id);
        }
        Save(); ShowNotice("Removed from the list. Downloaded and partial files are kept.", true); UpdateEmptyState();
    }
    private void ClearClick(object sender, RoutedEventArgs e)
    { removed.Clear(); foreach (var row in rows.Where(r => r.Job.Status == DownloadStatus.Completed).ToArray()) { row.Job.Hidden = true; removed.Add(row); rows.Remove(row); lastBytes.Remove(row.Job.Id); } Save(); ShowNotice("Completed entries removed from the list. Files are kept.", true); UpdateEmptyState(); }
    private void OptionsClick(object sender, RoutedEventArgs e)
    {
        var dialog = new OptionsDialog(settings) { Owner = this };
        if (dialog.ShowDialog() == true) { settings = dialog.Result; UpdateSettingsLabels(); ApplyLayout(); Save(); Tick(); }
    }
    private void UpdateSettingsLabels()
    {
        LocationLabel.Content = "Downloads folder"; LocationLabel.ToolTip = settings.DownloadDirectory;
        LimitStatus.Text = settings.SpeedLimitKiB > 0 ? $"Shared limit: {settings.SpeedLimitKiB:N0} KiB/s" : "Bandwidth: unlimited";
        GamingButton.Content = settings.PreviousSpeedLimitKiB.HasValue ? "Gaming mode: on" : "Gaming mode";
        GamingButton.Background = (System.Windows.Media.Brush)FindResource(settings.PreviousSpeedLimitKiB.HasValue ? "Selected" : "Surface");
        BrowserStatus.Content = settings.BrowserCheckedAt.HasValue ? "Browser checked" : "Connect browser";
        BrowserStatus.ToolTip = settings.BrowserCheckedAt.HasValue ? "Last successful browser check: " + settings.BrowserCheckedAt.Value.LocalDateTime.ToString("g") : "Set up Chrome or Edge";
    }
    private void BrowserClick(object sender, RoutedEventArgs e)
    {
        new BrowserSetupDialog(() => settings.BrowserCheckedAt) { Owner = this }.ShowDialog(); UpdateSettingsLabels();
    }
    private void CategoryChanged(object sender, SelectionChangedEventArgs e)
    {
        category = (Categories.SelectedItem as ListBoxItem)?.Content?.ToString() ?? "All downloads";
        if (LibraryTitle != null) LibraryTitle.Text = category;
        if (Downloads?.ItemsSource != null) CollectionViewSource.GetDefaultView(rows).Refresh();
    }
    private void SearchChanged(object sender, TextChangedEventArgs e) { if (Downloads?.ItemsSource != null) CollectionViewSource.GetDefaultView(rows).Refresh(); }
    private void SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateSelection();
    private void UpdateSelection()
    {
        if (CurrentRow is DownloadRow row)
        {
            SelectionTitle.Text = row.FileName + " · " + row.Status; SelectionDetail.Text = row.Detail;
            SelectionProgress.Text = row.Received + " of " + row.Size + " · " + row.Remaining;
            SelectionResume.Text = row.Job.TotalBytes == null ? "Resume support is checked when the transfer starts." : row.Job.SupportsRanges ? $"Resume supported · {row.Job.Segments.Count} connection(s)" : "This server does not support validated resume. An interrupted transfer may need Restart.";
            SelectionHash.Text = row.Job.Sha256 == null ? "" : "Calculated SHA-256: " + row.Job.Sha256 + "\nCompare with the publisher’s checksum to verify authenticity.";
        }
        else { SelectionTitle.Text = "Select a download to see its details"; SelectionDetail.Text = ""; SelectionHash.Text = ""; SelectionProgress.Text = ""; SelectionResume.Text = ""; }
        int count = Selected().Count(); BulkActions.Visibility = count > 1 ? Visibility.Visible : Visibility.Collapsed; SelectionCount.Text = $"{count} selected";
    }
    private void FolderClick(object sender, RoutedEventArgs e)
    {
        if (CurrentRow is not DownloadRow row) return;
        string folder = Path.GetDirectoryName(row.Job.Destination)!;
        if (Directory.Exists(folder)) Process.Start(new ProcessStartInfo("explorer.exe", folder) { UseShellExecute = true });
    }
    private void ReplaceClick(object sender, RoutedEventArgs e)
    {
        if (CurrentRow is not DownloadRow row || row.Job.Status == DownloadStatus.Completed) return;
        if (running.ContainsKey(row.Job.Id)) { MessageBox.Show(this, "Pause this download first."); return; }
        var dialog = new AddDialog(settings, true) { Owner = this };
        if (dialog.ShowDialog() != true) return;
        row.Job.Url = dialog.Requests[0].Url; row.Job.Headers.Clear(); row.Job.Headers = dialog.Requests[0].Headers ?? []; row.Job.Referer = null; row.Job.Error = null;
        row.Job.Status = DownloadStatus.Paused; Save(); row.Refresh();
    }
    private void DetailsClick(object sender, RoutedEventArgs e) { if (CurrentRow is DownloadRow row) new ProgressDialog(row) { Owner = this }.Show(); }
    private void DownloadDoubleClick(object sender, MouseButtonEventArgs e) => DetailsClick(sender, new RoutedEventArgs());
    private async void WindowClosing(object? sender, CancelEventArgs e)
    {
        if (quitting) return;
        e.Cancel = true;
        if (settings.MinimizeToTray) { Hide(); tray.BalloonTipTitle = "OpenDownload is still running"; tray.BalloonTipText = "Right-click the tray icon to exit or pause all downloads."; tray.ShowBalloonTip(2500); }
        else await ExitAsync();
    }
    private async Task ExitAsync()
    {
        if (quitting) return; quitting = true; timer.Stop(); PauseAll();
        await Task.WhenAll(running.Values.Select(v => v.Task).ToArray());
        Save(); tray.Dispose(); store.Dispose(); engine.Dispose(); Close(); Application.Current.Shutdown();
    }
}
