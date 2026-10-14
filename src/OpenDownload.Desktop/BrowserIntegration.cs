using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Win32;
using OpenDownload.Core;
namespace OpenDownload.Desktop;
public static class BrowserIntegration
{
    public static string ExtensionFolder => Path.Combine(AppContext.BaseDirectory, "browser-extension");
    public static void Register(string? extensionId = null)
    {
        extensionId ??= AppPaths.ExtensionId;
        if (!Regex.IsMatch(extensionId, "^[a-p]{32}$")) throw new ArgumentException("An extension ID must contain 32 letters from a to p.");
        string host = Path.Combine(AppContext.BaseDirectory, "OpenDownload.NativeHost.exe");
        if (!File.Exists(host)) throw new IOException("The browser helper is missing. Reinstall OpenDownload.");
        string manifest = Path.Combine(App.DataDirectory, "native-host.json");
        var origins = new HashSet<string> { $"chrome-extension://{extensionId}/" };
        if (File.Exists(manifest))
        {
            using var old = JsonDocument.Parse(File.ReadAllText(manifest));
            foreach (var origin in old.RootElement.GetProperty("allowed_origins").EnumerateArray())
                if (Regex.IsMatch(origin.GetString() ?? "", "^chrome-extension://[a-p]{32}/$")) origins.Add(origin.GetString()!);
        }
        File.WriteAllText(manifest, JsonSerializer.Serialize(new { name = "org.opendownload.app", description = "OpenDownload browser integration", path = host, type = "stdio", allowed_origins = origins.ToArray() }));
        foreach (string browser in new[] { "Google\\Chrome", "Microsoft\\Edge", "Chromium" })
        { using var key = Registry.CurrentUser.CreateSubKey($"Software\\{browser}\\NativeMessagingHosts\\org.opendownload.app"); key.SetValue("", manifest); }
    }
    public static void OpenBrowser(string browser, bool check = false)
    {
        string exe = browser == "Edge" ? "msedge.exe" : "chrome.exe";
        string? path = null;
        // Reuse the running browser executable, including nonstandard installations.
        foreach (var process in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(exe)))
        {
            using (process)
            {
                try { if (process.MainModule?.FileName is string runningPath && File.Exists(runningPath)) { path = runningPath; break; } }
                catch (System.ComponentModel.Win32Exception) { }
                catch (InvalidOperationException) { }
            }
        }
        foreach (var hive in new[] { Registry.CurrentUser, Registry.LocalMachine })
        { if (path != null) break; using var key = hive.OpenSubKey($"Software\\Microsoft\\Windows\\CurrentVersion\\App Paths\\{exe}"); if (key?.GetValue("") is string value && File.Exists(value.Trim('"'))) { path = value.Trim('"'); break; } }
        if (path == null)
        {
            var candidates = browser == "Edge" ? new[] { Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Microsoft", "Edge", "Application", exe) }
                : new[] { Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Google", "Chrome", "Application", exe), Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Google", "Chrome", "Application", exe) };
            path = candidates.FirstOrDefault(File.Exists);
        }
        if (path == null) throw new IOException($"{browser} was not found. Open it manually and visit " + (browser == "Edge" ? "edge://extensions" : "chrome://extensions") + ".");
        Process.Start(BrowserStartInfo(path, browser, check));
    }
    internal static ProcessStartInfo BrowserStartInfo(string executable, string browser, bool check)
    {
        // Execute Chrome directly so its URL is passed intact; an existing browser opens it in a new tab.
        var info = new ProcessStartInfo(executable) { UseShellExecute = false };
        info.ArgumentList.Add(check ? $"chrome-extension://{AppPaths.ExtensionId}/popup.html?setup=1" : browser == "Edge" ? "edge://extensions/" : "chrome://extensions/");
        return info;
    }
}
