using System.ComponentModel;
using OpenDownload.Core;

namespace OpenDownload.Desktop;

public sealed class DownloadRow(DownloadJob job) : INotifyPropertyChanged
{
    public DownloadJob Job { get; } = job;
    public string FileName => Job.FileName;
    public string Category => Job.Category;
    public string Status => Job.Status.ToString();
    public string Size => Job.TotalBytes.HasValue ? FormatBytes(Job.TotalBytes.Value) : "Unknown";
    public string Received => FormatBytes(Job.Downloaded);
    public double Progress => Job.TotalBytes > 0 ? Job.Downloaded * 100d / Job.TotalBytes.Value : Job.Status == DownloadStatus.Completed ? 100 : 0;
    public string Percentage => $"{Progress:0.0}%";
    public string Speed => Job.BytesPerSecond > 0 ? FormatBytes((long)Job.BytesPerSecond) + "/s" : "—";
    public string Remaining => Job.TotalBytes.HasValue && Job.BytesPerSecond > 0 ? TimeSpan.FromSeconds(Math.Min(86400000, Math.Max(0, Job.TotalBytes.Value - Job.Downloaded) / Job.BytesPerSecond)).ToString(@"d\:hh\:mm\:ss") : "—";
    public string Added => Job.Added.LocalDateTime.ToString("g");
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
