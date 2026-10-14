using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using OpenDownload.Core;

namespace OpenDownload.Desktop;
public partial class MainWindow
{
    private void ApplyLayout()
    {
        bool focus = settings.Layout == "Focus";
        bool changingLayout=FocusDownloads.Visibility != (focus ? Visibility.Visible : Visibility.Collapsed);
        bool showingDetails=DetailsPanel.Visibility!=Visibility.Visible && (focus || settings.WorkbenchDetailsVisible);
        LayoutButton.Content = (focus ? "Focus" : "Workbench") + " ▾";
        LibrarySidebar.Visibility = focus ? Visibility.Collapsed : Visibility.Visible;
        SidebarColumn.Width = new GridLength(focus ? 0 : 185);
        FocusFilters.Visibility = focus ? Visibility.Visible : Visibility.Collapsed;
        Downloads.Visibility = focus ? Visibility.Collapsed : Visibility.Visible;
        FocusDownloads.Visibility = focus ? Visibility.Visible : Visibility.Collapsed;
        DetailsPanel.Visibility = focus || settings.WorkbenchDetailsVisible ? Visibility.Visible : Visibility.Collapsed;
        DetailsToggle.Visibility = focus ? Visibility.Collapsed : Visibility.Visible;
        CloseDetailsButton.Visibility = focus ? Visibility.Collapsed : Visibility.Visible;
        DetailsToggle.Content = settings.WorkbenchDetailsVisible ? "Hide details" : "Show details";
        DetailsColumn.Width = new GridLength(focus ? 280 : 0);
        Grid.SetColumn(DetailsPanel, focus ? 1 : 0); Grid.SetRow(DetailsPanel, focus ? 0 : 1);
        Grid.SetRowSpan(DetailsPanel, focus ? 2 : 1);
        DetailsPanel.Margin = focus ? new Thickness(18, 0, 0, 0) : new Thickness(0, 18, 0, 0);
        DetailsPanel.MaxHeight = focus ? double.PositiveInfinity : 225;
        Downloads.RowHeight = settings.CompactRows ? 58 : 76;
        UpdateSelection(); UpdateEmptyState(); UpdateBrowserConnectionUi();
        if(changingLayout) Motion.Reveal(ListPanel);
        if(showingDetails) Motion.Reveal(DetailsPanel);
    }
    private void ToggleDetailsClick(object sender, RoutedEventArgs e)
    {
        settings.WorkbenchDetailsVisible = !settings.WorkbenchDetailsVisible;
        ApplyLayout(); Save();
        if (!settings.WorkbenchDetailsVisible) DetailsToggle.Focus();
    }
    private void CloseDetailsClick(object sender, RoutedEventArgs e)
    {
        settings.WorkbenchDetailsVisible = false; ApplyLayout(); Save(); DetailsToggle.Focus();
    }
    private void ChooseLayout()
    {
        var selected = CurrentRow;
        var chooser = new LayoutChooser(settings.Layout) { Owner = this };
        if (chooser.ShowDialog() != true) return;
        settings.Layout = chooser.Choice; ApplyLayout();
        if (selected != null) SelectOne(selected);
        Save();
    }
    private void LayoutClick(object sender, RoutedEventArgs e) => ChooseLayout();
    private void ThemeClick(object sender, RoutedEventArgs e) { settings.LightTheme = !settings.LightTheme; Theme.Apply(settings.LightTheme); UpdateSettingsLabels(); Save(); }
    private async void ExitClick(object sender, RoutedEventArgs e) => await ExitAsync();
    public Task ExitForUpdateAsync() => ExitAsync();
    private void GamingClick(object sender, RoutedEventArgs e)
    {
        if (settings.PreviousSpeedLimitKiB.HasValue) { settings.SpeedLimitKiB = settings.PreviousSpeedLimitKiB.Value; settings.PreviousSpeedLimitKiB = null; }
        else { settings.PreviousSpeedLimitKiB = settings.SpeedLimitKiB; settings.SpeedLimitKiB = Math.Max(1, settings.GamingLimitKiB); }
        UpdateSettingsLabels(); Save(); ShowNotice(settings.PreviousSpeedLimitKiB.HasValue ? $"Gaming mode on. All transfers share {settings.SpeedLimitKiB:N0} KiB/s. Change this cap in Settings." : "Gaming mode off. Your previous bandwidth setting is restored.");
    }
    private void FocusFilterClick(object sender, RoutedEventArgs e)
    {
        category = ((Button)sender).Content.ToString()!;
        var item = Categories.Items.Cast<ListBoxItem>().FirstOrDefault(i => i.Content.ToString() == category);
        if (item != null) Categories.SelectedItem = item;
        LibraryTitle.Text = category; CollectionViewSourceRefresh(); UpdateEmptyState();
    }
    private void CollectionViewSourceRefresh() => System.Windows.Data.CollectionViewSource.GetDefaultView(rows).Refresh();
    private void UpdateEmptyState()
    {
        var view = System.Windows.Data.CollectionViewSource.GetDefaultView(rows);
        EmptyState.Visibility = view.IsEmpty ? Visibility.Visible : Visibility.Collapsed;
        var labels = (StackPanel)EmptyState.Child;
        ((TextBlock)labels.Children[0]).Text = rows.Count == 0 ? "Your downloads start here" : "No downloads in this view";
        ((TextBlock)labels.Children[1]).Text = rows.Count == 0 ? "Add a URL or send a link from your browser." : "Try another filter or search term.";
    }
    private void FocusSelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateSelection();
    private void SelectOne(DownloadRow row)
    {
        if (settings.Layout == "Focus") { FocusDownloads.SelectedItems.Clear(); FocusDownloads.SelectedItem = row; }
        else { Downloads.SelectedItems.Clear(); Downloads.SelectedItem = row; }
    }
    private void RowActionClick(object sender, RoutedEventArgs e)
    {
        if (((Button)sender).Tag is not DownloadRow row) return;
        SelectOne(row);
        switch (row.ActionLabel)
        {
            case "Pause": PauseClick(sender, e); break;
            case "Folder": FolderClick(sender, e); break;
            case "Update link": ReplaceClick(sender, e); break;
            case "Restart": RestartClick(sender, e); break;
            default: ResumeClick(sender, e); break;
        }
    }
    private ContextMenu MoreMenu()
    {
        var menu = new ContextMenu();
        void Add(string title, RoutedEventHandler handler) { var item = new MenuItem { Header = title }; item.Click += handler; menu.Items.Add(item); }
        Add("Open downloaded file",OpenFileClick); Add("Advanced details", DetailsClick); Add("Update download link", ReplaceClick); Add("Restart incomplete download…", RestartClick);
        menu.Items.Add(new Separator()); Add("Remove from list — keep files", RemoveClick); Add("Remove and delete files...", DeleteClick); Add("Clear completed entries", ClearClick);
        Add("Restore removed entries", (_, _) =>
        {
            foreach (var job in allJobs.Where(j => j.Hidden)) { job.Hidden = false; rows.Add(new DownloadRow(job)); }
            removed.Clear(); Save(); UpdateEmptyState(); ShowNotice("Removed entries restored. Files have not been changed.");
        });
        return menu;
    }
    private void RowMoreClick(object sender, RoutedEventArgs e)
    {
        var button = (Button)sender; if (button.Tag is DownloadRow row && !Selected().Contains(row)) SelectOne(row);
        var menu = MoreMenu(); menu.PlacementTarget = button; menu.IsOpen = true;
    }
    private void BulkMoreClick(object sender, RoutedEventArgs e) { var menu = MoreMenu(); menu.PlacementTarget = (Button)sender; menu.IsOpen = true; }
    private void ShowNotice(string message, bool undo = false)
    { Notice.Text = message; UndoButton.Visibility = undo ? Visibility.Visible : Visibility.Collapsed; NoticeBar.Visibility = Visibility.Visible; Motion.Reveal(NoticeBar); }
    private void UndoClick(object sender, RoutedEventArgs e)
    { foreach (var row in removed) { row.Job.Hidden = false; rows.Add(row); } removed.Clear(); Save(); UpdateEmptyState(); ShowNotice("Entries restored."); }
    private void DefaultFolderClick(object sender, RoutedEventArgs e)
    { Directory.CreateDirectory(settings.DownloadDirectory); Process.Start(new ProcessStartInfo("explorer.exe") { UseShellExecute = true, ArgumentList = { settings.DownloadDirectory } }); }
    public void RecordBrowserCheck(bool capture=false) { RecordBrowserStatus(capture,false); settings.BrowserCheckedAt = DateTimeOffset.Now; UpdateSettingsLabels(); Save(); }
}
