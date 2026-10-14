using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace OpenDownload.Desktop;

public partial class MainWindow
{
    internal static readonly string[] SortModes=["Latest downloaded","Newest added","Oldest added","Name A-Z","File type","Largest first"];
    private readonly SpeedHistory speedHistory=new();
    private string? previewKey;
    private CompletionNotification? completionNotice;
    private DateTimeOffset? browserSeen;
    private bool browserCapture;
    private bool browserHandoffError;
    private void InitializeLibrary()
    {
        SortPicker.ItemsSource=SortModes; SortPicker.SelectedItem=SortModes.Contains(settings.SortMode) ? settings.SortMode : SortModes[0];
        UnitPicker.ItemsSource=new[]{"MB/s","MiB/s","Mbps"}; UnitPicker.SelectedItem=new[]{"MB/s","MiB/s","Mbps"}.Contains(settings.SpeedUnit) ? settings.SpeedUnit : "MB/s";
        ApplySort();
        SpeedChart.SizeChanged+=(_,_)=>RenderSpeedChart();
    }
    private void ApplySort()
    {
        if(CollectionViewSource.GetDefaultView(rows) is ListCollectionView view) view.CustomSort=new DownloadComparer(settings.SortMode);
    }
    private void SortChanged(object sender,SelectionChangedEventArgs e)
    {
        if(settings==null || SortPicker.SelectedItem is not string mode) return;
        settings.SortMode=mode; ApplySort(); Save();
    }
    private void UnitChanged(object sender,SelectionChangedEventArgs e)
    {
        if(settings==null || UnitPicker.SelectedItem is not string unit) return;
        settings.SpeedUnit=unit; foreach(var row in rows) { row.SpeedUnit=unit; row.Refresh(); }
        RenderSpeedChart(); UpdateSettingsLabels(); Save();
    }
    private void UpdateLibraryActivity()
    {
        double speed=rows.Sum(r=>r.Job.BytesPerSecond); speedHistory.Add(speed); RenderSpeedChart();
        UpdateBrowserConnectionUi();
    }
    private void UpdateBrowserConnectionUi()
    {
        bool live=browserSeen.HasValue && DateTimeOffset.Now-browserSeen.Value<TimeSpan.FromSeconds(90);
        bool attention=live && browserHandoffError;
        bool connectedBefore=live || settings.BrowserCheckedAt.HasValue;
        bool showConnect=settings.Layout=="Focus" && (!connectedBefore || attention);
        var bannerVisibility=showConnect ? Visibility.Visible : Visibility.Collapsed;
        if(BrowserConnectBanner.Visibility!=bannerVisibility) { BrowserConnectBanner.Visibility=bannerVisibility; if(showConnect) Motion.Reveal(BrowserConnectBanner); }
        BrowserConnectHint.Text=attention ? "Your browser could not send a download. Click Connect your browser to check the setup." : "Connect Chrome or Edge once. Click Connect your browser to see the steps.";
        LiveBrowserStatus.Content=live ? attention ? "Browser needs attention" : browserCapture ? "Browser connected · capture on" : "Browser connected" : connectedBefore ? "Browser offline" : "Connect your browser";
        LiveBrowserStatus.Style=(Style)FindResource(connectedBefore && !attention ? "QuietButton" : "PrimaryButton");
        BrowserStatus.Content=live ? attention ? "Browser needs attention" : "Browser connected" : connectedBefore ? "Browser offline" : "Connect your browser";
        LiveBrowserStatus.ToolTip=live ? "Connected to your browser. Click to view setup or troubleshoot." : connectedBefore ? "Open your browser and the connection will refresh. Click here if you need help." : "Click to connect Chrome or Edge. The setup guide walks you through each step.";

    }
    private void RenderSpeedChart()
    {
        double width=Math.Max(1,SpeedChart.ActualWidth), height=40;
        var samples=speedHistory.Samples.ToArray(); double peak=Math.Max(1,speedHistory.Peak);
        var points=new PointCollection();
        for(int i=0;i<samples.Length;i++) points.Add(new Point(width*i/Math.Max(1,samples.Length-1),height-height*samples[i]/peak));
        SpeedLine.Points=points;
        SpeedOverview.Visibility=running.Count>0 || speedHistory.Peak>0 ? Visibility.Visible : Visibility.Collapsed;
        SpeedReadout.Text=DownloadRow.FormatSpeed(samples.LastOrDefault(),settings.SpeedUnit)+" now / "+DownloadRow.FormatSpeed(speedHistory.Peak,settings.SpeedUnit)+" peak";
        SpeedCap.Text=settings.SpeedLimitKiB>0 ? "Shared cap: "+DownloadRow.FormatSpeed(settings.SpeedLimitKiB*1024d,settings.SpeedUnit) : "No app bandwidth cap";
    }
    private void UpdatePreview(DownloadRow? row)
    {
        string key=row==null ? "none" : row.Job.Id+":"+row.Job.Status;
        if(previewKey==key) return; previewKey=key; SelectionPreview.Source=null; SelectionPreview.Visibility=Visibility.Collapsed;
        if(row?.Category!="Images" || !row.IsCompleted || !File.Exists(row.Job.Destination)) return;
        try
        {
            if(new FileInfo(row.Job.Destination).Length>50*1024*1024) return;
            var picture=new BitmapImage(); picture.BeginInit(); picture.CacheOption=BitmapCacheOption.OnLoad; picture.DecodePixelWidth=420; picture.UriSource=new Uri(Path.GetFullPath(row.Job.Destination)); picture.EndInit(); picture.Freeze();
            SelectionPreview.Source=picture; SelectionPreview.Visibility=Visibility.Visible;
        }
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or NotSupportedException or System.Runtime.InteropServices.COMException or FileFormatException or ArgumentException or InvalidOperationException) { }
    }
    private void OpenFileClick(object sender,RoutedEventArgs e) { if(CurrentRow is DownloadRow row) OpenDownloadedFile(row); }
    internal static void OpenDownloadedFile(DownloadRow row)
    {
        if(!row.IsCompleted || !File.Exists(row.Job.Destination)) { MessageBox.Show("The completed file is not available. Open its folder to check."); return; }
        try { Process.Start(new ProcessStartInfo(row.Job.Destination) { UseShellExecute=true }); }
        catch(Exception ex) when(ex is IOException or System.ComponentModel.Win32Exception) { MessageBox.Show(ex.Message,"Could not open file"); }
    }
    internal static void OpenContainingFolder(DownloadRow row)
    {
        string? folder=Path.GetDirectoryName(row.Job.Destination); if(folder==null || !Directory.Exists(folder)) return;
        Process.Start(new ProcessStartInfo("explorer.exe") { UseShellExecute=true,ArgumentList={folder} });
    }
    private void NotifyCompleted(DownloadRow row)
    {
        if(!settings.CompletionNotifications || Environment.GetEnvironmentVariable("OPENDOWNLOAD_TEST_MODE")=="1") return;
        completionNotice?.Close();
        completionNotice=new CompletionNotification(row,()=>OpenDownloadedFile(row),()=>OpenContainingFolder(row)); completionNotice.Show();
    }
    public void RecordBrowserStatus(bool capture,bool error)
    { browserSeen=DateTimeOffset.Now; browserCapture=capture; browserHandoffError=error; }
}
