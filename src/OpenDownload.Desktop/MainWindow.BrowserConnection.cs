using System.Windows;
namespace OpenDownload.Desktop;

public partial class MainWindow
{
    internal async Task<bool> CheckBrowserConnectionAsync(Func<Task> open,TimeSpan timeout)
    {
        if(checkingBrowser) return false;
        checkingBrowser=true; UpdateBrowserConnectionUi();
        var began=DateTimeOffset.Now;
        try
        {
            await open();
            var deadline=DateTimeOffset.UtcNow+timeout;
            while(DateTimeOffset.UtcNow<deadline)
            {
                if(settings.BrowserCheckedAt is DateTimeOffset response && response>began)
                { browserConnectionFailed=false; return true; }
                await Task.Delay(100);
            }
        }
        catch(Exception ex) when(ex is System.IO.IOException or UnauthorizedAccessException or ArgumentException or System.ComponentModel.Win32Exception or InvalidOperationException) { }
        finally { checkingBrowser=false; UpdateBrowserConnectionUi(); }
        browserConnectionFailed=true; UpdateBrowserConnectionUi();
        ShowNotice("The browser did not confirm its connection. Follow the setup instructions to reconnect it.");
        return false;
    }
    private async void BrowserConnectionClick(object sender,RoutedEventArgs e)
    {
        if(checkingBrowser) return;
        if(!settings.BrowserCheckedAt.HasValue || browserConnectionFailed || browserHandoffError)
        { BrowserClick(sender,e); return; }
        bool connected=await CheckBrowserConnectionAsync(()=>BrowserIntegration.OpenBrowser(settings.PreferredBrowser,true),TimeSpan.FromSeconds(15));
        if(!connected && !quitting)
        {
            var existing=Application.Current.Windows.OfType<BrowserSetupDialog>().FirstOrDefault();
            if(existing!=null) existing.Activate(); else BrowserClick(sender,e);
        }
    }
}
