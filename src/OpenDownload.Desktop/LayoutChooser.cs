using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace OpenDownload.Desktop;
public sealed class LayoutChooser : Window
{
    public string Choice { get; private set; } = "Workbench";
    public LayoutChooser(string? current = null)
    {
        Style = (Style)Application.Current.FindResource(typeof(Window));
        Title = "Choose your OpenDownload layout"; Width = 790; SizeToContent = SizeToContent.Height; ResizeMode = ResizeMode.NoResize; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var panel = new StackPanel { Margin = new Thickness(28) }; Content = panel;
        panel.Children.Add(new TextBlock { Text = "Make OpenDownload yours", FontSize = 26, FontWeight = FontWeights.SemiBold });
        panel.Children.Add(new TextBlock { Text = "Choose a workspace. Switch anytime using the layout button at the top.", Foreground = (Brush)FindResource("Muted"), Margin = new Thickness(0, 10, 0, 24), TextWrapping = TextWrapping.Wrap });
        var choices = new Grid(); choices.ColumnDefinitions.Add(new()); choices.ColumnDefinitions.Add(new()); panel.Children.Add(choices);
        foreach (var (name, column, description) in new[] { ("Workbench", 0, "A compact file list for managing many downloads."), ("Focus", 1, "Spacious transfer cards with details alongside.") })
        {
            var stack = new StackPanel();
            string file = Path.Combine(AppContext.BaseDirectory, "docs", "images", name.ToLowerInvariant() + ".png");
            if (File.Exists(file)) stack.Children.Add(new Image { Source = new BitmapImage(new Uri(file)), Height = 205, Stretch = Stretch.Uniform, Margin = new Thickness(0, 0, 0, 16) });
            else stack.Children.Add(MiniPreview(name));
            stack.Children.Add(new TextBlock { Text = name, FontSize = 21, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 8) });
            stack.Children.Add(new TextBlock { Text = description, TextWrapping = TextWrapping.Wrap, Foreground = (Brush)FindResource("Muted"), MinHeight = 55 });
            var button = new Button { Content = current == name ? "Keep " + name : "Use " + name, Style = (Style)FindResource("PrimaryButton"), Margin = new Thickness(0, 16, 0, 0) };
            button.Click += (_, _) => { Choice = name; DialogResult = true; }; stack.Children.Add(button);
            var border = new Border { Child = stack, Padding = new Thickness(18), CornerRadius = new CornerRadius(12), BorderBrush = (Brush)FindResource("Line"), BorderThickness = new Thickness(1), Background = (Brush)FindResource("Surface"), Margin = new Thickness(column == 0 ? 0 : 8, 0, column == 0 ? 8 : 0, 0) };
            Grid.SetColumn(border, column); choices.Children.Add(border);
        }
        panel.Children.Add(new TextBlock { Text = "Both layouts have the same downloads, controls and settings.", Foreground = (Brush)FindResource("Muted"), Margin = new Thickness(0, 22, 0, 0) });
    }
    private UIElement MiniPreview(string layout)
    {
        var canvas = new Grid { Height = 140, Margin = new Thickness(0, 0, 0, 18), Background = (Brush)FindResource("Canvas") };
        var rows = new StackPanel { Margin = new Thickness(layout == "Workbench" ? 55 : 12, 12, layout == "Focus" ? 75 : 12, 12) };
        foreach (var text in new[] { "model.safetensors", "game-update.7z", "dataset.zip" }) rows.Children.Add(new Border { Height = layout == "Focus" ? 34 : 27, Margin = new Thickness(0, 3, 0, 3), Background = (Brush)FindResource("Selected"), CornerRadius = new CornerRadius(4), Child = new TextBlock { Text = text, FontSize = 10, Margin = new Thickness(8, 5, 0, 0) } });
        canvas.Children.Add(rows); return canvas;
    }
}
