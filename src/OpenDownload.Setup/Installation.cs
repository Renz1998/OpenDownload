using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Text.Json;
using Microsoft.Win32;
using OpenDownload.Core;
namespace OpenDownload.Setup;
public static class Installation
{
    private const string UninstallKey = "Software\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\OpenDownload";
    private static readonly string[] Browsers = ["Google\\Chrome", "Microsoft\\Edge", "Chromium"];
    public static string DefaultDestination()
    {
        using var key = Registry.CurrentUser.OpenSubKey(UninstallKey);
        if (key?.GetValue("InstallLocation") is string current && File.Exists(Path.Combine(current, "OpenDownload.Desktop.exe"))) return current;
        string link = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "OpenDownload.lnk");
        try
        {
            if (File.Exists(link))
            {
                dynamic shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")!)!;
                dynamic shortcut = shell.CreateShortcut(link); string target = shortcut.TargetPath;
                if (File.Exists(target) && FileVersionInfo.GetVersionInfo(target).ProductName == "OpenDownload") return Path.GetDirectoryName(target)!;
            }
        }
        catch { }
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "OpenDownload");
    }
    public static string ValidateDestination(string destination)
    {
        if (!Path.IsPathFullyQualified(destination)) throw new IOException("Choose a full local installation path.");
        string full = Path.GetFullPath(destination).TrimEnd(Path.DirectorySeparatorChar);
        if (full.Length < 8 || full.Equals(Path.GetPathRoot(full)?.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase) || full.StartsWith("\\\\")) throw new IOException("Choose a dedicated local application folder, not a drive root or network share.");
        CheckNoLinks(full);
        if (Directory.Exists(full) && Directory.EnumerateFileSystemEntries(full).Any())
        {
            string marker = Path.Combine(full, "OpenDownload.Desktop.exe");
            if (!File.Exists(marker) || FileVersionInfo.GetVersionInfo(marker).ProductName != "OpenDownload" || !File.Exists(Path.Combine(full, "OpenDownload.NativeHost.exe")) || !File.Exists(Path.Combine(full, "OpenDownload.Desktop.runtimeconfig.json"))) throw new IOException("This folder contains other files. Choose an empty folder or your existing OpenDownload installation.");
        }
        return full;
    }
    public static string SafeChild(string root, string relative)
    {
        string prefix = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        string child = Path.GetFullPath(Path.Combine(prefix, relative.Replace('/', Path.DirectorySeparatorChar)));
        if (Path.IsPathRooted(relative) || !child.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) throw new IOException("The package contains an unsafe file path.");
        return child;
    }
    private static void CheckNoLinks(string path)
    {
        for (var dir = new DirectoryInfo(path); dir != null; dir = dir.Parent)
            if (dir.Exists && dir.Attributes.HasFlag(FileAttributes.ReparsePoint)) throw new IOException("Choose an installation folder without junctions or symbolic links.");
    }
    public static void Install(string destination, bool desktopShortcut, bool testMode)
    {
        string target = ValidateDestination(destination), parent = Path.GetDirectoryName(target)!;
        Directory.CreateDirectory(parent);
        string stage = target + ".staging-" + Guid.NewGuid().ToString("N");
        string backup = target + ".backup-" + Guid.NewGuid().ToString("N");
        Directory.CreateDirectory(stage); bool movedOld = false, movedNew = false;
        var oldValues = new Dictionary<string, string?>();
        try
        {
            using var payload = Assembly.GetExecutingAssembly().GetManifestResourceStream("OpenDownload.Package") ?? throw new IOException("The installer package is missing.");
            using var zip = new ZipArchive(payload, ZipArchiveMode.Read);
            var files = new List<string>();
            foreach (var entry in zip.Entries)
            {
                if (entry.FullName.EndsWith('/')) continue;
                string path = SafeChild(stage, entry.FullName); Directory.CreateDirectory(Path.GetDirectoryName(path)!); entry.ExtractToFile(path, false); files.Add(entry.FullName);
            }
            if (!File.Exists(Path.Combine(stage, "OpenDownload.Desktop.exe")) || !File.Exists(Path.Combine(stage, "OpenDownload.NativeHost.exe"))) throw new IOException("The package is incomplete.");
            string dataPointer = Path.Combine(target, "data-path.txt");
            if (File.Exists(dataPointer)) { File.Copy(dataPointer, Path.Combine(stage, "data-path.txt"), true); files.Add("data-path.txt"); }
            if (Directory.Exists(target))
            {
                var owned = files.Select(f => Path.GetRelativePath(stage, SafeChild(stage, f))).ToHashSet(StringComparer.OrdinalIgnoreCase);
                var pending = new Stack<string>(); pending.Push(target);
                while (pending.Count > 0)
                {
                    string directory = pending.Pop(); CheckNoLinks(directory);
                    foreach (string sub in Directory.EnumerateDirectories(directory)) { CheckNoLinks(sub); pending.Push(sub); }
                    foreach (string old in Directory.EnumerateFiles(directory))
                    {
                        string relative = Path.GetRelativePath(target, old);
                        if (owned.Contains(relative) || relative is "native-host.json" or "installed-files.json") continue;
                        if (File.GetAttributes(old).HasFlag(FileAttributes.ReparsePoint)) throw new IOException("The installation contains a linked file; move it outside the program folder before upgrading.");
                        string copy = SafeChild(stage, relative); Directory.CreateDirectory(Path.GetDirectoryName(copy)!); File.Copy(old, copy, false);
                    }
                }
            }
            // Probe every old packaged file before changing the installation. This catches a running app.
            foreach (string file in files.Select(f => SafeChild(target, f)).Where(File.Exists))
                using (var probe = new FileStream(file, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
            string hostManifest = Path.Combine(stage, "native-host.json");
            File.WriteAllText(hostManifest, JsonSerializer.Serialize(new { name = "org.opendownload.app", description = "OpenDownload browser integration", path = Path.Combine(target, "OpenDownload.NativeHost.exe"), type = "stdio", allowed_origins = new[] { $"chrome-extension://{AppPaths.ExtensionId}/" } }));
            files.Add("native-host.json"); files.Add("installed-files.json");
            File.WriteAllText(Path.Combine(stage, "installed-files.json"), JsonSerializer.Serialize(files.Distinct(StringComparer.OrdinalIgnoreCase).ToArray()));
            if (Directory.Exists(target)) { Directory.Move(target, backup); movedOld = true; }
            Directory.Move(stage, target); movedNew = true;
            if (!testMode)
            {
                string cache = Path.Combine(AppPaths.DataFolder(target), "installer"); Directory.CreateDirectory(cache);
                string cachedSetup = Path.Combine(cache, "OpenDownload.Setup-0.2.2.exe");
                string currentExe = Environment.ProcessPath ?? throw new IOException("Cannot locate this installer.");
                if (!Path.GetFullPath(currentExe).Equals(Path.GetFullPath(cachedSetup), StringComparison.OrdinalIgnoreCase)) File.Copy(currentExe, cachedSetup, true);
                foreach (var browser in Browsers)
                {
                    string keyPath = $"Software\\{browser}\\NativeMessagingHosts\\org.opendownload.app";
                    using var previous = Registry.CurrentUser.OpenSubKey(keyPath); oldValues[keyPath] = previous?.GetValue("") as string;
                    using var key = Registry.CurrentUser.CreateSubKey(keyPath); key.SetValue("", Path.Combine(target, "native-host.json"));
                }
                using (var key = Registry.CurrentUser.CreateSubKey(UninstallKey))
                {
                    key.SetValue("DisplayName", "OpenDownload"); key.SetValue("DisplayVersion", "0.2.2"); key.SetValue("Publisher", "OpenDownload contributors"); key.SetValue("InstallLocation", target);
                    key.SetValue("UninstallString", $"\"{cachedSetup}\" --uninstall \"{target}\""); key.SetValue("DisplayIcon", Path.Combine(target, "OpenDownload.Desktop.exe")); key.SetValue("NoModify", 1); key.SetValue("NoRepair", 1);
                }
                CreateShortcut(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "OpenDownload", "OpenDownload.lnk"), target);
                if (desktopShortcut) CreateShortcut(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "OpenDownload.lnk"), target);
            }
            // The previous application folder is deliberately preserved as a rollback backup.
        }
        catch (Exception ex)
        {
            foreach (var item in oldValues)
            { using var key = Registry.CurrentUser.CreateSubKey(item.Key); if (item.Value == null) key.DeleteValue("", false); else key.SetValue("", item.Value); }
            if (movedNew && Directory.Exists(target)) Directory.Move(target, target + ".failed-" + Guid.NewGuid().ToString("N"));
            if (movedOld && Directory.Exists(backup) && !Directory.Exists(target)) Directory.Move(backup, target);
            throw new IOException(ex is IOException ? "Installation could not finish. Close OpenDownload using Exit (including its tray icon), check free space and choose a writable folder. " + ex.Message : "Installation failed; the previous version was restored.", ex);
        }
    }
    private static void CreateShortcut(string link, string target)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(link)!);
        dynamic shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")!)!;
        dynamic shortcut = shell.CreateShortcut(link); shortcut.TargetPath = Path.Combine(target, "OpenDownload.Desktop.exe"); shortcut.IconLocation = Path.Combine(target, "OpenDownload.Desktop.exe") + ",0"; shortcut.WorkingDirectory = target; shortcut.Description = "OpenDownload — Workbench or Focus"; shortcut.Save();
    }
    public static void Remove(string destination, bool testMode)
    {
        string target = ValidateDestination(destination);
        string manifest = Path.Combine(target, "installed-files.json");
        if (!File.Exists(manifest)) throw new IOException("The install manifest is missing. Reinstall before uninstalling; unrelated files will not be deleted.");
        var entries = JsonSerializer.Deserialize<string[]>(File.ReadAllText(manifest)) ?? throw new IOException("Invalid install manifest.");
        using var payload = Assembly.GetExecutingAssembly().GetManifestResourceStream("OpenDownload.Package") ?? throw new IOException("The uninstaller package is missing.");
        using var original = new ZipArchive(payload, ZipArchiveMode.Read);
        var allowed = original.Entries.Where(e => !e.FullName.EndsWith('/')).Select(e => SafeChild(target, e.FullName)).Concat(new[] { "native-host.json", "installed-files.json", "data-path.txt" }.Select(e => SafeChild(target, e))).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var files = entries.Select(entry => SafeChild(target, entry)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (files.Any(file => !allowed.Contains(file))) throw new IOException("The install manifest lists files outside this package. Uninstall stopped to preserve unrelated files.");
        foreach (var file in files.Where(File.Exists)) { CheckNoLinks(Path.GetDirectoryName(file)!); using var probe = new FileStream(file, FileMode.Open, FileAccess.ReadWrite, FileShare.None); }
        if (!testMode)
        {
            foreach (var browser in Browsers)
            {
                string path = $"Software\\{browser}\\NativeMessagingHosts\\org.opendownload.app";
                using var key = Registry.CurrentUser.OpenSubKey(path); string? registered = key?.GetValue("") as string;
                bool ours = registered?.Equals(Path.Combine(target, "native-host.json"), StringComparison.OrdinalIgnoreCase) == true;
                if (!ours && registered != null && File.Exists(registered))
                {
                    try { using var json = JsonDocument.Parse(File.ReadAllText(registered)); ours = json.RootElement.GetProperty("path").GetString()?.Equals(Path.Combine(target, "OpenDownload.NativeHost.exe"), StringComparison.OrdinalIgnoreCase) == true; } catch { }
                }
                if (ours) Registry.CurrentUser.DeleteSubKeyTree(path, false);
            }
            using (var key = Registry.CurrentUser.OpenSubKey(UninstallKey))
                if (key?.GetValue("InstallLocation") is string value && value.Equals(target, StringComparison.OrdinalIgnoreCase)) Registry.CurrentUser.DeleteSubKeyTree(UninstallKey, false);
            foreach (string link in new[] { Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "OpenDownload", "OpenDownload.lnk"), Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "OpenDownload.lnk") })
            {
                if (!File.Exists(link)) continue;
                dynamic shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")!)!; dynamic shortcut = shell.CreateShortcut(link);
                if (string.Equals((string)shortcut.TargetPath, Path.Combine(target, "OpenDownload.Desktop.exe"), StringComparison.OrdinalIgnoreCase)) File.Delete(link);
            }
        }
        foreach (string file in files.Where(File.Exists)) File.Delete(file);
        foreach (string dir in files.Select(Path.GetDirectoryName).OfType<string>().Distinct().OrderByDescending(d => d.Length))
            if (Directory.Exists(dir) && !Directory.EnumerateFileSystemEntries(dir).Any()) Directory.Delete(dir);
        if (Directory.Exists(target) && !Directory.EnumerateFileSystemEntries(target).Any()) Directory.Delete(target);
    }
}
