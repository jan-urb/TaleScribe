using System;
using System.Threading.Tasks;

namespace TaleScribe.Services;

/// <summary>
///     Plays a saved recording on the results page: load, play, pause, seek and app-only volume.
///     Implemented per OS by WindowsAudioPlayer and MacAudioPlayer.
/// </summary>
public interface IAudioPlayer : IAsyncDisposable
{
    TimeSpan Position { get; }
    TimeSpan Duration { get; }

    Task LoadAsync(string filePath);

    /// <summary>Stops playback and releases the file and output device. LoadAsync can be called again afterwards.</summary>
    Task UnloadAsync();
    void PlayAudio();
    void PauseAudio();
    void Seek(TimeSpan position);

    /// <summary>0 to 1. Changes this app's volume only, never the system volume.</summary>
    void SetVolume(float volume);
}
