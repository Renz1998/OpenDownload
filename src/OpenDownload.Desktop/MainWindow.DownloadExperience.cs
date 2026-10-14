using System.IO;
using System.Windows;
using System.Windows.Shell;
using OpenDownload.Core;
namespace OpenDownload.Desktop;

public partial class MainWindow
{
    private readonly Queue<DownloadRow> destinationPrompts=[];
    private TransferActivityWindow? activityWindow;
    private bool promptingDestination;
    private void QueueDestinationPrompt(DownloadRow row)
    {
        destinationPrompts.Enqueue(row);
        _=Dispatcher.BeginInvoke(ProcessDestinationPrompts,System.Windows.Threading.DispatcherPriority.Background);
    }
    private void ProcessDestinationPrompts()
    {
        if(promptingDestination || quitting) return;
        promptingDestination=true;
        try
        {
            while(destinationPrompts.TryDequeue(out var row))
            {
                if(!row.Job.AwaitingDestination || row.Job.Hidden) continue;
                string type=DownloadFolders.TypeKey(row.FileName);
                string folder; bool remember=false;
                if(type.Length>0 && settings.TypeFolders.TryGetValue(type,out var saved)) folder=saved;
                else
                {
                    ShowWindow(); var dialog=new DestinationDialog(row,settings) { Owner=this };
                    if(dialog.ShowDialog()!=true) { ShowNotice("Download kept paused. Choose Resume to pick its folder later."); continue; }
                    folder=dialog.DirectoryPath; remember=dialog.RememberType;
                }
                try
                {
                    Directory.CreateDirectory(folder);
                    row.Job.Destination=DownloadNames.ReservePath(folder,row.FileName,allJobs.Where(j=>j.Id!=row.Job.Id).Select(j=>j.Destination));
                    row.Job.FileName=Path.GetFileName(row.Job.Destination);
                    row.Job.AwaitingDestination=false; row.Job.Status=DownloadStatus.Queued;
                    if(remember) DownloadFolders.Remember(settings,row.FileName,folder);
                    Save(); row.Refresh();
                }
                catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or ArgumentException) { row.Job.Status=DownloadStatus.Paused; ShowNotice("Choose another folder: "+ex.Message); }
            }
        }
        finally { promptingDestination=false; }
        Tick();
    }
    private bool ShouldShowTransferWindow(DownloadRow row) => settings.ShowProgressWindow &&
        !settings.TypeFolders.ContainsKey(DownloadFolders.TypeKey(row.FileName));
    private void ShowTransferWindow(DownloadRow row)
    {
        if(!ShouldShowTransferWindow(row) || Environment.GetEnvironmentVariable("OPENDOWNLOAD_TEST_MODE")=="1") return;
        if(activityWindow==null)
        {
            activityWindow=new TransferActivityWindow(r=>Pause(r),r=> { if(!running.ContainsKey(r.Job.Id)) { r.Job.Status=OpenDownload.Core.DownloadStatus.Queued; queuePaused=false; Tick(); } }) { Owner=this };
            activityWindow.Closed+=(_,_)=>activityWindow=null;
            foreach(var active in rows.Where(r=>running.ContainsKey(r.Job.Id))) activityWindow.Add(active);
            activityWindow.Show();
        }
        activityWindow.Add(row);
    }
    private void UpdateTaskbarProgress()
    {
        TaskbarItemInfo??=new TaskbarItemInfo();
        var visible=allJobs.Where(j=>!j.Hidden).ToArray();
        var active=visible.Where(j=>j.Status is DownloadStatus.Downloading or DownloadStatus.Connecting).ToArray();
        if(active.Length==0)
        {
            TaskbarItemInfo.ProgressState=visible.Any(j=>j.Status==DownloadStatus.Failed) ? TaskbarItemProgressState.Error : visible.Any(j=>j.Status==DownloadStatus.Paused && j.Downloaded>0) ? TaskbarItemProgressState.Paused : TaskbarItemProgressState.None;
            var stopped=visible.Where(j=>j.Status is DownloadStatus.Failed or DownloadStatus.Paused && j.TotalBytes>0).ToArray();
            double stoppedTotal=stopped.Sum(j=>(double)(j.TotalBytes??0));
            TaskbarItemInfo.ProgressValue=stoppedTotal>0 ? Math.Clamp(stopped.Sum(j=>(double)j.Downloaded)/stoppedTotal,0,1) : 0;
            return;
        }
        if(active.Any(j=>!j.TotalBytes.HasValue || j.Status==DownloadStatus.Connecting)) { TaskbarItemInfo.ProgressState=TaskbarItemProgressState.Indeterminate; return; }
        TaskbarItemInfo.ProgressState=TaskbarItemProgressState.Normal;
        double total=active.Sum(j=>(double)j.TotalBytes!.Value);
        TaskbarItemInfo.ProgressValue=total>0 ? Math.Clamp(active.Sum(j=>(double)j.Downloaded)/total,0,1) : 0;
    }
}
