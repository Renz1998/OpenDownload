using System.Collections;
namespace OpenDownload.Desktop;
internal sealed class DownloadComparer(string mode) : IComparer
{
    public int Compare(object? x,object? y)
    {
        var a=(DownloadRow)x!; var b=(DownloadRow)y!;
        int result=mode switch
        {
            "Newest added"=>b.Job.Added.CompareTo(a.Job.Added),
            "Oldest added"=>a.Job.Added.CompareTo(b.Job.Added),
            "Name A-Z"=>StringComparer.OrdinalIgnoreCase.Compare(a.FileName,b.FileName),
            "File type"=>StringComparer.OrdinalIgnoreCase.Compare(System.IO.Path.GetExtension(a.FileName),System.IO.Path.GetExtension(b.FileName)),
            "Largest first"=>(b.Job.TotalBytes??0).CompareTo(a.Job.TotalBytes??0),
            _=>(b.Job.FinishedAt??b.Job.Added).CompareTo(a.Job.FinishedAt??a.Job.Added)
        };
        return result!=0 ? result : StringComparer.Ordinal.Compare(a.Job.Id,b.Job.Id);
    }
}
internal sealed class SpeedHistory
{
    private readonly Queue<double> samples=[];
    public IReadOnlyCollection<double> Samples=>samples;
    public double Peak=>samples.Count==0 ? 0 : samples.Max();
    public void Add(double bytesPerSecond) { samples.Enqueue(Math.Max(0,bytesPerSecond)); while(samples.Count>60) samples.Dequeue(); }
}
