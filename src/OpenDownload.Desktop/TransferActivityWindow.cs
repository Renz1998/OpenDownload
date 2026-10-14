using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Threading;
namespace OpenDownload.Desktop;

public sealed class TransferActivityWindow : Window
{
    internal ObservableCollection<DownloadRow> Transfers { get; }=[];
    public TransferActivityWindow(Action<DownloadRow> pause,Action<DownloadRow> resume)
    {
        Style=(Style)Application.Current.FindResource(typeof(Window)); Title="Download activity"; Width=730; Height=520; MinWidth=550; MinHeight=300; WindowStartupLocation=WindowStartupLocation.CenterOwner;
        var panel=new DockPanel { Margin=new Thickness(20) }; Content=panel;
        var heading=new TextBlock { Text="Your download activity",FontSize=23,Margin=new Thickness(0,0,0,12) }; DockPanel.SetDock(heading,Dock.Top); panel.Children.Add(heading);
        var summary=new TextBlock { Margin=new Thickness(0,0,0,10) }; DockPanel.SetDock(summary,Dock.Top); panel.Children.Add(summary);
        var hint=new TextBlock { Text="Closing this window keeps transfers running. Each row has its own controls.",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,10,0,0) }; DockPanel.SetDock(hint,Dock.Bottom); panel.Children.Add(hint);
        var list=new ListBox { ItemsSource=Transfers };
        list.ItemTemplate=(DataTemplate)XamlReader.Parse("""
        <DataTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"><Border BorderBrush="{DynamicResource Line}" BorderThickness="1" Padding="12" Margin="0,4" CornerRadius="8"><StackPanel>
        <TextBlock Text="{Binding FileName}" FontWeight="SemiBold" TextTrimming="CharacterEllipsis"/>
        <ProgressBar Value="{Binding Progress,Mode=OneWay}" Maximum="100" Height="6" Margin="0,12,0,8"/>
        <TextBlock Text="{Binding TransferSummary}"/><TextBlock Text="{Binding Status}" Margin="0,5,0,8"/>
        <WrapPanel><Button Content="Pause" Tag="{Binding}" IsEnabled="{Binding CanPause}"/><Button Content="Resume" Tag="{Binding}" IsEnabled="{Binding CanResume}"/><Button Content="Open file" Tag="{Binding}" IsEnabled="{Binding IsCompleted}"/><Button Content="Open folder" Tag="{Binding}"/></WrapPanel>
        </StackPanel></Border></DataTemplate>
        """);
        list.AddHandler(Button.ClickEvent,new RoutedEventHandler((_,e)=>
        {
            if(e.OriginalSource is not Button button || button.Tag is not DownloadRow row) return;
            switch(button.Content) { case "Pause":pause(row);break; case "Resume":resume(row);break; case "Open file":MainWindow.OpenDownloadedFile(row);break; case "Open folder":MainWindow.OpenContainingFolder(row);break; }
        })); panel.Children.Add(list);
        var timer=new DispatcherTimer { Interval=TimeSpan.FromSeconds(1) };
        timer.Tick+=(_,_)=> { foreach(var row in Transfers) row.Refresh(); summary.Text=$"{Transfers.Count(r=>r.CanPause)} active / {Transfers.Count(r=>r.IsCompleted)} completed / {Transfers.Count} transfers"; };
        Closed+=(_,_)=>timer.Stop(); timer.Start();
    }
    public void Add(DownloadRow row) { if(!Transfers.Any(r=>r.Job.Id==row.Job.Id)) Transfers.Add(row); }
}
