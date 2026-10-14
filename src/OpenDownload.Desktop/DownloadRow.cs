using System.ComponentModel;
using OpenDownload.Core;

namespace OpenDownload.Desktop;

public sealed class DownloadRow(DownloadJob job) : INotifyPropertyChanged
{
    public DownloadJob Job { get; } = job;
    public string FileName => Job.FileName;
    public string Category => Job.Category;
    public string Status => Job.Status == DownloadStatus.Failed ? "Needs attention" : Job.Status == DownloadStatus.Downloading && Job.TotalBytes > 0 && Job.Downloaded == Job.TotalBytes ? "Checking file" : Job.Status.ToString();
    public string Source => Uri.TryCreate(Job.Url, UriKind.Absolute, out var uri) ? uri.Host : "";
    public string Subtitle => Size + " · " + Source;
    public string ActionLabel => Job.Status is DownloadStatus.Connecting or DownloadStatus.Downloading ? "Pause" : Job.Status == DownloadStatus.Completed ? "Folder" : Job.Status == DownloadStatus.Failed ? Job.Error?.Contains("Restart", StringComparison.OrdinalIgnoreCase) == true ? "Restart" : Job.Error?.Contains("HTTP 403") == true || Job.Error?.Contains("HTTP 401") == true ? "Update link" : "Retry" : "Resume";
    public string TransferSummary => Percentage + " · " + (Job.BytesPerSecond > 0 ? Speed + " · " : "") + Remaining;
    public string Size => Job.TotalBytes.HasValue ? FormatBytes(Job.TotalBytes.Value) : "Unknown";
    public string Received => FormatBytes(Job.Downloaded);
    public double Progress => Job.TotalBytes > 0 ? Job.Downloaded * 100d / Job.TotalBytes.Value : Job.Status == DownloadStatus.Completed ? 100 : 0;
    public string Percentage => $"{Progress:0.0}%";
    public string Speed => Job.BytesPerSecond > 0 ? FormatBytes((long)Job.BytesPerSecond) + "/s" : "—";
    public string Remaining
    {
        get
        {
            if (!Job.TotalBytes.HasValue || Job.BytesPerSecond <= 0) return Job.Status == DownloadStatus.Completed ? Job.StartedAt.HasValue ? "Done in " + Duration(Job.ActiveSeconds) : "Downloaded" : "—";
            var span = TimeSpan.FromSeconds(Math.Min(86400000, Math.Max(0, Job.TotalBytes.Value - Job.Downloaded) / Job.BytesPerSecond));
            return span.TotalDays >= 1 ? $"{(int)span.TotalDays}d {span.Hours}h left" : span.TotalHours >= 1 ? $"{span.Hours}h {span.Minutes}m left" : span.TotalMinutes >= 1 ? $"{span.Minutes}m {span.Seconds}s left" : $"{span.Seconds}s left";
        }
    }
    public string Added => Job.Added.LocalDateTime.ToString("g");
    public string Timing => "Added: " + Added + "\nStarted: " + (Job.StartedAt?.LocalDateTime.ToString("G") ?? "Not recorded") +
        "\nFinished: " + (Job.FinishedAt?.LocalDateTime.ToString("G") ?? (Job.Status == DownloadStatus.Completed ? "Not recorded for this older download" : "Not finished")) +
        "\nDownload time: " + (Job.StartedAt.HasValue ? Duration(Job.ActiveSeconds) + " active" : "Not recorded") +
        (Job.StartedAt.HasValue && Job.FinishedAt.HasValue ? " / " + Duration((Job.FinishedAt.Value-Job.StartedAt.Value).TotalSeconds) + " elapsed" : "");
    private static string Duration(double seconds)
    {
        var value=TimeSpan.FromSeconds(Math.Max(0,seconds));
        return value.TotalHours>=1 ? $"{(int)value.TotalHours}h {value.Minutes}m {value.Seconds}s" : value.TotalMinutes>=1 ? $"{value.Minutes}m {value.Seconds}s" : $"{value.TotalSeconds:0.0}s";
    }
    public string Detail => Job.Error ?? Job.Destination;
    public event PropertyChangedEventHandler? PropertyChanged;
    public void Refresh() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(""));
    public static string FormatBytes(long value)
    {
        string[] units = ["B", "KiB", "MiB", "GiB", "TiB"];
        double number = value; int i = 0;
        while (number >= 1024 && i < units.Length - 1) { number /= 1024; i++; }
        return $"{number:0.##} {units[i]}";
    }
}
