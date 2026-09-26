using System;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;
using NAudio.MacOS.CoreAudio;
using NAudio.Wave;

namespace TaleScribe.Services;

/// <summary>
///     macOS microphone recording through Core Audio, using the input device chosen in Settings.
///     Writes a WAV for recordings or buffers in memory for a quick memo.
/// </summary>
[SupportedOSPlatform("macos10.5")]
public class MacAudioService : IAudioService
{
    private WaveFormat? _captureFormat;
    private CancellationTokenSource? _cts;
    private bool _disposed;
    private Exception? _failure;
    private CoreAudioRecorder? _input;
    private string? _outputPath;
    private MemoryStream? _pcmBuffer;
    private Task? _recordTask;
    private WaveFileWriter? _writer;

    public event Action<Exception>? ErrorOccurred;

    public bool IsRecording => _recordTask is { IsCompleted: false };

    public bool IsPaused { get; private set; }

    public void Start(string outputPath)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        IsPaused = false;

        if (IsRecording)
            throw new InvalidOperationException("Already recording.");

        _cts?.Dispose();
        _cts = null;
        _recordTask = null;

        var input = CreateRecorder();
        try
        {
            input.InitializeRecording();
            _writer = new WaveFileWriter(outputPath, input.CaptureFormat);
        }
        catch
        {
            input.Dispose();
            throw;
        }

        _input = input;
        _outputPath = outputPath;
        _failure = null;
        _cts = new CancellationTokenSource();
        _recordTask = Task.Run(() => RecordAsync(_cts.Token));
    }

    public async Task<string?> StopAsync()
    {
        var task = _recordTask;
        if (task is null) return null;

        try
        {
            try
            {
                _input?.StopRecording();
            }
            catch
            {
                _cts!.Cancel();
            }

            try
            {
                await task.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
                _cts!.Cancel();
                await task.ConfigureAwait(false);
            }
        }
        finally
        {
            _cts?.Dispose();
            _cts = null;
            _recordTask = null;
            _input = null;
        }

        var path = _outputPath;
        _outputPath = null;

        if (_failure is not null)
        {
            var failure = _failure;
            _failure = null;
            throw failure;
        }

        return path;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _pcmBuffer?.Dispose();

        try
        {
            _input?.StopRecording();
        }
        catch
        {
            // ignored
        }

        try
        {
            _cts?.Cancel();
        }
        catch
        {
            // ignored
        }
    }


    public void StartMemo()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (IsRecording)
            throw new InvalidOperationException("Already recording.");

        _cts?.Dispose();
        _cts = null;
        _recordTask = null;

        var input = CreateRecorder();
        try
        {
            input.InitializeRecording();
            _captureFormat = input.CaptureFormat;
        }
        catch
        {
            input.Dispose();
            throw;
        }

        _pcmBuffer?.Dispose();
        _pcmBuffer = new MemoryStream();

        _input = input;
        _failure = null;
        _cts = new CancellationTokenSource();
        _recordTask = Task.Run(() => RecordMemoAsync(_cts.Token));
    }

    public async Task<float[]?> StopMemoAsync()
    {
        var task = _recordTask;
        if (task is null) return null;

        try
        {
            try
            {
                _input?.StopRecording();
            }
            catch
            {
                _cts!.Cancel();
            }

            try
            {
                await task.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
                _cts!.Cancel();
                await task.ConfigureAwait(false);
            }
        }
        finally
        {
            _cts?.Dispose();
            _cts = null;
            _recordTask = null;
            _input = null;
        }

        var buffer = _pcmBuffer;
        _pcmBuffer = null;

        using (buffer)
        {
            if (_failure is not null)
            {
                var failure = _failure;
                _failure = null;
                throw failure;
            }

            return buffer is null ? null : AudioConverter.ConvertMemo(buffer, _captureFormat!);
        }
    }

    public void Pause()
    {
        if (IsRecording) IsPaused = true;
    }

    public void Resume()
    {
        IsPaused = false;
    }

    private async Task RecordAsync(CancellationToken token)
    {
        try
        {
            await foreach (var buffer in _input!.CaptureAsync(token))
                if (!IsPaused)
                    _writer!.Write(buffer.Buffer, 0, buffer.Buffer.Length);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _failure = ex;
            ErrorOccurred?.Invoke(ex);
        }
        finally
        {
            try
            {
                _writer?.Dispose();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred: {ex.Message}");
            }

            _writer = null;

            try
            {
                _input?.Dispose();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred: {ex.Message}");
            }
        }
    }

    private static CoreAudioRecorder CreateRecorder()
    {
        var id = SettingsService.Load().DeviceUID;
        AudioDevice? device = null;

        if (!string.IsNullOrEmpty(id))
            try
            {
                device = AudioSystemObject.Instance.Devices.FirstOrDefault(d => d.DeviceUID == id);
            }
            catch
            {
            }

        return device is null ? new CoreAudioRecorder() : new CoreAudioRecorder(device);
    }

    private async Task RecordMemoAsync(CancellationToken token)
    {
        try
        {
            await foreach (var buffer in _input!.CaptureAsync(token))
                _pcmBuffer!.Write(buffer.Buffer, 0, buffer.Buffer.Length);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _failure = ex;
            ErrorOccurred?.Invoke(ex);
        }
        finally
        {
            try
            {
                _input?.Dispose();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred: {ex.Message}");
            }
        }
    }
}