using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using TaleScribe.ViewModels;

namespace TaleScribe.Views;

public partial class ResultsView : UserControl
{
    public ResultsView()
    {
        InitializeComponent();
    }
    
    private async void SaveTranscriptButton_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not RecordingFlowViewModel recordingFlowViewModel) return;

        try
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel is null) return;

            var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Save transcript",
                SuggestedFileName = recordingFlowViewModel.SaveTranscriptFileName,
                DefaultExtension = "txt",
                FileTypeChoices = [FilePickerFileTypes.TextPlain]
            });

            if (file is null) return; // user cancelled

            if (recordingFlowViewModel.SaveTranscriptCommand.CanExecute(file))
                await recordingFlowViewModel.SaveTranscriptCommand.ExecuteAsync(file);
        }
        catch (Exception ex)
        {
            recordingFlowViewModel.ErrorMessage = $"The transcript could not be saved. {ex.Message}";
        }
    }
}