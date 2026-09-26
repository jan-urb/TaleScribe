using System;

namespace TaleScribe.Services;

/// <summary>
///     Picks the recorder and player for the current OS: the Windows (WASAPI) or macOS (Core Audio)
///     implementation of IAudioService and IAudioPlayer.
/// </summary>
public static class AudioServiceFactory
{
    public static IAudioService Create()
    {
        if (OperatingSystem.IsWindows())
            return new WindowsAudioService();

        if (OperatingSystem.IsMacOSVersionAtLeast(10, 5))
            return new MacAudioService();

        throw new PlatformNotSupportedException("Only Windows and macOS are supported.");
    }

    public static IAudioPlayer CreatePlayer()
    {
        if (OperatingSystem.IsWindows())
            return new WindowsAudioPlayer();

        if (OperatingSystem.IsMacOSVersionAtLeast(10, 5))
            return new MacAudioPlayer();

        throw new PlatformNotSupportedException("Only Windows and macOS are supported.");
    }
}
