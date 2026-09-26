using System;
using System.Collections.Generic;
using System.IO;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace TaleScribe.Services;

/// <summary>
///     Turns audio into what the transcriber expects: 16 kHz mono float samples. Decodes imported
///     mp3/m4a/wav files, reads recorded WAVs, and converts in-memory quick-memo buffers.
/// </summary>
public static class AudioConverter
{
    /// <summary>
    ///     Decodes an imported mp3, m4a or wav file straight to transcription samples. The reader
    ///     already yields PCM in the file's own format, so there is no intermediate WAV.
    /// </summary>
    public static float[] DecodeFile(string inputPath)
    {
        using WaveStream reader = OpenReader(inputPath);
        return ToTranscriptionSamples(reader.ToSampleProvider());
    }
    
    private static WaveStream OpenReader(string path)
    {
        if (OperatingSystem.IsWindows())
            return new MediaFoundationReader(path);                        // MP3 + M4A via Media Foundation

        if (OperatingSystem.IsMacOS())
            return ExtendedAudioFileReaderFromURL.CreateFromFile(path);    // MP3 + M4A via Audio Toolbox

        throw new PlatformNotSupportedException("Only Windows and macOS are supported.");
    }
    
    public static float[] GetTranscriptionSamples(string wavPath)
    {
        using var reader = new WaveFileReader(wavPath);
        return ToTranscriptionSamples(reader.ToSampleProvider());
    }
    
    private static float[] ToTranscriptionSamples(ISampleProvider source)
    {
        ISampleProvider samples = ToMono(source);

        if (samples.WaveFormat.SampleRate != 16000)
            samples = new WdlResamplingSampleProvider(samples, 16000);

        var result = new List<float>();
        var chunk = new float[4096];
        int read;
        while ((read = samples.Read(chunk.AsSpan(0, chunk.Length))) > 0)
        {
            for (int i = 0; i < read; i++)
                result.Add(chunk[i]);
        }

        return result.ToArray();
    }


    private static ISampleProvider ToMono(ISampleProvider source)
    {
        var channels = source.WaveFormat.Channels;

        if (channels == 1)
            return source;

        if (channels == 2)
            return new StereoToMonoSampleProvider(source) { LeftVolume = 0.5f, RightVolume = 0.5f };
        
        var matrix = new float[channels, 1];
        for (var channel = 0; channel < channels; channel++)
            matrix[channel, 0] = 1f / channels;

        return new ChannelMixerSampleProvider(source, matrix);
    }
    

    public static float[] ConvertMemo(MemoryStream buffer, WaveFormat captureFormat)
    {
        buffer.Position = 0;
        return ToTranscriptionSamples(new RawSourceWaveStream(buffer, captureFormat).ToSampleProvider());
    }
}