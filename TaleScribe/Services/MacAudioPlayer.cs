using System;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace TaleScribe.Services;

/// <summary>
///     macOS playback for the results page, through Core Audio and the Audio Toolbox file reader.
/// </summary>
[SupportedOSPlatform("macos10.5")]
public class MacAudioPlayer : IAudioPlayer
{
    private CoreAudioPlayer? _player;
    private WaveStream? _reader;
    private VolumeSampleProvider? _volume;

    public event EventHandler<StoppedEventArgs>? PlaybackStopped;

    public bool IsLoaded => _player is not null;
    public TimeSpan Position => _reader?.CurrentTime ?? TimeSpan.Zero;
    public TimeSpan Duration => _reader?.TotalTime ?? TimeSpan.Zero;


    public void SetVolume(float volume)
    {
        if (_volume is not null)
            _volume.Volume = Math.Clamp(volume, 0f, 1f);
    }

    public async Task LoadAsync(string filePath)
    {
        await UnloadAsync();

    
        _reader = ExtendedAudioFileReaderFromURL.CreateFromFile(filePath);
        _volume = new VolumeSampleProvider(_reader.ToSampleProvider());

        _player = new CoreAudioPlayer(); // system default output device
        _player.PlaybackStopped += (_, e) => PlaybackStopped?.Invoke(this, e);
        _player.Init(_volume);
    }

    public void PlayAudio()
    {
        if (_player is null || _reader is null)
            return;

        if (_reader.CurrentTime >= _reader.TotalTime)
            _reader.CurrentTime = TimeSpan.Zero;

        _player.Play();
    }

    public void PauseAudio()
    {
        _player?.Pause();
    }

    public void Seek(TimeSpan position)
    {
        if (_reader is not null)
            _reader.CurrentTime = position;
    }

    public async Task UnloadAsync()
    {
        if (_player is not null)
        {
            _player.Stop();
            await _player.DisposeAsync();
        }

        if (_reader is not null)
            await _reader.DisposeAsync();

        _player = null;
        _reader = null;
        _volume = null;
    }

    public ValueTask DisposeAsync() => new(UnloadAsync());
}