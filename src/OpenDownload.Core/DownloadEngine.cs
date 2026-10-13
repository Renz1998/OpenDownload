using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;

namespace OpenDownload.Core;

public sealed class SourceChangedException(string message) : IOException(message);
public sealed class DownloadEngine : IDisposable
{
    private readonly HttpClient client;
    public BandwidthLimiter Limiter { get; } = new();
    public DownloadEngine()
    {
        client = new HttpClient(new SocketsHttpHandler
        {
            AutomaticDecompression = DecompressionMethods.None,
            AllowAutoRedirect = false, UseCookies = false,
            ConnectTimeout = TimeSpan.FromSeconds(20), MaxConnectionsPerServer = 16
        }) { Timeout = Timeout.InfiniteTimeSpan };
    }
    private async Task<HttpResponseMessage> SendAsync(DownloadJob job, long? start, long? end, bool resume, CancellationToken ct)
    {
        Uri original = DownloadNames.ValidateUrl(job.Url), current = original;
        for (int hop = 0; hop < 10; hop++)
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, current);
            req.Headers.UserAgent.ParseAdd("OpenDownload/0.1");
            req.Headers.TryAddWithoutValidation("Accept-Encoding", "identity");
            // Never forward authentication to a different origin after a redirect.
            if (current.GetLeftPart(UriPartial.Authority) == original.GetLeftPart(UriPartial.Authority))
            {
                foreach (var h in job.Headers)
                    if (h.Key is "Cookie" or "Authorization" or "User-Agent") req.Headers.TryAddWithoutValidation(h.Key, h.Value);
                if (Uri.TryCreate(job.Referer, UriKind.Absolute, out var referer) && referer.Scheme is "https" or "http") req.Headers.Referrer = referer;
            }
            if (start.HasValue) req.Headers.Range = new RangeHeaderValue(start, end);
            if (resume && job.Validator != null) req.Headers.TryAddWithoutValidation("If-Range", job.Validator);
            using var headerTimeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            headerTimeout.CancelAfter(TimeSpan.FromSeconds(45));
            var response = await client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, headerTimeout.Token);
            if (response.StatusCode is HttpStatusCode.MovedPermanently or HttpStatusCode.Redirect or HttpStatusCode.SeeOther or HttpStatusCode.TemporaryRedirect or HttpStatusCode.PermanentRedirect)
            {
                var location = response.Headers.Location;
                response.Dispose();
                if (location == null) throw new HttpRequestException("Redirect has no destination.");
                var target = new Uri(current, location);
                DownloadNames.ValidateUrl(target.AbsoluteUri);
                if (current.Scheme == "https" && target.Scheme == "http") throw new HttpRequestException("Refusing an insecure HTTPS-to-HTTP redirect.");
                current = target;
                continue;
            }
            return response;
        }
        throw new HttpRequestException("Too many redirects.");
    }
    private static string? ValidatorOf(HttpResponseMessage response)
    {
        var tag = response.Headers.ETag;
        return tag is { IsWeak: false } ? tag.ToString() : response.Content.Headers.LastModified?.ToString("R");
    }
    private static void CheckEncoding(HttpResponseMessage response)
    {
        if (response.Content.Headers.ContentEncoding.Any(x => !x.Equals("identity", StringComparison.OrdinalIgnoreCase)))
            throw new IOException("Server returned an encoded file despite an identity request; download stopped to protect file integrity.");
    }
    private async Task ProbeAsync(DownloadJob job, CancellationToken ct)
    {
        using var response = await SendAsync(job, 0, 0, false, ct);
        if (response.StatusCode == HttpStatusCode.RequestedRangeNotSatisfiable && response.Content.Headers.ContentRange?.Length == 0)
        { job.TotalBytes = 0; job.SupportsRanges = false; return; }
        response.EnsureSuccessStatusCode();
        CheckEncoding(response);
        var range = response.Content.Headers.ContentRange;
        bool ranged = response.StatusCode == HttpStatusCode.PartialContent;
        if (ranged && (range?.From != 0 || range.To != 0 || range.Length is null || range.Unit != "bytes" || range.Length < 1))
            throw new IOException("Server returned an invalid range response.");
        long? length = ranged ? range!.Length : response.Content.Headers.ContentLength;
        string? validator = ValidatorOf(response);
        if (job.Downloaded > 0 && (job.TotalBytes != length || job.Validator == null || job.Validator != validator || !ranged))
            throw new SourceChangedException("The source changed or cannot safely resume. Use Restart to download a fresh copy.");
        job.TotalBytes = length;
        job.Validator = validator;
        // Only split a file when we can prove all connections see the same version.
        job.SupportsRanges = ranged && validator != null;
    }
    public async Task RunAsync(DownloadJob job, CancellationToken cancellationToken, Func<Task>? checkpoint = null)
    {
        job.Error = null; job.Status = DownloadStatus.Connecting;
        try
        {
            await ProbeAsync(job, cancellationToken);
            Directory.CreateDirectory(Path.GetDirectoryName(job.Destination)!);
            if (File.Exists(job.Destination)) throw new IOException("The destination already exists. Choose a different name.");
            bool resuming = job.Downloaded > 0;
            if (resuming && (!File.Exists(job.PartialPath) || (job.TotalBytes.HasValue && new FileInfo(job.PartialPath).Length != job.TotalBytes)))
                throw new IOException("Partial file is missing or has the wrong size. Restart this download.");
            if (!resuming)
            {
                using var part = new FileStream(job.PartialPath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
                if (job.TotalBytes.HasValue)
                {
                    var drive = new DriveInfo(Path.GetPathRoot(Path.GetFullPath(job.Destination))!);
                    if (drive.IsReady && drive.AvailableFreeSpace < job.TotalBytes.Value + 16 * 1024 * 1024) throw new IOException("Not enough free space for this download.");
                    part.SetLength(job.TotalBytes.Value);
                }
                job.Segments = [];
                if (job.TotalBytes == 0) { part.Flush(true); }
                else if (job.SupportsRanges && job.TotalBytes >= 1024 * 1024)
                {
                    int count = Math.Clamp(job.Connections, 1, 8);
                    long size = job.TotalBytes.Value / count;
                    for (int i = 0; i < count; i++) job.Segments.Add(new Segment { Start = i * size, End = i == count - 1 ? job.TotalBytes.Value - 1 : (i + 1) * size - 1 });
                }
                else job.Segments.Add(new Segment { Start = 0, End = job.TotalBytes.HasValue ? job.TotalBytes.Value - 1 : long.MaxValue - 1 });
            }
            job.Status = DownloadStatus.Downloading;
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var transfers = job.Segments.Select(async s =>
            {
                try { await TransferWithRetryAsync(job, s, linked.Token); }
                catch { await linked.CancelAsync(); throw; }
            }).ToArray();
            await Task.WhenAll(transfers);
            cancellationToken.ThrowIfCancellationRequested();
            if (job.TotalBytes.HasValue && job.Downloaded != job.TotalBytes) throw new IOException("File size did not match the server response.");
            await using (var input = new FileStream(job.PartialPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                job.Sha256 = Convert.ToHexString(await SHA256.HashDataAsync(input, cancellationToken)).ToLowerInvariant();
            if (checkpoint != null) await checkpoint();
            File.Move(job.PartialPath, job.Destination, false);
            job.Status = DownloadStatus.Completed;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { job.Status = DownloadStatus.Paused; }
        catch (Exception ex)
        {
            job.Status = DownloadStatus.Failed;
            // Do not leak signed URLs, cookies or authorization headers into logs/UI.
            job.Error = ex is SourceChangedException or IOException ? ex.Message : ex is HttpRequestException http && http.StatusCode.HasValue
                ? $"Server returned HTTP {(int)http.StatusCode}. The link may have expired or need authentication."
                : "Transfer failed. Check your connection and retry; an expired link may need replacing.";
        }
        finally { job.BytesPerSecond = 0; }
    }
    private async Task TransferWithRetryAsync(DownloadJob job, Segment segment, CancellationToken ct)
    {
        for (int attempt = 0; ; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            if (job.TotalBytes.HasValue && segment.Committed >= segment.Length) return;
            try { await TransferAsync(job, segment, ct); return; }
            catch (Exception ex) when (attempt < 3 && !ct.IsCancellationRequested && ex is not SourceChangedException &&
                (ex is HttpRequestException { StatusCode: null } || ex is EndOfStreamException || ex is OperationCanceledException))
            {
                if (!job.SupportsRanges) Interlocked.Exchange(ref segment.Committed, 0);
                await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)), ct);
            }
        }
    }
    private async Task TransferAsync(DownloadJob job, Segment segment, CancellationToken ct)
    {
        bool range = job.SupportsRanges;
        long offset = range ? segment.Start + segment.Committed : 0;
        if (!range) Interlocked.Exchange(ref segment.Committed, 0);
        using var response = await SendAsync(job, range ? offset : null, range ? segment.End : null, range, ct);
        response.EnsureSuccessStatusCode(); CheckEncoding(response);
        if (range)
        {
            var cr = response.Content.Headers.ContentRange;
            if (response.StatusCode != HttpStatusCode.PartialContent || cr?.From != offset || cr.To != segment.End || cr.Length != job.TotalBytes || cr.Unit != "bytes" || ValidatorOf(response) != job.Validator)
                throw new SourceChangedException("The source changed or returned an invalid range. Restart this download.");
        }
        else if (response.StatusCode != HttpStatusCode.OK || response.Content.Headers.ContentLength != job.TotalBytes || (job.Validator != null && ValidatorOf(response) != job.Validator))
            throw new SourceChangedException("The source changed between requests. Restart this download.");
        await using var input = await response.Content.ReadAsStreamAsync(ct);
        using var output = new FileStream(job.PartialPath, FileMode.Open, FileAccess.Write, FileShare.ReadWrite, 128 * 1024);
        if (!range && !job.TotalBytes.HasValue) output.SetLength(0);
        output.Position = offset;
        byte[] buffer = new byte[128 * 1024];
        long remaining = job.TotalBytes.HasValue ? segment.Length - segment.Committed : long.MaxValue;
        while (remaining > 0)
        {
            using var readTimeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            readTimeout.CancelAfter(TimeSpan.FromSeconds(45));
            int count = await input.ReadAsync(buffer.AsMemory(0, (int)Math.Min(buffer.Length, remaining)), readTimeout.Token);
            if (count == 0)
            {
                if (job.TotalBytes.HasValue) throw new EndOfStreamException("Connection ended before the complete file arrived.");
                break;
            }
            await Limiter.WaitAsync(count, ct);
            await output.WriteAsync(buffer.AsMemory(0, count), ct);
            // Persisted segment offsets must never describe bytes still only in a stream buffer.
            output.Flush(true);
            Interlocked.Add(ref segment.Committed, count);
            remaining -= count;
        }
        if (job.TotalBytes.HasValue)
        {
            using var readTimeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            readTimeout.CancelAfter(TimeSpan.FromSeconds(45));
            if (await input.ReadAsync(buffer.AsMemory(0, 1), readTimeout.Token) != 0) throw new IOException("Server sent more bytes than requested.");
        }
        else { job.TotalBytes = segment.Committed; segment.End = segment.Committed - 1; }
    }
    public static void Restart(DownloadJob job)
    {
        if (job.Status is DownloadStatus.Downloading or DownloadStatus.Connecting) throw new InvalidOperationException("Pause the download first.");
        if (File.Exists(job.PartialPath)) File.Delete(job.PartialPath);
        job.Segments = []; job.TotalBytes = null; job.Validator = null; job.Error = null; job.Sha256 = null; job.Status = DownloadStatus.Queued;
    }
    public void Dispose() => client.Dispose();
}
