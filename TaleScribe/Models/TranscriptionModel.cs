using CommunityToolkit.Mvvm.ComponentModel;

namespace TaleScribe.Models;

public partial class TranscriptionModel : ObservableObject
{
    public string Name { get; set; } = "";
    public string Link { get; set; } = "";
    public string Size { get; set; } = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsInstalled))]
    [NotifyPropertyChangedFor(nameof(CanDownload))]
    [NotifyPropertyChangedFor(nameof(IsDownloading))]
    [NotifyPropertyChangedFor(nameof(IsUsing))]
    [NotifyPropertyChangedFor(nameof(CanSelect))]
    private Status _status = Status.Download;

    public bool IsInstalled => Status == Status.Installed;
    public bool CanDownload => Status == Status.Download;
    public bool IsDownloading => Status == Status.Downloading;
    public bool IsUsing => Status == Status.Using;
    
    
    public bool CanSelect => Status is Status.Installed or Status.Using;
}

public enum Status
{
    Installed,
    Download,
    Downloading,
    Using
}