using System;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using NAudio.Wave;

namespace TaleScribe.Services;

/// <summary>
///     Windows playback for the results page, through WASAPI and Media Foundation.
/// </summary>
[SupportedOSPlatform("windows")]
public class WindowsAudioPlayer : IAudioPlayer
{
    private WasapiPlayer? _player;
    private MediaFoundationReader? _reader;

    public TimeSpan Position => _reader?.CurrentTime ?? TimeSpan.Zero;
    public TimeSpan Duration => _reader?.TotalTime ?? TimeSpan.Zero;

    public async Task LoadAsync(string filePath)
    {
        await UnloadAsync();

        _reader = new MediaFoundationReader(filePath);

        _player = new WasapiPlayerBuilder()
            .WithSharedMode()  
            .WithEventSync()  
            .Build();

        _player.Init(_reader);
    }

    public void PlayAudio()
    {
        if (_player is null || _reader is null)
            return;
        
        if (_reader.CurrentTime >= _reader.TotalTime)
            _reader.CurrentTime = TimeSpan.Zero;

        _player.Play();
    }

    public void PauseAudio() => _player?.Pause();

    public void SetVolume(float volume)
    {
        if (_player is not null)
            _player.Volume = Math.Clamp(volume, 0f, 1f);
    }

    public void Seek(TimeSpan position)
    {
        if (_reader is not null)
            _reader.CurrentTime = position;
    }

    public async Task UnloadAsync()
    {
        _player?.Stop();

        if (_player is not null)
            await _player.DisposeAsync();

        _reader?.Dispose();
        _player = null;
        _reader = null;
    }

    public ValueTask DisposeAsync() => new(UnloadAsync());
}
