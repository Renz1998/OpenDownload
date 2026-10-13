using System.Windows;
using System.Windows.Media;
namespace OpenDownload.Desktop;
public static class Theme
{
 public static void Apply(bool light)
 {
  string[] keys=["Canvas","Surface","Sidebar","Ink","Muted","Line","Accent","AccentInk","Selected","Hover","Track","Warning"];
  string[] values=light?["#F8FAF9","#FFFFFF","#F1F5F3","#172B25","#576B62","#DCE5DF","#116D57","#FFFFFF","#E1F1E9","#EDF4F0","#E6ECE8","#914811"]:["#131817","#1A201E","#171D1B","#E8EEEB","#A9B9B0","#323E37","#8ADBBD","#102A20","#243E30","#27312C","#35423A","#EAB58B"];
  for(int i=0;i<keys.Length;i++)Application.Current.Resources[keys[i]]=new SolidColorBrush((Color)ColorConverter.ConvertFromString(values[i]));
 }
}
