using System.Net.Http;
using System.Text.Json;
namespace OpenDownload.Desktop;

internal static class UpdateService
{
    internal static string? FindVersion(string json,Version current)
    {
        using var document=JsonDocument.Parse(json);
        Version best=current; string? result=null;
        foreach(var release in document.RootElement.EnumerateArray())
        {
            if(release.GetProperty("draft").GetBoolean()) continue;
            string tag=release.GetProperty("tag_name").GetString()??"";
            if(!Version.TryParse(tag.TrimStart('v'),out var version) || version.Build<0 || version<=best) continue;
            string name=$"OpenDownload-{version.ToString(3)}-Setup.exe";
            string expected=$"https://github.com/Renz1998/OpenDownload/releases/download/{tag}/{name}";
            if(!release.GetProperty("assets").EnumerateArray().Any(a=>a.GetProperty("name").GetString()==name && a.GetProperty("browser_download_url").GetString()==expected)) continue;
            best=version; result=version.ToString(3);
        }
        return result;
    }
    internal static async Task<string?> CheckAsync(Version current)
    {
        using var client=new HttpClient { Timeout=TimeSpan.FromSeconds(15) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("OpenDownload/"+current.ToString(3));
        using var response=await client.GetAsync("https://api.github.com/repos/Renz1998/OpenDownload/releases?per_page=30");
        response.EnsureSuccessStatusCode();
        return FindVersion(await response.Content.ReadAsStringAsync(),current);
    }
}
