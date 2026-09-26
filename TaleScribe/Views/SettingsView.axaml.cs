using System;
using System.Runtime.InteropServices;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using TaleScribe.Services;
using TaleScribe.ViewModels;

namespace TaleScribe.Views;

public partial class SettingsView : UserControl
{
    public SettingsView()
    {
        InitializeComponent();
        Loaded += OnViewLoaded;
    }

    private void OnViewLoaded(object? sender, RoutedEventArgs e)
    {
        if (DataContext is SettingsViewModel settingsViewModel)
        {
            settingsViewModel.LoadTranscriptionModels();
            settingsViewModel.LoadDiarizationModels();
            settingsViewModel.LoadAudioDevices();
            settingsViewModel.LoadLanguages();
        }
    }

    private async void BrowseButton_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not SettingsViewModel settingsViewModel) return;

        try
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel is null) return;

            var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Select a folder",
                AllowMultiple = false,
                SuggestedStartLocation = await topLevel.StorageProvider
                    .TryGetFolderFromPathAsync(AppPathsService.ModelsDir)
            });

            if (folders.Count == 0) return; // user cancelled

            var path = folders[0].TryGetLocalPath();
            if (path is null)
            {
                settingsViewModel.ErrorMessage = "That folder can't be used.";
                return;
            }

            if (settingsViewModel.ChangeModelDirectoryCommand.CanExecute(path))
                settingsViewModel.ChangeModelDirectoryCommand.Execute(path);
        }
        catch (Exception ex)
        {
            settingsViewModel.ErrorMessage = $"The folder could not be changed. {ex.Message}";
        }
    }

    private async void OpenMicSettings_Click(object? sender, RoutedEventArgs e)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return;

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null) return;

        await topLevel.Launcher.LaunchUriAsync(
            new Uri("x-apple.systempreferences:com.apple.preference.security?Privacy_Microphone"));
    }
}