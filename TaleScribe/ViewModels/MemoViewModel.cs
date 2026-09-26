using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NAudio.MacOS.CoreAudio;
using TaleScribe.Services;

namespace TaleScribe.ViewModels;


public enum MemoState
{
    Idle,
    Recording,
}

/// <summary>
///     The Quick memo page: records while the button is held, transcribes each press and appends
///     the text. Nothing is saved to disk or the database.
/// </summary>
public partial class MemoViewModel: ViewModelBase, IDisposable
{
    private readonly string? _asrModelPath;
       
    private readonly NativeService _nativeService;
    private readonly IAudioService _audioService;
    
    private bool _disposed;

    [ObservableProperty]
    private string? _errorMessage;
    
    [ObservableProperty]
    private string? _result;
    
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartCommand))]
    [NotifyCanExecuteChangedFor(nameof(StopCommand))]
    [NotifyCanExecuteChangedFor(nameof(ResetCommand))]
    private bool _isRunning;
    

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsIdle))]
    [NotifyPropertyChangedFor(nameof(IsRecording))]
    private MemoState _state = MemoState.Idle;

    public bool IsIdle => State == MemoState.Idle;
    public bool IsRecording => State == MemoState.Recording;

    public MemoViewModel()
    {
        _nativeService = new NativeService();
        _audioService = AudioServiceFactory.Create();
        _audioService.ErrorOccurred += OnRecorderError;
        
        _asrModelPath = SettingsService.Load().ModelPath;
    }

    private bool CanStart() => !IsRunning;
    
    private bool CanStop() => IsRunning;
    
    
    [RelayCommand(CanExecute = nameof(CanStart))]
    private void BeginRecording()
    {
        if (State != MemoState.Recording)
        {
            State = MemoState.Recording;
        }
    }
    
    [RelayCommand(CanExecute = nameof(CanStart))]
    private void Start()
    {
        ErrorMessage = null;
        
        try
        {
            _audioService.StartMemo();
        }
        catch (Exception ex)
        {
            ErrorMessage = Describe(ex);
            return;
        }

        IsRunning = true;
    }


    [RelayCommand(IncludeCancelCommand = true, CanExecute = nameof(CanStop))]
    private async Task StopAsync(CancellationToken token)
    {
        try
        {

            var pcm = await Task.Run(() => _audioService.StopMemoAsync());
            var text = await ProcessAudio(pcm, token);

            if (!string.IsNullOrWhiteSpace(text))
            {
                Result = string.IsNullOrWhiteSpace(Result)
                    ? text.Trim()
                    : $"{Result} {text.Trim()}";
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            ErrorMessage = DescribeProcessingFailure(ex);
        }
        finally
        {
            IsRunning = false;
        }
    }

    
    private async Task<string> ProcessAudio(float[]? pcm, CancellationToken token)
    {
        if (pcm is null || pcm.Length == 0)
            throw new InvalidOperationException("Recording contained no audio.");

        if (string.IsNullOrEmpty(_asrModelPath) || !File.Exists(_asrModelPath))
            throw new InvalidOperationException("Choose and download a transcription model in Settings first.");
        
        return await _nativeService.MemoTranscribe(pcm, _asrModelPath, token);
    }
    
    [RelayCommand(CanExecute = nameof(CanStart))]
    private void Reset()   //clear button
    {
        ErrorMessage = null;
        Result = null;
        State = MemoState.Idle;
    }
    
    
    private void OnRecorderError(Exception ex)
    {
        Dispatcher.UIThread.Post(() =>
        {
            ErrorMessage = Describe(ex);
            IsRunning = false;
        });
    }
    

    private static string Describe(Exception ex) => ex switch
    {
        NotSupportedException => ex.Message,
        CoreAudioException or COMException or InvalidOperationException =>
            "No microphone is available, or access to it was denied.",
        UnauthorizedAccessException or IOException =>
            "The microphone could not be read.",
        _ => $"Recording failed."
    };

 
    private static string DescribeProcessingFailure(Exception ex) => ex switch
    {
        InvalidOperationException => ex.Message,
        IOException => "The recording file could not be read.",
        ArgumentException or NotSupportedException =>
            $"The recording is in a format that cannot be transcribed. {ex.Message}",
        _ => $"The recording could not be transcribed. {ex.Message}"
    };

    
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _audioService.ErrorOccurred -= OnRecorderError;
        _audioService.Dispose();
    }
}
