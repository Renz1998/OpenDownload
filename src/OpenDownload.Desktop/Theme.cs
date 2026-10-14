using System.Windows;
using System.Windows.Media;
using System.Runtime.InteropServices;
using System.Windows.Interop;
namespace OpenDownload.Desktop;
public static class Theme
{
 private static bool initialized, lightTheme;
 [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
 internal static int LastTitleBarResult { get; private set; }
 internal static bool TitleBarMatches(Window window)
 {
  ApplyTitleBar(window); return LastTitleBarResult==0;
 }
 private static int ColorRef(Color color) => color.R | (color.G << 8) | (color.B << 16);
 public static void ApplyTitleBar(Window window)
 {
  var handle = new WindowInteropHelper(window).Handle; if(handle == IntPtr.Zero) return;
  int dark = lightTheme ? 0 : 1; DwmSetWindowAttribute(handle,20,ref dark,4);
  int caption=ColorRef(((SolidColorBrush)Application.Current.FindResource("Surface")).Color);
  int text=ColorRef(((SolidColorBrush)Application.Current.FindResource("Ink")).Color);
  LastTitleBarResult=DwmSetWindowAttribute(handle,35,ref caption,4); DwmSetWindowAttribute(handle,36,ref text,4);
 }
 public static void Apply(bool light)
 {
  lightTheme=light;
  if(!initialized) { EventManager.RegisterClassHandler(typeof(Window),FrameworkElement.LoadedEvent,new RoutedEventHandler((sender,_)=>ApplyTitleBar((Window)sender))); initialized=true; }
  string[] keys=["Canvas","Surface","Sidebar","Ink","Muted","Line","Accent","AccentInk","Selected","Hover","Track","Warning"];
  string[] values=light?["#F8FAF9","#FFFFFF","#F1F5F3","#172B25","#576B62","#DCE5DF","#116D57","#FFFFFF","#E1F1E9","#EDF4F0","#E6ECE8","#914811"]:["#131817","#1A201E","#171D1B","#E8EEEB","#A9B9B0","#323E37","#8ADBBD","#102A20","#243E30","#27312C","#35423A","#EAB58B"];
  for(int i=0;i<keys.Length;i++)Application.Current.Resources[keys[i]]=new SolidColorBrush((Color)ColorConverter.ConvertFromString(values[i]));
  foreach(Window window in Application.Current.Windows) ApplyTitleBar(window);
 }
}
