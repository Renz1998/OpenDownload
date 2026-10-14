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
            double expandedListHeight=ListPanel.ActualHeight;
            CloseDetailsClick(this,new()); await Dispatcher.InvokeAsync(UpdateLayout,DispatcherPriority.ApplicationIdle);
            Check(DetailsPanel.Visibility==Visibility.Collapsed && ListPanel.ActualHeight>expandedListHeight+100 && DetailsToggle.Content.ToString()=="Show details","Closing Workbench details gives its space to the download list");
            SelectOne(rows[1]); Check(DetailsPanel.Visibility==Visibility.Collapsed,"Selecting another download leaves closed details hidden");
            Check(!store.LoadSettings().WorkbenchDetailsVisible,"Workbench details visibility persists across settings reload");
            settings.Layout="Focus"; ApplyLayout(); Check(DetailsPanel.Visibility==Visibility.Visible && DetailsToggle.Visibility==Visibility.Collapsed && CloseDetailsButton.Visibility==Visibility.Collapsed,"Focus details remain visible independently of Workbench preference");
            settings.Layout="Workbench"; ApplyLayout(); ToggleDetailsClick(this,new()); SelectOne(rows[0]); await Dispatcher.InvokeAsync(UpdateLayout,DispatcherPriority.ApplicationIdle);
            Check(DetailsPanel.Visibility==Visibility.Visible && store.LoadSettings().WorkbenchDetailsVisible,"Show details restores the panel and saves the preference");
            Capture("workbench.png");
            settings.Layout = "Focus"; ApplyLayout(); SelectOne(rows[0]); await Dispatcher.InvokeAsync(UpdateLayout, DispatcherPriority.ApplicationIdle);
            Check(FocusDownloads.Visibility == Visibility.Visible && LibrarySidebar.Visibility == Visibility.Collapsed && Grid.GetColumn(DetailsPanel) == 1, "Focus uses transfer cards and side details");
            Check(GlobalSettingsButton.IsVisible,"Focus exposes Settings in the shared header");
            settings.BrowserCheckedAt=null; browserSeen=null; UpdateBrowserConnectionUi();
            Check(BrowserConnectBanner.Visibility==Visibility.Visible && LiveBrowserStatus.Content.ToString()=="Connect your browser","Focus exposes an obvious first-time browser connection action");
            RecordBrowserCheck(); UpdateBrowserConnectionUi();
            Check(BrowserConnectBanner.Visibility==Visibility.Collapsed && LiveBrowserStatus.Style==FindResource("QuietButton"),"Connected browser setup becomes a quiet status instead of a banner");
            browserSeen=null; UpdateBrowserConnectionUi(); Check(BrowserConnectBanner.Visibility==Visibility.Collapsed && LiveBrowserStatus.Content.ToString()=="Browser offline","Closing the browser does not bring back first-time setup");
            var firstFocusRow=(DownloadRow)FocusDownloads.Items[0]; FocusDownloads.ScrollIntoView(firstFocusRow); await Dispatcher.InvokeAsync(UpdateLayout,DispatcherPriority.ApplicationIdle);
            var focusItem=(ListBoxItem)FocusDownloads.ItemContainerGenerator.ContainerFromItem(firstFocusRow);
            Check(DownloadActivationRow(FocusDownloads,focusItem)==firstFocusRow && DownloadActivationRow(FocusDownloads,FindVisualButtons(focusItem).First())==null,"Double-click targets the download card and excludes action buttons");

            rows[0].Job.Status=DownloadStatus.Downloading; UpdateTaskbarProgress();
            Check(TaskbarItemInfo.ProgressState==System.Windows.Shell.TaskbarItemProgressState.Normal && Math.Abs(TaskbarItemInfo.ProgressValue-0.65)<0.001,"Taskbar progress follows received bytes");
            rows[0].Job.Status=DownloadStatus.Connecting; UpdateTaskbarProgress();
            Check(TaskbarItemInfo.ProgressState==System.Windows.Shell.TaskbarItemProgressState.Indeterminate,"Taskbar animates while connecting"); rows[0].Job.Status=DownloadStatus.Paused; UpdateTaskbarProgress();
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
            var now=DateTimeOffset.Now;
            var oldFinished=new DownloadRow(new DownloadJob { Added=now,FinishedAt=now.AddHours(-2) });
            var recentlyFinished=new DownloadRow(new DownloadJob { Added=now.AddDays(-1),FinishedAt=now.AddHours(-1) });
            Check(new DownloadComparer("Latest downloaded").Compare(recentlyFinished,oldFinished)<0,"Default sorting uses completion time instead of creation time");
            SortPicker.SelectedItem="Name A-Z"; Check(store.LoadSettings().SortMode=="Name A-Z","Sorting choice persists for both layouts"); SortPicker.SelectedItem="Latest downloaded";
            Check(DownloadRow.FormatSpeed(1_000_000,"MB/s")=="1.0 MB/s" && DownloadRow.FormatSpeed(1_000_000,"Mbps")=="8.0 Mbps","Speed display distinguishes megabytes from megabits");
            var history=new SpeedHistory(); for(int i=0;i<100;i++) history.Add(i); Check(history.Samples.Count==60 && history.Peak==99,"Speed graph retains a bounded recent history");
            var pngRow=AddJob(new BrowserRequest { Url="https://example.invalid/preview.png",FileName="preview.png" },App.DataDirectory,true); pngRow.Job.Status=DownloadStatus.Completed;
            var bitmap=System.Windows.Media.Imaging.BitmapSource.Create(2,2,96,96,System.Windows.Media.PixelFormats.Bgra32,null,new byte[16],8);
            var encoder=new System.Windows.Media.Imaging.PngBitmapEncoder(); encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
            using(var file=File.Create(pngRow.Job.Destination)) encoder.Save(file);
            UpdatePreview(pngRow); Check(pngRow.Category=="Images" && SelectionPreview.Source!=null,"Completed image files have a category and a local preview");
            UpdatePreview(null); File.Delete(pngRow.Job.Destination); allJobs.Remove(pngRow.Job); rows.Remove(pngRow); store.Remove(pngRow.Job.Id);
            using(var window=new WindowCapture(new TransferActivityWindow(_=>{},_=>{})))
            {
                var activity=(TransferActivityWindow)window.Window; activity.Add(rows[0]); activity.Add(rows[1]); activity.Add(rows[0]);
                await Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);
                Check(activity.Transfers.Count==2,"One activity window consolidates multiple transfers without duplicates");
            }
            bool opened=false;
            using(var window=new WindowCapture(new CompletionNotification(rows[0],()=>opened=true,()=>{})))
            {
                FindButtons(window.Window).Single(b=>b.Content?.ToString()=="Open file").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Check(opened,"Completion notification exposes an actionable Open file button");
            }
            RecordBrowserStatus(true,true); UpdateLibraryActivity(); Check(LiveBrowserStatus.Content.ToString()!.Contains("needs attention"),"Recent browser handoff failures are visible");
            RecordBrowserStatus(true,false); UpdateLibraryActivity(); Check(LiveBrowserStatus.Content.ToString()!.Contains("capture on"),"Live browser connection reports capture state");
            var promptRow=AddJob(new BrowserRequest { Url="https://example.invalid/example.PNG",FileName="example.PNG" },App.DataDirectory,true); promptRow.Job.AwaitingDestination=true;
            _=Dispatcher.BeginInvoke(new Action(()=>
            {
                var dialog=Application.Current.Windows.OfType<DestinationDialog>().Single();
                var panel=(StackPanel)((ScrollViewer)dialog.Content).Content;
                panel.Children.OfType<TextBox>().Single().Text=Path.Combine(App.DataDirectory,"images");
                panel.Children.OfType<CheckBox>().Single().IsChecked=true;
                FindButtons(dialog).Single(b=>b.Content?.ToString()=="Start download").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            }),DispatcherPriority.ApplicationIdle);
            destinationPrompts.Enqueue(promptRow); ProcessDestinationPrompts();
            Check(!promptRow.Job.AwaitingDestination && promptRow.Job.Status==DownloadStatus.Queued && settings.TypeFolders.ContainsKey(".png") && !settings.TypeFolders.ContainsKey(".jpg"),"Destination modal remembers only its selected file extension");
            Check(store.LoadSettings().TypeFolders[".png"]==Path.Combine(App.DataDirectory,"images"),"File-type destination persists");
            settings.ShowProgressWindow=true;
            Check(!ShouldShowTransferWindow(promptRow),"Remembered destinations do not reopen the activity popup");
            Check(ShouldShowTransferWindow(rows.First(r=>r.FileName.EndsWith(".safetensors"))),"Unremembered types still honor the activity-window setting");
            var remembered=AddJob(new BrowserRequest { Url="https://example.invalid/next.PNG",FileName="next.PNG" },App.DataDirectory,true); remembered.Job.AwaitingDestination=true;
            destinationPrompts.Enqueue(remembered); ProcessDestinationPrompts();
            Check(!remembered.Job.AwaitingDestination && Path.GetDirectoryName(remembered.Job.Destination)==settings.TypeFolders[".png"],"Repeated remembered type starts without another destination modal");
            allJobs.Remove(remembered.Job); rows.Remove(remembered); store.Remove(remembered.Job.Id);
            var themedMenu=MoreMenu(); themedMenu.PlacementTarget=this; themedMenu.IsOpen=true;
            await Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);
            Check(themedMenu.Template!=null && themedMenu.Items.OfType<MenuItem>().All(item=>item.Template!=null),"Context menu and rows use themed templates without the system icon gutter");
            GuidePictures.Save(themedMenu,(int)themedMenu.ActualWidth,(int)themedMenu.ActualHeight,Path.Combine(output,"context-menu.png"));
            themedMenu.IsOpen=false;
            allJobs.Remove(promptRow.Job); rows.Remove(promptRow); store.Remove(promptRow.Job.Id);
            var cancelled=AddJob(new BrowserRequest { Url="https://example.invalid/example.zip" },App.DataDirectory,true); cancelled.Job.AwaitingDestination=true;
            _=Dispatcher.BeginInvoke(new Action(()=>Application.Current.Windows.OfType<DestinationDialog>().Single().Close()),DispatcherPriority.ApplicationIdle);
            destinationPrompts.Enqueue(cancelled); ProcessDestinationPrompts();
            Check(cancelled.Job.AwaitingDestination && cancelled.Job.Status==DownloadStatus.Paused,"Cancelling the destination prompt keeps a recoverable paused request");
            allJobs.Remove(cancelled.Job); rows.Remove(cancelled); store.Remove(cancelled.Job.Id);
            SearchBox.Text = "not-present"; UpdateEmptyState(); Check(EmptyState.Visibility == Visibility.Visible, "Search empty state is visible"); SearchBox.Text = "";
            Theme.Apply(true); await Dispatcher.InvokeAsync(UpdateLayout, DispatcherPriority.ApplicationIdle); Check(((System.Windows.Media.SolidColorBrush)FindResource("Ink")).Color.R < 50, "Light theme applies readable dark text"); Theme.Apply(false);
            using (var chooser = new WindowCapture(new LayoutChooser())) { await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle); GuidePictures.Save(chooser.Window.Content as System.Windows.Media.Visual ?? chooser.Window, (int)chooser.Window.ActualWidth, (int)chooser.Window.ActualHeight, Path.Combine(output, "layout-choice.png")); }
            GuidePictures.Create(output);
            Check(bindingErrors.Errors.Count == 0, "Both layouts render without binding errors: " + string.Join(";", bindingErrors.Errors));
            File.AppendAllText(Path.Combine(output, "ui-results.txt"), $"{passed} UI checks passed.\n");
            await ExitAsync();
        }
        catch (Exception ex) { Directory.CreateDirectory(output); File.WriteAllText(Path.Combine(output, "ui-failure.txt"), ex.ToString()); await ExitAsync(); Environment.Exit(1); }
        void Capture(string name) { ListPanel.BeginAnimation(UIElement.OpacityProperty,null); DetailsPanel.BeginAnimation(UIElement.OpacityProperty,null); BrowserConnectBanner.BeginAnimation(UIElement.OpacityProperty,null); UpdateLayout(); GuidePictures.Save(Content as System.Windows.Media.Visual ?? this, (int)ActualWidth, (int)ActualHeight, Path.Combine(output, name)); }
    }
    private sealed class BindingErrorListener : TraceListener
    { public List<string> Errors { get; } = []; public override void Write(string? message) { if (message?.Contains("Error:") == true) Errors.Add(message); } public override void WriteLine(string? message) => Write(message); }
    private sealed class WindowCapture : IDisposable
    { public Window Window { get; } public WindowCapture(Window window) { Window = window; window.Show(); } public void Dispose() => Window.Close(); }
    private static IEnumerable<Button> FindVisualButtons(DependencyObject root)
    {
        if(root is Button button) yield return button;
        for(int i=0;i<System.Windows.Media.VisualTreeHelper.GetChildrenCount(root);i++)
            foreach(var child in FindVisualButtons(System.Windows.Media.VisualTreeHelper.GetChild(root,i))) yield return child;
    }
    private static IEnumerable<Button> FindButtons(DependencyObject root)
    {
        if (root is Button button) yield return button;
        foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>()) foreach (var found in FindButtons(child)) yield return found;
    }
}
