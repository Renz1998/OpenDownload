using System.IO;
using System.Windows;
using System.Windows.Controls;
using OpenDownload.Core;
namespace OpenDownload.Desktop;

public partial class MainWindow
{
    private void RecoveryClick(object sender,RoutedEventArgs e)
    {
        if(CurrentRow is not DownloadRow row || row.Job.Status!=DownloadStatus.Failed) { ShowNotice("Choose a failed download to see its recovery options."); return; }
        var advice=DownloadRecovery.Explain(row.Job);
        var dialog=new Window { Owner=this }; var panel=DialogUi.Setup(dialog,"Fix this download",620);
        panel.Children.Add(new TextBlock { Text=row.FileName,FontSize=20,TextWrapping=TextWrapping.Wrap });
        DialogUi.Label(panel,advice.Help); DialogUi.Label(panel,"Server details: "+row.Job.Error);
        DialogUi.Label(panel,"Save to folder"); var folder=DialogUi.Input(panel,Path.GetDirectoryName(row.Job.Destination)!);
        var browse=new Button { Content="Choose another folder" }; panel.Children.Add(browse);
        browse.Click+=(_,_)=> { using var picker=new System.Windows.Forms.FolderBrowserDialog { InitialDirectory=folder.Text }; if(picker.ShowDialog()==System.Windows.Forms.DialogResult.OK) folder.Text=picker.SelectedPath; };
        var fresh=new Button { Content="Paste a fresh link or login details" }; panel.Children.Add(fresh);
        fresh.Click+=(_,_)=> { dialog.Close(); ReplaceClick(this,new()); };
        DialogUi.Buttons(panel,dialog,"Save folder and retry",()=>
        {
            try
            {
                if(!Path.IsPathFullyQualified(folder.Text)) throw new ArgumentException("Choose an absolute folder path.");
                Directory.CreateDirectory(folder.Text);
                if(!Path.GetFullPath(folder.Text).Equals(Path.GetDirectoryName(row.Job.Destination),StringComparison.OrdinalIgnoreCase))
                {
                    string destination=DownloadNames.ReservePath(folder.Text,row.FileName,allJobs.Where(j=>j.Id!=row.Job.Id).Select(j=>j.Destination));
                    string partial=destination+"."+row.Job.Id+".odpart";
                    if(File.Exists(row.Job.PartialPath)) File.Move(row.Job.PartialPath,partial);
                    row.Job.Destination=destination; row.Job.FileName=Path.GetFileName(destination);
                }
                row.Job.Status=DownloadStatus.Queued; queuePaused=false; Save(); row.Refresh(); Tick(); dialog.DialogResult=true;
            }
            catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or ArgumentException) { MessageBox.Show(dialog,ex.Message,"Choose another folder"); }
        });
        dialog.ShowDialog();
    }
}
