using System;
using System.IO;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace TaleScribe.Services;

/// <summary>
///     Windows microphone recording through WASAPI, using the input device chosen in Settings.
///     Writes a WAV for recordings or buffers in memory for a quick memo.
/// </summary>
[SupportedOSPlatform("windows")]
public class WindowsAudioService : IAudioService
{
    private WaveFormat? _captureFormat;
    private CancellationTokenSource? _cts;
    private MMDevice? _device;
    private bool _disposed;
    private Exception? _failure;
    private WasapiRecorder? _input;
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
            _writer = new WaveFileWriter(outputPath, input.WaveFormat);
        }
        catch
        {
            input.Dispose();
            ReleaseDevice();
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
            _cts!.Cancel();

            try
            {
                await task.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
                throw new TimeoutException("Audio capture did not stop within 5 seconds.");
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

    public void StartMemo()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (IsRecording)
            throw new InvalidOperationException("Already recording.");

        _cts?.Dispose();
        _cts = null;
        _recordTask = null;

        var input = CreateRecorder();
        _captureFormat = input.WaveFormat;

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
            _cts!.Cancel();

            try
            {
                await task.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
                throw new TimeoutException("Audio capture did not stop within 5 seconds.");
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

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _pcmBuffer?.Dispose();

        try
        {
            _cts?.Cancel();
        }
        catch
        {
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
                    _writer!.Write(buffer.Data.Span);
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
                if (_input is not null)
                    await _input.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred: {ex.Message}");
            }

            ReleaseDevice();
        }
    }

    private async Task RecordMemoAsync(CancellationToken token)
    {
        try
        {
            await foreach (var buffer in _input!.CaptureAsync(token))
                _pcmBuffer!.Write(buffer.Data.Span);
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
                if (_input is not null)
                    await _input.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred: {ex.Message}");
            }

            ReleaseDevice();
        }
    }

    private WasapiRecorder CreateRecorder()
    {
        _device = FindSavedDevice();

        var builder = new WasapiRecorderBuilder();
        if (_device is not null)
            builder.WithDevice(_device);

        try
        {
            return builder.Build();
        }
        catch
        {
            ReleaseDevice();
            throw;
        }
    }

    private static MMDevice? FindSavedDevice()
    {
        var id = SettingsService.Load().DeviceUID;
        if (string.IsNullOrEmpty(id)) return null;

        try
        {
            using var enumerator = new MMDeviceEnumerator();
            var device = enumerator.GetDevice(id);
            if (device.State == DeviceState.Active) return device;

            device.Dispose();
            return null;
        }
        catch
        {
            return null;
        }
    }

    private void ReleaseDevice()
    {
        try
        {
            _device?.Dispose();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred: {ex.Message}");
        }

        _device = null;
    }
}