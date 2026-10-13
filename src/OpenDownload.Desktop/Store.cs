using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using OpenDownload.Core;

namespace OpenDownload.Desktop;

public sealed class Store : IDisposable
{
    private readonly SqliteConnection db;
    private static readonly byte[] entropy = Encoding.UTF8.GetBytes("OpenDownload-v1");
    public Store(string folder)
    {
        Directory.CreateDirectory(folder);
        db = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = Path.Combine(folder, "downloads.db"), Pooling = false }.ToString());
        db.Open();
        using var cmd = db.CreateCommand();
        cmd.CommandText = "PRAGMA journal_mode=WAL; PRAGMA synchronous=FULL; CREATE TABLE IF NOT EXISTS jobs (id TEXT PRIMARY KEY, payload BLOB NOT NULL); CREATE TABLE IF NOT EXISTS settings (id INTEGER PRIMARY KEY, payload TEXT NOT NULL);";
        cmd.ExecuteNonQuery();
    }
    private sealed record StoredJob(DownloadJob Job, Dictionary<string, string> Headers);
    public List<DownloadJob> Load()
    {
        using var cmd = db.CreateCommand(); cmd.CommandText = "SELECT payload FROM jobs ORDER BY rowid";
        using var reader = cmd.ExecuteReader(); var list = new List<DownloadJob>();
        while (reader.Read())
        {
            var json = ProtectedData.Unprotect((byte[])reader[0], entropy, DataProtectionScope.CurrentUser);
            var stored = JsonSerializer.Deserialize<StoredJob>(json) ?? throw new IOException("Invalid download record.");
            stored.Job.Headers = stored.Headers;
            if (stored.Job.Status is DownloadStatus.Downloading or DownloadStatus.Connecting) stored.Job.Status = DownloadStatus.Paused;
            // Recover a crash between the final rename and saving the completed state.
            if (stored.Job.Status != DownloadStatus.Completed && stored.Job.Sha256 != null && File.Exists(stored.Job.Destination) && !File.Exists(stored.Job.PartialPath))
            {
                using var file = File.OpenRead(stored.Job.Destination);
                if (Convert.ToHexString(SHA256.HashData(file)).Equals(stored.Job.Sha256, StringComparison.OrdinalIgnoreCase)) stored.Job.Status = DownloadStatus.Completed;
            }
            list.Add(stored.Job);
        }
        return list;
    }
    public void Save(IEnumerable<DownloadJob> jobs, Settings settings)
    {
        using var transaction = db.BeginTransaction();
        foreach (var job in jobs)
        {
            using var cmd = db.CreateCommand(); cmd.Transaction = transaction;
            cmd.CommandText = "INSERT INTO jobs(id,payload) VALUES($id,$data) ON CONFLICT(id) DO UPDATE SET payload=excluded.payload";
            cmd.Parameters.AddWithValue("$id", job.Id);
            cmd.Parameters.AddWithValue("$data", ProtectedData.Protect(JsonSerializer.SerializeToUtf8Bytes(new StoredJob(job, job.Headers)), entropy, DataProtectionScope.CurrentUser));
            cmd.ExecuteNonQuery();
        }
        using (var cmd = db.CreateCommand())
        {
            cmd.Transaction = transaction; cmd.CommandText = "INSERT INTO settings(id,payload) VALUES(1,$data) ON CONFLICT(id) DO UPDATE SET payload=excluded.payload";
            cmd.Parameters.AddWithValue("$data", JsonSerializer.Serialize(settings)); cmd.ExecuteNonQuery();
        }
        transaction.Commit();
    }
    public Settings LoadSettings()
    {
        using var cmd = db.CreateCommand(); cmd.CommandText = "SELECT payload FROM settings WHERE id=1";
        return cmd.ExecuteScalar() is string json ? JsonSerializer.Deserialize<Settings>(json) ?? new() : new();
    }
    public void Remove(string id)
    {
        using var cmd = db.CreateCommand(); cmd.CommandText = "DELETE FROM jobs WHERE id=$id"; cmd.Parameters.AddWithValue("$id", id); cmd.ExecuteNonQuery();
    }
    public void Dispose() => db.Dispose();
}
