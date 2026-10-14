using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace OpenDownload.Desktop;

internal static class BrowserNavigation
{
    private delegate bool EnumWindow(IntPtr handle, IntPtr parameter);
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindow callback, IntPtr parameter);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr handle);
    [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr handle);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr handle, out uint process);
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr handle);
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr handle, int command);
    [DllImport("user32.dll", SetLastError=true)] private static extern uint SendInput(uint count, Input[] input, int size);
    [StructLayout(LayoutKind.Sequential)] private struct Input { public uint Type; public InputData Data; }
    [StructLayout(LayoutKind.Explicit)] private struct InputData
    {
        [FieldOffset(0)] public Keyboard Keyboard;
        [FieldOffset(0)] public Mouse Mouse;
    }
    [StructLayout(LayoutKind.Sequential)] private struct Keyboard { public ushort Key, Scan; public uint Flags, Time; public UIntPtr Extra; }
    [StructLayout(LayoutKind.Sequential)] private struct Mouse { public int X,Y; public uint Data,Flags,Time; public UIntPtr Extra; }
    private static Input Key(ushort key, bool up=false) => new() { Type=1,Data=new InputData { Keyboard=new Keyboard { Key=key,Flags=up?2u:0u } } };
    private static IntPtr FindWindow(string executable)
    {
        IntPtr result=IntPtr.Zero;
        string name=Path.GetFileNameWithoutExtension(executable);
        EnumWindows((window,_)=>
        {
            if(!IsWindowVisible(window)) return true;
            GetWindowThreadProcessId(window,out uint pid);
            try { using var process=Process.GetProcessById((int)pid); if(process.ProcessName.Equals(name,StringComparison.OrdinalIgnoreCase)) { result=window; return false; } }
            catch(ArgumentException) { }
            return true;
        },IntPtr.Zero);
        return result;
    }
    private static void Send(IntPtr window, Input[] inputs)
    {
        // Never type into another app if focus changed while the browser was opening.
        if(GetForegroundWindow()!=window) throw new IOException("Browser focus changed. Click the button again, or type the extensions address in your browser.");
        if(SendInput((uint)inputs.Length,inputs,Marshal.SizeOf<Input>())!=inputs.Length) throw new IOException("Windows could not send the browser navigation. Open the extensions page from the address bar.");
    }
    public static async Task OpenPageAsync(string executable,string url)
    {
        var window=FindWindow(executable);
        if(window==IntPtr.Zero)
        {
            Process.Start(new ProcessStartInfo(executable) { UseShellExecute=false,ArgumentList={"about:blank"} });
            for(int i=0;i<50 && window==IntPtr.Zero;i++) { await Task.Delay(100); window=FindWindow(executable); }
        }
        if(window==IntPtr.Zero) throw new IOException("The browser did not open. Open it manually and visit "+url);
        if(IsIconic(window)) ShowWindow(window,9);
        SetForegroundWindow(window); await Task.Delay(120);
        Send(window,[Key(0x11),Key(0x54),Key(0x54,true),Key(0x11,true)]);
        await Task.Delay(200);
        var text=new List<Input>();
        foreach(char character in url)
        {
            text.Add(new Input { Type=1,Data=new InputData { Keyboard=new Keyboard { Scan=character,Flags=4 } } });
            text.Add(new Input { Type=1,Data=new InputData { Keyboard=new Keyboard { Scan=character,Flags=6 } } });
        }
        text.Add(Key(0x0D)); text.Add(Key(0x0D,true)); Send(window,text.ToArray());
    }
}
