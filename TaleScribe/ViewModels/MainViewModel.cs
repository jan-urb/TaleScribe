using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace TaleScribe.ViewModels;

/// <summary>
///     The main window: holds the page shown next to the sidebar and switches between Recordings,
///     Quick memo and Settings. Disposes the page being left so it releases the microphone.
/// </summary>
public partial class MainViewModel : ViewModelBase
{
    [ObservableProperty]
    private ViewModelBase _currentPage;

    public MainViewModel()
    {
        _currentPage = new RecordingFlowViewModel();
    }
    
    partial void OnCurrentPageChanging(ViewModelBase? oldValue, ViewModelBase newValue)
    {
        if (!ReferenceEquals(oldValue, newValue))
            (oldValue as IDisposable)?.Dispose();
    }

    [RelayCommand]
    private void NavigateToRecordingsList()
    {
        CurrentPage = new RecordingFlowViewModel();
    }
    
    [RelayCommand]
    private void NavigateToMemo()
    {
        CurrentPage = new MemoViewModel();
    }
    
    [RelayCommand]
    private void NavigateToSettings()
    {
        CurrentPage = new SettingsViewModel();
    }
}