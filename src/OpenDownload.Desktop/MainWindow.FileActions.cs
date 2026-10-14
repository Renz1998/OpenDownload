using System.IO;
using System.Windows;
using OpenDownload.Core;
using Microsoft.VisualBasic.FileIO;
namespace OpenDownload.Desktop;

public partial class MainWindow
{
    private readonly HashSet<string> deleting = [];
    private async void DeleteClick(object sender, RoutedEventArgs e)
    {
        var selected = Selected().ToArray(); if(selected.Length == 0) return;
        if(MessageBox.Show(this,$"Remove {selected.Length} download(s) and move their downloaded and partial files to the Recycle Bin? Active transfers will stop. This cannot be undone from OpenDownload.","Remove and delete files",MessageBoxButton.YesNo,MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        foreach(var row in selected) { deleting.Add(row.Job.Id); Pause(row); }
        try
        {
            await Task.WhenAll(selected.Where(r=>running.ContainsKey(r.Job.Id)).Select(r=>running[r.Job.Id].Task).ToArray());
            foreach(var row in selected)
            {
                try
                {
                    RecycleDownloadFiles(row.Job);
                    store.Remove(row.Job.Id); allJobs.Remove(row.Job); rows.Remove(row); removed.Remove(row); lastBytes.Remove(row.Job.Id);
                }
                catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or OperationCanceledException)
                { ShowNotice("Could not delete " + row.FileName + ". The entry was kept. " + ex.Message); return; }
            }
            Save(); UpdateEmptyState(); ShowNotice("Entries removed. Their files were moved to the Recycle Bin.");
        }
        finally { foreach(var row in selected) deleting.Remove(row.Job.Id); }
    }
    internal static void RecycleDownloadFiles(DownloadJob job)
    {
        foreach(string path in new[]{job.Destination,job.PartialPath})
        {
            if(!File.Exists(path)) continue;
            if(File.GetAttributes(path).HasFlag(FileAttributes.ReparsePoint)) throw new IOException("Symbolic-link files are not deleted by OpenDownload.");
            FileSystem.DeleteFile(Path.GetFullPath(path),UIOption.OnlyErrorDialogs,RecycleOption.SendToRecycleBin,UICancelOption.ThrowException);
        }
    }
    private void DismissNoticeClick(object sender,RoutedEventArgs e) => NoticeBar.Visibility=Visibility.Collapsed;
}
