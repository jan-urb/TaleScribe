using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NAudio.MacOS.CoreAudio;
using TaleScribe.Models;
using TaleScribe.Services;

namespace TaleScribe.ViewModels;

public enum RecorderState
{
    Idle,
    Recording,
    Processing,
    Finished
}

/// <summary>
///     The Recordings page and everything reached from it: the recordings list, recording from the
///     microphone or importing a file, transcribing and saving it, and the results page with its
///     audio player. State decides which of the four views is shown.
/// </summary>
public partial class RecordingFlowViewModel : ViewModelBase, IDisposable
{
    private readonly string? _asrModelPath;
    private readonly IAudioService _audioService;
    private readonly DatabaseService _databaseService;

    private readonly string? _diarModelPath;

    private readonly NativeService _nativeService;


    //audio player
    private readonly IAudioPlayer _player = AudioServiceFactory.CreatePlayer();
    private readonly DispatcherTimer _positionTimer;

    private string? _currentPath;
    private bool _disposed;

    [ObservableProperty] private double _durationSeconds;

    [ObservableProperty] private string? _errorMessage;

    [ObservableProperty] private bool _isLoaded;

    [ObservableProperty]                                                                          
    [NotifyPropertyChangedFor(nameof(PauseText))]                                                 
    private bool _isPaused; 
    
    public string PauseText => IsPaused ? "Resume" : "Pause";   

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(PlayPauseText))]
    private bool _isPlaying;

    [ObservableProperty] private double _positionSeconds;

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(RecordedOn))]
    [NotifyPropertyChangedFor(nameof(SaveTranscriptFileName))]
    [NotifyCanExecuteChangedFor(nameof(SaveTranscriptCommand))]
    private SpeechRecognitionResult? _recording;

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(RecordingsCountText))]
    private List<RecordingSummary> _recordings = new();

    [ObservableProperty] private List<CombinedResult>? _result;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsIdle))]
    [NotifyPropertyChangedFor(nameof(IsRecording))]
    [NotifyPropertyChangedFor(nameof(IsProcessing))]
    [NotifyPropertyChangedFor(nameof(IsFinished))]
    [NotifyCanExecuteChangedFor(nameof(OpenRecordingCommand))]
    [NotifyCanExecuteChangedFor(nameof(StartCommand))]
    [NotifyCanExecuteChangedFor(nameof(StopCommand))]
    [NotifyCanExecuteChangedFor(nameof(CancelCommand))]
    [NotifyCanExecuteChangedFor(nameof(ImportCommand))]
    private RecorderState _state = RecorderState.Idle;

    [ObservableProperty] private string _trackName = "No audio selected";

    private bool _updatingPosition;

    [ObservableProperty] private double _volume = 0.75;

    private int resultId;

    public RecordingFlowViewModel()
    {
        _asrModelPath = SettingsService.Load().ModelPath;
        _diarModelPath = SettingsService.Load().DiarModelPath;

        _nativeService = new NativeService();
        _audioService = AudioServiceFactory.Create();
        _databaseService = new DatabaseService();
        _audioService.ErrorOccurred += OnRecorderError;

        _ = LoadRecordingsAsync();


        _positionTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(200)
        };

        _positionTimer.Tick += (_, _) => RefreshPosition();
    }

    public string? RecordedOn => Recording?.CreatedAt.ToLocalTime().ToString("dd.MM.yyyy HH:mm");

    public string RecordingsCountText =>
        $"{Recordings.Count} recording{(Recordings.Count == 1 ? "" : "s")} · nothing leaves this machine";

    public bool IsIdle => State == RecorderState.Idle;
    public bool IsRecording => State == RecorderState.Recording;
    public bool IsProcessing => State == RecorderState.Processing;
    public bool IsFinished => State == RecorderState.Finished;


    public string PlayPauseText => IsPlaying ? "Pause" : "Play";

    public string TimeText =>
        $"{TimeSpan.FromSeconds(PositionSeconds):m\\:ss} / " +
        $"{TimeSpan.FromSeconds(DurationSeconds):m\\:ss}";

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _audioService.ErrorOccurred -= OnRecorderError;
        _audioService.Dispose();

        _positionTimer.Stop();
        _ = _player.DisposeAsync();
    }

    partial void OnStateChanged(RecorderState value)
    {
        if (value == RecorderState.Idle)
            _ = LoadRecordingsAsync();
    }

    private async Task LoadRecordingsAsync()
    {
        try
        {
            Recordings = await _databaseService.GetRecordsAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage ??= $"Your recordings could not be loaded. {ex.Message}";
        }
    }

    [RelayCommand(CanExecute = nameof(IsIdle))]
    private async Task OpenRecordingAsync(int id)
    {
        ErrorMessage = null;
        resultId = id;

        if (await LoadResultAsync())
            State = RecorderState.Finished;
    }


    [RelayCommand(CanExecute = nameof(IsIdle))]
    private void Start()
    {
        ErrorMessage = null;
        IsPaused = false;

        var outputPath = Path.Combine(AppPathsService.RecordingsDir, $"recording-{DateTime.Now:yyyyMMdd-HHmmss}.wav");

        try
        {
            _audioService.Start(outputPath);
        }
        catch (Exception ex)
        {
            ErrorMessage = Describe(ex);
            return;
        }

        State = RecorderState.Recording;
    }

    [RelayCommand(CanExecute = nameof(IsRecording))]
    private async Task CancelAsync()
    {
        string? path = null;
        IsPaused = false;
        try
        {
            path = await _audioService.StopAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = Describe(ex);
        }
        finally
        {
            TryDelete(path);
            _currentPath = null;
            Result = null;
            Recording = null;
            State = RecorderState.Idle;
        }
    }

    [RelayCommand(IncludeCancelCommand = true, CanExecute = nameof(IsRecording))]
    private async Task StopAsync(CancellationToken token)
    {
        State = RecorderState.Processing;
        IsPaused = false;
        string? path;
        try
        {
            path = await _audioService.StopAsync();
        }
        catch (Exception ex)
        {
            _currentPath = null;
            ErrorMessage = Describe(ex);
            State = RecorderState.Idle;
            return;
        }

        _currentPath = path;

        if (path is null)
        {
            ErrorMessage = "There was no recording to transcribe.";
            State = RecorderState.Idle;
            return;
        }

        await TranscribeAndSaveAsync(() => AudioConverter.GetTranscriptionSamples(path), path, token);
    }

    [RelayCommand]
    private void TogglePause()
    {
        if (_audioService.IsPaused) _audioService.Resume();
        else _audioService.Pause();

        IsPaused = _audioService.IsPaused;
    }

    [RelayCommand(IncludeCancelCommand = true, CanExecute = nameof(IsIdle))]
    private async Task ImportAsync(string path, CancellationToken token)
    {
        ErrorMessage = null;
        State = RecorderState.Processing;
        _currentPath = path;

        await TranscribeAndSaveAsync(() => AudioConverter.DecodeFile(path), path, token);
    }

    [RelayCommand]
    private void CancelProcessing()
    {
        if (StopCommand.IsRunning) StopCommand.Cancel();
        if (ImportCommand.IsRunning) ImportCommand.Cancel();
    }

    private async Task TranscribeAndSaveAsync(Func<float[]> decode, string path, CancellationToken token)
    {
        try
        {
            var pcm = await Task.Run(decode, token);

            await ProcessAudio(pcm, token);

            if (!await SaveResultAsync(path))
            {
                Result = null;
                State = RecorderState.Idle;
                return;
            }

            if (!await LoadResultAsync())
            {
                Result = null;
                State = RecorderState.Idle;
                return;
            }

            State = RecorderState.Finished;
        }
        catch (OperationCanceledException)
        {
            State = RecorderState.Idle;
        }
        catch (Exception ex)
        {
            _currentPath = null;
            ErrorMessage = DescribeProcessingFailure(ex);
            State = RecorderState.Idle;
        }
    }

    [RelayCommand]
    private async Task ResetAsync() // the "back" button on the results view
    {
        _positionTimer.Stop();
        IsPlaying = false;
        IsLoaded = false;
        await _player.UnloadAsync();

        ErrorMessage = null;
        Result = null;
        Recording = null;
        _currentPath = null;
        PositionSeconds = 0;
        DurationSeconds = 0;
        TrackName = "No audio selected";
        State = RecorderState.Idle;
    }

    private async Task ProcessAudio(float[] pcm, CancellationToken token)
    {
        if (pcm.Length == 0) throw new InvalidOperationException("Recording contained no audio.");

        if (string.IsNullOrEmpty(_asrModelPath) || !File.Exists(_asrModelPath))
            throw new InvalidOperationException("Choose and download a transcription model in Settings first.");
        if (string.IsNullOrEmpty(_diarModelPath) || !File.Exists(_diarModelPath))
            throw new InvalidOperationException("Choose and download a speaker (diarization) model in Settings first.");

        var result = await _nativeService.TranscribeFile(_asrModelPath, _diarModelPath, pcm, token);
        Result = _nativeService.CombineTranscriptionDiarization(result);
    }

    private async Task<bool> SaveResultAsync(string path)
    {
        var combined = Result;
        if (combined is null) return true;

        try
        {
            resultId = await _databaseService.AddRecordingAsync(
                combined, path, Path.GetFileNameWithoutExtension(path));
            return true;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"The transcript could not be saved. {ex.Message}";
            return false;
        }
    }

    private async Task<bool> LoadResultAsync()
    {
        try
        {
            Recording = await _databaseService.GetRecordDetailsAsync(resultId);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"The saved transcript could not be loaded. {ex.Message}";
            return false;
        }

        if (Recording is null)
        {
            ErrorMessage = "The saved transcript could not be found.";
            return false;
        }

        //load audio file
        try
        {
            await _player.LoadAsync(Recording.Path);
            _player.SetVolume((float)Volume);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"The audio could not be loaded. {ex.Message}";
            IsLoaded = false;
            return true;
        }

        TrackName = Path.GetFileName(Recording.Path);
        DurationSeconds = _player.Duration.TotalSeconds;
        PositionSeconds = 0;
        IsPlaying = false;
        IsLoaded = true;

        return true;
    }
    
    /// <summary>The name the save dialog suggests: the recording's title as a .txt file.</summary>
    public string SaveTranscriptFileName
    {
        get
        {
            var title = Recording?.Title;
            if (string.IsNullOrWhiteSpace(title)) return "transcript.txt";

            return $"{title}.txt";
        }
    }

    private bool CanSaveTranscript() => Recording is not null;

    /// <summary>
    ///     Writes the transcript on the results screen to <paramref name="file" /> as plain text: title,
    ///     date, then each turn with its speaker and start time. The view picks the file.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanSaveTranscript))]
    private async Task SaveTranscriptAsync(IStorageFile file)
    {
        if (Recording is null) return;

        try
        {
            await using var stream = await file.OpenWriteAsync();

            // Overwriting a longer file would otherwise leave its old ending behind.
            if (stream.CanSeek) stream.SetLength(0);

            // UTF-8, so letters such as č, š and ž are kept.
            await using var writer = new StreamWriter(stream, new UTF8Encoding(false));
            await writer.WriteAsync(BuildTranscriptText(Recording));
        }
        catch (Exception ex)
        {
            ErrorMessage = $"The transcript could not be saved. {ex.Message}";
        }
    }

    private string BuildTranscriptText(SpeechRecognitionResult recording)
    {
        var text = new StringBuilder();
        text.AppendLine(recording.Title);
        text.AppendLine(RecordedOn);

        foreach (var turn in recording.Combined)
        {
            var start = TimeSpan.FromMilliseconds(Math.Max(0, turn.T0));
            var time = start.TotalHours >= 1 ? start.ToString(@"h\:mm\:ss") : start.ToString(@"m\:ss");

            text.AppendLine();
            text.AppendLine($"Speaker {turn.SpeakerId}  {time}");
            text.AppendLine(turn.Text);
        }

        return text.ToString();
    }

    private void OnRecorderError(Exception ex)
    {
        Dispatcher.UIThread.Post(() =>
        {
            ErrorMessage = Describe(ex);

            if (State == RecorderState.Recording)
                State = RecorderState.Idle;
        });
    }


    private static string Describe(Exception ex)
    {
        return ex switch
        {
            NotSupportedException => ex.Message,
            CoreAudioException or COMException or InvalidOperationException =>
                "No microphone is available, or access to it was denied.",
            UnauthorizedAccessException or IOException =>
                $"The recording could not be saved to {AppPathsService.RecordingsDir}.",
            _ => "Recording failed."
        };
    }


    private static string DescribeProcessingFailure(Exception ex)
    {
        return ex switch
        {
            InvalidOperationException => ex.Message, // e.g. "Recording contained no audio."
            IOException => "The recording file could not be read.",
            ArgumentException or NotSupportedException =>
                $"The recording is in a format that cannot be transcribed. {ex.Message}",
            _ => $"The recording could not be transcribed. {ex.Message}"
        };
    }

    private static void TryDelete(string? path)
    {
        if (path is null) return;

        try
        {
            File.Delete(path);
        }
        catch (Exception)
        {
        }
    }

    [RelayCommand]
    private void TogglePlayback()
    {
        if (IsPlaying)
        {
            _player.PauseAudio();
            _positionTimer.Stop();
        }
        else
        {
            _player.PlayAudio();
            _positionTimer.Start();
        }

        IsPlaying = !IsPlaying;
    }

    partial void OnVolumeChanged(double value)
    {
        _player.SetVolume((float)value);
    }

    partial void OnPositionSecondsChanged(double value)
    {
        if (!_updatingPosition && IsLoaded)
            _player.Seek(TimeSpan.FromSeconds(value));

        OnPropertyChanged(nameof(TimeText));
    }

    partial void OnDurationSecondsChanged(double value)
    {
        OnPropertyChanged(nameof(TimeText));
    }

    private void RefreshPosition()
    {
        if (!IsLoaded)
            return;

        _updatingPosition = true;
        PositionSeconds = _player.Position.TotalSeconds;
        _updatingPosition = false;

        if (_player.Position >= _player.Duration)
        {
            _positionTimer.Stop();
            IsPlaying = false;
        }
    }
}