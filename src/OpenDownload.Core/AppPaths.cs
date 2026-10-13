using System.Security.Cryptography;
using System.Text;
namespace OpenDownload.Core;
public static class AppPaths
{
    public const string ExtensionId = "oagdmhfnjjcidibdhafcbjmfmaeaojam";
    public static string DataFolder(string appDirectory)
    {
        string file = Path.Combine(appDirectory, "data-path.txt");
        return Path.GetFullPath(Environment.GetEnvironmentVariable("OPENDOWNLOAD_DATA") ?? (File.Exists(file) ? File.ReadAllText(file).Trim() : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OpenDownload")));
    }
    public static string Pipe(string dataDirectory)
    {
        string suffix = Environment.GetEnvironmentVariable("OPENDOWNLOAD_TEST_MODE") == "1" ? "-test-" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(dataDirectory).ToLowerInvariant())))[..12] : "";
        return "OpenDownload-" + Environment.UserName + suffix;
    }
}
