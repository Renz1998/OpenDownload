using System.Diagnostics;

namespace OpenDownload.Core;

// A single limiter is shared by every connection and every job.
public sealed class BandwidthLimiter
{
    private readonly object gate = new();
    private long next;
    private int previousLimit;
    public Func<int> LimitKiB { get; set; } = () => 0;
    public async Task WaitAsync(int bytes, CancellationToken cancellationToken)
    {
        int limit = LimitKiB();
        if (limit <= 0) { lock (gate) next = 0; return; }
        double wait;
        lock (gate)
        {
            long now = Stopwatch.GetTimestamp();
            if (previousLimit != limit) { next = now; previousLimit = limit; }
            next = Math.Max(next, now);
            next += (long)(bytes / (limit * 1024d) * Stopwatch.Frequency);
            wait = (next - now) / (double)Stopwatch.Frequency;
        }
        if (wait > 0) await Task.Delay(TimeSpan.FromSeconds(wait), cancellationToken);
    }
}
