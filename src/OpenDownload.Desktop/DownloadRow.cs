using System.ComponentModel;
using OpenDownload.Core;

namespace OpenDownload.Desktop;

public sealed class DownloadRow(DownloadJob job) : INotifyPropertyChanged
{
    public DownloadJob Job { get; } = job;
    public string FileName => Job.FileName;
    public string SpeedUnit { get; set; } = "MB/s";
    public bool CanPause => Job.Status is DownloadStatus.Downloading or DownloadStatus.Connecting;
    public bool CanResume => !Job.AwaitingDestination && Job.Status is DownloadStatus.Paused or DownloadStatus.Failed;
    public bool IsCompleted => Job.Status==DownloadStatus.Completed;
    public string FileIconGeometry => Category switch
    {
        "Images"=>"M2,3 L22,3 22,21 2,21 Z M2,17 L8,10 13,15 17,11 22,17 M16,7 L17,7",
        "Video"=>"M2,3 L22,3 22,21 2,21 Z M9,7 L17,12 9,17 Z",
        "Music"=>"M9,18 L9,4 21,2 21,16 M9,6 L21,4 M3,18 A3,2 0 1 0 9,18 A3,2 0 1 0 3,18 M15,16 A3,2 0 1 0 21,16 A3,2 0 1 0 15,16",
        "Archives"=>"M4,2 L20,2 20,22 4,22 Z M10,3 L14,3 M10,7 L14,7 M10,11 L14,11 M10,15 L14,15 14,19 10,19 Z",
        "AI models"=>"M5,5 L19,5 19,19 5,19 Z M9,9 L15,9 15,15 9,15 Z M8,1 L8,5 M16,1 L16,5 M8,19 L8,23 M16,19 L16,23 M1,8 L5,8 M1,16 L5,16 M19,8 L23,8 M19,16 L23,16",
        _=>"M4,2 L15,2 21,8 21,22 4,22 Z M15,2 L15,8 21,8 M8,12 L17,12 M8,16 L17,16"
    };
    public string Category => Job.Category;
    public string Status => Job.Status == DownloadStatus.Failed ? "Needs attention" : Job.Status == DownloadStatus.Downloading && Job.TotalBytes > 0 && Job.Downloaded == Job.TotalBytes ? "Checking file" : Job.Status.ToString();
    public string Source => Uri.TryCreate(Job.Url, UriKind.Absolute, out var uri) ? uri.Host : "";
    public string Subtitle => Size + " · " + Source;
    public string ActionLabel => Job.Status is DownloadStatus.Connecting or DownloadStatus.Downloading ? "Pause" : Job.Status == DownloadStatus.Completed ? "Folder" : Job.Status==DownloadStatus.Failed ? DownloadRecovery.Explain(Job).Action : "Resume";
    public string TransferSummary => Percentage + " · " + (Job.BytesPerSecond > 0 ? Speed + " · " : "") + Remaining;
    public string Size => Job.TotalBytes.HasValue ? FormatBytes(Job.TotalBytes.Value) : "Unknown";
    public string Received => FormatBytes(Job.Downloaded);
    public double Progress => Job.TotalBytes > 0 ? Job.Downloaded * 100d / Job.TotalBytes.Value : Job.Status == DownloadStatus.Completed ? 100 : 0;
    public string Percentage => $"{Progress:0.0}%";
    public string Speed => Job.BytesPerSecond > 0 ? FormatSpeed(Job.BytesPerSecond,SpeedUnit) : "Idle";
    public static string FormatSpeed(double bytes,string unit) => unit switch
    { "Mbps"=>$"{bytes*8/1_000_000:0.0} Mbps", "MiB/s"=>$"{bytes/1_048_576:0.0} MiB/s", _=>$"{bytes/1_000_000:0.0} MB/s" };
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
