using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
namespace OpenDownload.Desktop;

public sealed class CompletionNotification : Window
{
    public CompletionNotification(DownloadRow row,Action openFile,Action openFolder)
    {
        Style=(Style)Application.Current.FindResource(typeof(Window)); Title="Download complete"; Width=390; SizeToContent=SizeToContent.Height;
        ShowInTaskbar=false; ShowActivated=false; Topmost=true; ResizeMode=ResizeMode.NoResize; WindowStyle=WindowStyle.ToolWindow;
        var panel=new StackPanel { Margin=new Thickness(18) }; Content=panel;
        panel.Children.Add(new TextBlock { Text="Download complete",FontSize=18,FontWeight=FontWeights.SemiBold });
        panel.Children.Add(new TextBlock { Text=row.FileName,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,8,0,12) });
        var actions=new WrapPanel(); panel.Children.Add(actions);
        var file=new Button { Content="Open file" }; file.Click+=(_,_)=>{ openFile(); Close(); }; actions.Children.Add(file);
        var folder=new Button { Content="Open folder" }; folder.Click+=(_,_)=>{ openFolder(); Close(); }; actions.Children.Add(folder);
        var close=new Button { Content="Dismiss" }; close.Click+=(_,_)=>Close(); actions.Children.Add(close);
        Loaded+=(_,_)=> { Left=SystemParameters.WorkArea.Right-ActualWidth-18; Top=SystemParameters.WorkArea.Bottom-ActualHeight-18; Theme.ApplyTitleBar(this); };
        var timer=new DispatcherTimer { Interval=TimeSpan.FromSeconds(12) }; timer.Tick+=(_,_)=> { if(!IsMouseOver) Close(); }; Closed+=(_,_)=>timer.Stop(); timer.Start();
    }
}
