using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace OpenDownload.Desktop;
public static class GuidePictures
{
    public static void Create(string directory)
    {
        Directory.CreateDirectory(directory);
        foreach (string browser in new[] { "Chrome", "Edge" })
        {
            var visual = new DrawingVisual(); using (var dc = visual.RenderOpen())
            {
                var bg = new SolidColorBrush(Color.FromRgb(248, 250, 249)); var ink = new SolidColorBrush(Color.FromRgb(23, 43, 37)); var muted = new SolidColorBrush(Color.FromRgb(87, 107, 98)); var accent = new SolidColorBrush(Color.FromRgb(17, 109, 87));
                dc.DrawRoundedRectangle(bg, new Pen(new SolidColorBrush(Color.FromRgb(220, 229, 223)), 1), new Rect(1, 1, 958, 318), 12, 12);
                void Text(string text, double x, double y, double size = 18, Brush? color = null) => dc.DrawText(new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), size, color ?? ink, 1), new Point(x, y));
                void Box(double x, double y, double w, double h, string text) { dc.DrawRoundedRectangle(accent, null, new Rect(x, y, w, h), 7, 7); Text(text, x + 14, y + 10, 16, Brushes.White); }
                Text(browser + "  /  Extensions", 28, 20, 21); Text(browser == "Chrome" ? "chrome://extensions" : "edge://extensions", 28, 60, 16, muted);
                if (browser == "Chrome") { Box(700, 20, 230, 44, "1   Developer mode  ON"); Box(28, 112, 195, 44, "2   Load unpacked"); }
                else { dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(232, 239, 235)), null, new Rect(15, 100, 230, 190)); Text("My extensions", 30, 120); Box(30, 227, 200, 44, "1  Developer mode"); Box(280, 112, 195, 44, "2   Load unpacked"); }
                Text("3   Choose the browser-extension folder", browser == "Chrome" ? 28 : 280, 190, 20, accent);
                Text("It must contain manifest.json. Then click Select Folder.", browser == "Chrome" ? 28 : 280, 230, 16, muted);
                Text("Illustration — exact placement can vary with browser versions.", 28, 287, 13, muted);
            }
            Save(visual, 960, 320, Path.Combine(directory, browser.ToLowerInvariant() + "-setup.png"));
        }
    }
    public static void Save(Visual visual, int width, int height, string path)
    {
        var image = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32); image.Render(visual);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image)); using var file = File.Create(path); encoder.Save(file);
    }
}
