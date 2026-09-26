using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Platform;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TaleScribe.Models;
using TaleScribe.Services;

namespace TaleScribe.ViewModels;

/// <summary>
///     The Settings page: lists transcription and speaker models, downloads them and picks the one
///     in use, changes the models folder, and chooses language and input device. Every change is
///     saved to settings.json right away.
/// </summary>
public partial class SettingsViewModel : ViewModelBase, IDisposable
{
    private static readonly Uri ModelsJsonUri = new("avares://TaleScribe/Assets/whisper-gguf-models.json");
    private static readonly Uri DiarModelsJsonUri = new("avares://TaleScribe/Assets/diar-models.json");
    private static readonly Uri LanguagesJsonUri = new("avares://TaleScribe/Assets/languages.json");

    private readonly ModelDownloadService _downloader = new();
    private readonly Setting _setting;

    [ObservableProperty] private List<AudioInputDevice>? _audioDeviceList;

    [ObservableProperty] private List<TranscriptionModel>? _diarModelList;

    private bool _disposed;

    [ObservableProperty] private string? _errorMessage;

    [ObservableProperty] private List<Language>? _languagesList;

    [ObservableProperty] private List<TranscriptionModel>? _modelList;

    [ObservableProperty] private string _modelsDirectory = "";

    [ObservableProperty] private AudioInputDevice? _selectedAudioDevice;

    [ObservableProperty] private TranscriptionModel? _selectedDiarModel;

    [ObservableProperty] private Language? _selectedLanguage;

    [ObservableProperty] private string? _selectedModelPath;

    public bool ShowSystemSettings { get; } = OperatingSystem.IsMacOS(); 

    public SettingsViewModel()
    {
        _setting = SettingsService.Load();
        ModelsDirectory = AppPathsService.ModelsDir;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        DownloadCommand.Cancel();
    }

    partial void OnSelectedAudioDeviceChanged(AudioInputDevice? value)
    {
        if (_setting.DeviceUID == value?.Id) return;

        _setting.DeviceUID = value?.Id;
        SaveSettings();
    }

    partial void OnSelectedDiarModelChanged(TranscriptionModel? value)
    {
        if (value is { CanSelect: true })
            UseDiarModel(value);
    }

    private void UseDiarModel(TranscriptionModel model)
    {
        foreach (var other in DiarModelList ?? [])
            if (other != model && other.Status == Status.Using)
                other.Status = Status.Installed;

        model.Status = Status.Using;

        var path = _downloader.PathFor(model);
        if (_setting.DiarModelPath == path) return;

        _setting.DiarModelPath = path;
        SaveSettings();
    }

    partial void OnSelectedLanguageChanged(Language? value)
    {
        if (_setting.LanguageCode == value?.Code) return;

        _setting.LanguageCode = value?.Code;
        SaveSettings();
    }

    private void SaveSettings()
    {
        if (!SettingsService.Save(_setting))
            ErrorMessage = "Your choice could not be saved.";
    }

    public void LoadLanguages()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        try
        {
            using var stream = AssetLoader.Open(LanguagesJsonUri);
            var list = JsonSerializer.Deserialize<List<Language>>(stream, options) ?? new List<Language>();

            LanguagesList = list;

            SelectedLanguage = PickLanguage(list);
        }
        catch (Exception ex)
        {
            LanguagesList = new List<Language>();
            SelectedLanguage = null;
            ErrorMessage = $"The language list could not be loaded. {ex.Message}";
        }
    }

    private Language? PickLanguage(List<Language> languages)
    {
        if (languages.Count == 0) return null;

        return languages.FirstOrDefault(l => l.Code == _setting.LanguageCode)
               ?? languages.FirstOrDefault(l => l.Code == "auto")
               ?? languages[0];
    }

    public void LoadTranscriptionModels()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        try
        {
            using var stream = AssetLoader.Open(ModelsJsonUri);
            var list = JsonSerializer.Deserialize<List<TranscriptionModel>>(stream, options) ??
                       new List<TranscriptionModel>();

            foreach (var model in list)
                if (!IsDownloaded(model))
                {
                    model.Status = Status.Download;
                }
                else if (_downloader.PathFor(model) == _setting.ModelPath)
                {
                    model.Status = Status.Using;
                    SelectedModelPath = _setting.ModelPath;
                }
                else
                {
                    model.Status = Status.Installed;
                }

            ModelList = list;
            ErrorMessage = null;
        }
        catch (Exception ex)
        {
            ModelList = new List<TranscriptionModel>();
            ErrorMessage = $"The model list could not be loaded. {ex.Message}";
        }
    }

    public void LoadDiarizationModels()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        try
        {
            using var stream = AssetLoader.Open(DiarModelsJsonUri);
            var list = JsonSerializer.Deserialize<List<TranscriptionModel>>(stream, options) ??
                       new List<TranscriptionModel>();

            foreach (var model in list)
                if (!IsDownloaded(model))
                    model.Status = Status.Download;
                else if (_downloader.PathFor(model) == _setting.DiarModelPath)
                    model.Status = Status.Using;
                else
                    model.Status = Status.Installed;

            DiarModelList = list;
            SelectedDiarModel = list.FirstOrDefault(m => m.IsUsing) ?? list.FirstOrDefault();
        }
        catch (Exception ex)
        {
            DiarModelList = new List<TranscriptionModel>();
            SelectedDiarModel = null;
            ErrorMessage = $"The speaker model list could not be loaded. {ex.Message}";
        }
    }

    private bool IsDownloaded(TranscriptionModel model)
    {
        return File.Exists(_downloader.PathFor(model));
    }

    [RelayCommand]
    private void SelectModel(TranscriptionModel model)
    {
        if (!model.CanSelect) return;

        UseModel(model);
    }

    private void UseModel(TranscriptionModel model)
    {
        foreach (var other in ModelList ?? [])
            if (other != model && other.Status == Status.Using)
                other.Status = Status.Installed;

        model.Status = Status.Using;
        SelectedModelPath = _downloader.PathFor(model);
        _setting.ModelPath = SelectedModelPath;
        SaveSettings();
    }

    [RelayCommand(IncludeCancelCommand = true)]
    private async Task DownloadAsync(TranscriptionModel model, CancellationToken token)
    {
        if (model.Status != Status.Download) return;

        ErrorMessage = null;
        model.Status = Status.Downloading;
        ChangeModelDirectoryCommand.NotifyCanExecuteChanged();

        try
        {
            await _downloader.DownloadAsync(model.Link, _downloader.PathFor(model), token);
            model.Status = Status.Installed;


            if (model == SelectedDiarModel)
                UseDiarModel(model);
            else if (ModelList?.Contains(model) == true && string.IsNullOrEmpty(SelectedModelPath))
                UseModel(model);
        }
        catch (OperationCanceledException)
        {
            model.Status = Status.Download;
        }
        catch (Exception ex)
        {
            model.Status = Status.Download;
            ErrorMessage = $"{model.Name} could not be downloaded. {ex.Message}";
        }
        finally
        {
            ChangeModelDirectoryCommand.NotifyCanExecuteChanged();
        }
    }

    private bool CanChangeModelDirectory()
    {
        return !DownloadCommand.IsRunning;
    }

    [RelayCommand(CanExecute = nameof(CanChangeModelDirectory))]
    private void ChangeModelDirectory(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;

        try
        {
            AppPathsService.ChangeModelDirectory(path);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"That folder could not be used. {ex.Message}";
            return;
        }

        ModelsDirectory = AppPathsService.ModelsDir;
        _setting.ModelDirectory = ModelsDirectory;

        _setting.ModelPath = null;
        _setting.DiarModelPath = null;
        SelectedModelPath = null;
        SaveSettings();

        LoadTranscriptionModels();
        LoadDiarizationModels();
    }

    public void LoadAudioDevices()
    {
        try
        {
            var (devices, defaultId) = AudioDeviceService.GetInputDevices();

            AudioDeviceList = devices;
            SelectedAudioDevice = PickInputDevice(devices, defaultId);
        }
        catch (Exception ex)
        {
            AudioDeviceList = new List<AudioInputDevice>();
            SelectedAudioDevice = null;
            ErrorMessage = $"The input devices could not be listed. {ex.Message}";
        }
    }

    private AudioInputDevice? PickInputDevice(List<AudioInputDevice> devices, string? defaultId)
    {
        if (devices.Count == 0) return null;

        return devices.FirstOrDefault(d => d.Id == _setting.DeviceUID)
               ?? devices.FirstOrDefault(d => d.Id == defaultId)
               ?? devices[0];
    }
}