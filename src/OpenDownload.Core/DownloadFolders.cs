using System.Runtime.InteropServices;
namespace OpenDownload.Core;

public static class DownloadFolders
{
    [DllImport("shell32.dll")] private static extern int SHGetKnownFolderPath(ref Guid folder,uint flags,IntPtr token,out IntPtr path);
    public static string Default
    {
        get
        {
            if(OperatingSystem.IsWindows())
            {
                var id=new Guid("374DE290-123F-4565-9164-39C4925E467B");
                if(SHGetKnownFolderPath(ref id,0,IntPtr.Zero,out var pointer)==0)
                    try { return Marshal.PtrToStringUni(pointer)!; } finally { Marshal.FreeCoTaskMem(pointer); }
            }
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"Downloads");
        }
    }
    public static string TypeKey(string name) => Path.GetExtension(DownloadNames.Sanitize(name)).ToLowerInvariant();
    public static string ForType(Settings settings,string name)
    {
        string type=TypeKey(name);
        return type.Length>0 && settings.TypeFolders.TryGetValue(type,out string? folder) ? folder : settings.DownloadDirectory;
    }
    public static void Remember(Settings settings,string name,string folder)
    {
        string type=TypeKey(name);
        if(type.Length==0) throw new ArgumentException("A file extension is needed to remember a destination.");
        settings.TypeFolders[type]=Path.GetFullPath(folder);
    }
}
