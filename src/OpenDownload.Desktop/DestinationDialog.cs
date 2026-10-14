using System.IO;
using System.Windows;
using System.Windows.Controls;
using OpenDownload.Core;
namespace OpenDownload.Desktop;

public sealed class DestinationDialog : Window
{
    public string DirectoryPath { get; private set; } = "";
    public bool RememberType { get; private set; }
    public DestinationDialog(DownloadRow row, Settings settings)
    {
        var panel=DialogUi.Setup(this,"Save download",600);
        DialogUi.Label(panel,row.FileName); DialogUi.Label(panel,"From: "+row.Source);
        DialogUi.Label(panel,"Save to folder"); var folder=DialogUi.Input(panel,DownloadFolders.ForType(settings,row.FileName));
        var browse=new Button { Content="Choose folder",HorizontalAlignment=HorizontalAlignment.Left };
        browse.Click+=(_,_)=> { using var picker=new System.Windows.Forms.FolderBrowserDialog { InitialDirectory=folder.Text }; if(picker.ShowDialog()==System.Windows.Forms.DialogResult.OK) folder.Text=picker.SelectedPath; }; panel.Children.Add(browse);
        string type=DownloadFolders.TypeKey(row.FileName);
        var remember=DialogUi.Check(panel,type.Length>0 ? "Always save "+type+" files here" : "Always save this file type here (type not known yet)",false);
        remember.IsEnabled=type.Length>0;
        DialogUi.Label(panel,"This choice applies only to this extension. Change or clear remembered folders in Settings.");
        DialogUi.Buttons(panel,this,"Start download",()=>
        {
            try
            {
                if(!Path.IsPathFullyQualified(folder.Text)) throw new ArgumentException("Choose an absolute folder path.");
                DirectoryPath=Path.GetFullPath(folder.Text); Directory.CreateDirectory(DirectoryPath);
                RememberType=remember.IsChecked==true; DialogResult=true;
            }
            catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or ArgumentException) { MessageBox.Show(this,ex.Message,"Check download folder"); }
        });
    }
}
