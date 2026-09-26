using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using TaleScribe.ViewModels;

namespace TaleScribe.Views;

public partial class RecordingsListView : UserControl
{
    private static readonly FilePickerFileType AudioFiles = new("Audio")
    {
        Patterns = ["*.mp3", "*.m4a", "*.wav"]
    };

    public RecordingsListView()
    {
        InitializeComponent();
    }
    
    private async void OpenFileButton_Clicked(object sender, RoutedEventArgs args)
    {
        if (DataContext is not RecordingFlowViewModel recordingFlowViewModel) return;

        try
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel is null) return;

            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Open audio file",
                AllowMultiple = false,
                FileTypeFilter = [AudioFiles]
            });

            if (files.Count == 0) return;

            var path = files[0].TryGetLocalPath();
            if (path is null)
            {
                recordingFlowViewModel.ErrorMessage = "That file can't be opened.";
                return;
            }

            if (recordingFlowViewModel.ImportCommand.CanExecute(path))
                await recordingFlowViewModel.ImportCommand.ExecuteAsync(path);
        }
        catch (Exception ex)
        {
            recordingFlowViewModel.ErrorMessage = $"The file could not be imported. {ex.Message}";
        }
    }
}