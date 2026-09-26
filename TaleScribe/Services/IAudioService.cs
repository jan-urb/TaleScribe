using System;
using System.Threading.Tasks;

namespace TaleScribe.Services;

/// <summary>
///     Records from the microphone, either to a WAV file (Recordings) or into memory (push to
///     talk). Implemented per OS by WindowsAudioService and MacAudioService.
/// </summary>
public interface IAudioService : IDisposable
{
    bool IsRecording { get; }

    bool IsPaused { get; }
    event Action<Exception>? ErrorOccurred;

    /// <summary>Records to a WAV file at <paramref name="outputPath" />.</summary>
    void Start(string outputPath);

    /// <summary>Stops a recording started with <see cref="Start" /> and returns the WAV path.</summary>
    Task<string?> StopAsync();

    /// <summary>Records into memory for a quick memo; no file is written.</summary>
    void StartMemo();

    /// <summary>Stops a memo and returns 16 kHz mono samples ready for transcription.</summary>
    Task<float[]?> StopMemoAsync();

    /// <summary>Stops keeping audio until <see cref="Resume" />; the device stays open.</summary>
    void Pause();

    /// <summary>Starts keeping audio again after <see cref="Pause" />, in the same file or memo.</summary>
    void Resume();
}