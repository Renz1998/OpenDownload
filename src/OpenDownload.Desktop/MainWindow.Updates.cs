using System.Diagnostics;
using System.Windows;
namespace OpenDownload.Desktop;

public partial class MainWindow
{
    private string? availableVersion;
    private bool checkingUpdate;
    private async Task CheckUpdatesAsync(bool manual)
    {
        if(checkingUpdate || Environment.GetEnvironmentVariable("OPENDOWNLOAD_TEST_MODE")=="1") return;
        if(!manual && (!settings.CheckUpdates || settings.LastUpdateCheck is DateTimeOffset last && DateTimeOffset.Now-last<TimeSpan.FromHours(24))) return;
        checkingUpdate=true; UpdateButton.IsEnabled=false;
        try
        {
            availableVersion=await UpdateService.CheckAsync(typeof(App).Assembly.GetName().Version!);
            settings.LastUpdateCheck=DateTimeOffset.Now; Save();
            UpdateButton.Content=availableVersion==null ? "Check for updates" : "Download "+availableVersion;
            UpdateButton.ToolTip=availableVersion==null ? "Check GitHub for a newer installer" : "Open the official GitHub installer download. Close OpenDownload with Exit before upgrading.";
            if(manual || availableVersion!=null) ShowNotice(availableVersion==null ? "You have the newest published OpenDownload installer." : "OpenDownload "+availableVersion+" is available. Click Download "+availableVersion+" at the top to get the installer.");
        }
        catch(Exception ex) when(ex is System.Net.Http.HttpRequestException or TaskCanceledException or System.Text.Json.JsonException or InvalidOperationException or KeyNotFoundException or ArgumentException)
        { if(manual) ShowNotice("Could not check for updates. Try again later or visit the GitHub releases page."); }
        finally { checkingUpdate=false; UpdateButton.IsEnabled=true; }
    }
    private async void UpdateClick(object sender,RoutedEventArgs e)
    {
        if(availableVersion==null) { await CheckUpdatesAsync(true); return; }
        try { Process.Start(new ProcessStartInfo($"https://github.com/Renz1998/OpenDownload/releases/download/v{availableVersion}/OpenDownload-{availableVersion}-Setup.exe") { UseShellExecute=true }); }
        catch(System.ComponentModel.Win32Exception) { ShowNotice("Open github.com/Renz1998/OpenDownload/releases in your browser to download the update."); }
    }
}
