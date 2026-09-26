using System;
using System.Collections.Generic;
using System.Runtime.Versioning;
using NAudio.CoreAudioApi;
using NAudio.MacOS.CoreAudio;
using TaleScribe.Models;

namespace TaleScribe.Services;

/// <summary>
///     Lists the microphones on this machine and which one is the system default, for the Input
///     device setting. Windows uses Core Audio (WASAPI), macOS uses Core Audio devices.
/// </summary>
public static class AudioDeviceService
{
    public static (List<AudioInputDevice> Devices, string? DefaultId) GetInputDevices()
    {
        if (OperatingSystem.IsWindows())
            return GetWindowsInputDevices();

        if (OperatingSystem.IsMacOSVersionAtLeast(10, 4))
            return GetMacInputDevices();

        throw new PlatformNotSupportedException("Only Windows and macOS are supported.");
    }


    [SupportedOSPlatform("windows")]
    private static (List<AudioInputDevice> Devices, string? DefaultId) GetWindowsInputDevices()
    {
        var devices = new List<AudioInputDevice>();

        using var enumerator = new MMDeviceEnumerator();
        using (var endpoints = enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active))
        {
            foreach (var device in endpoints)
                using (device)
                {
                    devices.Add(new AudioInputDevice(device.ID, device.FriendlyName));
                }
        }

        string? defaultId = null;
        if (enumerator.HasDefaultAudioEndpoint(DataFlow.Capture, Role.Console))
        {
            using var defaultDevice = enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Console);
            defaultId = defaultDevice.ID;
        }

        return (devices, defaultId);
    }


    [SupportedOSPlatform("macos10.4")]
    private static (List<AudioInputDevice> Devices, string? DefaultId) GetMacInputDevices()
    {
        var devices = new List<AudioInputDevice>();

        foreach (var device in AudioSystemObject.Instance.Devices)
            if (device.GetStreams(AudioObjectPropertyScopeConstants.Input).Length > 0)
                devices.Add(new AudioInputDevice(device.DeviceUID ?? "", device.Name ?? ""));

        string? defaultId = null;
        try
        {
            defaultId = AudioSystemObject.Instance.DefaultInputDevice?.DeviceUID;
        }
        catch
        {
        }

        return (devices, defaultId);
    }
}