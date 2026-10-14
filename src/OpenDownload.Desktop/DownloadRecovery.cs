using OpenDownload.Core;
namespace OpenDownload.Desktop;

internal static class DownloadRecovery
{
    internal static (string Action, string Help) Explain(DownloadJob job)
    {
        string error=(job.Error??"").ToLowerInvariant();
        if(error.Contains("restart")) return ("Restart","The source or partial file changed. Restart downloads a fresh copy; existing partial progress will be discarded after confirmation.");
        if(error.Contains("401") || error.Contains("403")) return ("Update link","This server refused access. The link may have expired or need a login. Copy a fresh download link, or add the required authentication under Add download > Authentication.");
        if(error.Contains("404") || error.Contains("410")) return ("Update link","The file is no longer available at this address. Copy a new download link from the website.");
        if(error.Contains("free space") || error.Contains("permission") || error.Contains("denied") || error.Contains("destination already")) return ("Check folder","Check free space and folder permissions. Move an existing file out of the way if the name is already taken, then retry. Change the destination under the Fix failed download menu.");
        if(error.Contains("429")) return ("Retry","The server is limiting requests. Wait a few minutes and try again. Fewer connections can help.");
        if(error.Contains("encoded file") || error.Contains("insecure") || error.Contains("invalid range") || error.Contains("size did not")) return ("Update link","The server response could not be downloaded safely. Try a fresh direct file link or use your browser for this file.");
        return ("Retry","The connection was interrupted or the server did not respond. Check your internet connection and try again; validated partial progress is kept.");
    }
}
