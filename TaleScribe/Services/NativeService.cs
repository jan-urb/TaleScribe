using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using TaleScribe.Models;

namespace TaleScribe.Services;

/// <summary>
///     Calls the native transcribe library (transcribe.cpp): transcribes and diarizes a recording,
///     transcribes quick-memo audio, and merges words and speakers into transcript turns.
/// </summary>
public class NativeService
{
    private const string Lib = "transcribe";

    private readonly Setting _setting;

    public NativeService()
    {
        _setting = SettingsService.Load();
    }

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    private static extern void transcribe_run_params_init(ref TranscribeRunParams p);

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    private static extern int transcribe_open(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string path,
        IntPtr loadParams, IntPtr sessionParams, out IntPtr session);

    [DllImport(Lib, EntryPoint = "transcribe_run", CallingConvention = CallingConvention.Cdecl)]
    private static extern int transcribe_run_default(
        IntPtr session, float[] pcm, int nSamples, IntPtr runParams);

    [DllImport(Lib, EntryPoint = "transcribe_run", CallingConvention = CallingConvention.Cdecl)]
    private static extern int transcribe_run(
        IntPtr session, float[] pcm, int nSamples, in TranscribeRunParams runParams);

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr transcribe_full_text(IntPtr session);

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    private static extern void transcribe_session_free(IntPtr session);

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr transcribe_status_string(int status);

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    private static extern void transcribe_segment_init(ref TranscribeSegment seg);

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    private static extern int transcribe_get_segment(IntPtr session, int i, ref TranscribeSegment seg);

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    private static extern int transcribe_n_segments(IntPtr session);

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    private static extern void transcribe_speaker_segment_init(ref TranscribeSpeakerSegments speakerSeg);

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    private static extern int transcribe_n_speaker_segments(IntPtr session);

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    private static extern int transcribe_get_speaker_segment(IntPtr session, int i,
        ref TranscribeSpeakerSegments speakerSeg);

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    private static extern void transcribe_set_abort_callback(
        IntPtr session, AbortCallback? cb, IntPtr userData);

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.U1)]
    private static extern bool transcribe_was_aborted(IntPtr session);

    private static string StatusText(int status)
    {
        return Marshal.PtrToStringUTF8(transcribe_status_string(status)) ?? $"status {status}";
    }


    public static float[] LoadWav16k(string wavPath)
    {
        var raw = File.ReadAllBytes(wavPath);
        var n = (raw.Length - 44) / 2;
        var pcm = new float[n];
        for (var i = 0; i < n; i++)
            pcm[i] = BitConverter.ToInt16(raw, 44 + i * 2) / 32768f;
        return pcm;
    }


    private static float[] LoadPcm16k(byte[] raw)
    {
        var n = raw.Length / 2;
        var pcm = new float[n];
        for (var i = 0; i < n; i++)
            pcm[i] = BitConverter.ToInt16(raw, i * 2) / 32768f;
        return pcm;
    }

    public Task<RecordingResult> TranscribeFile(string modelPathAsr, string modelPathDiar, float[] pcm,
        CancellationToken ct = default)
    {
        var transcriptionList = new List<TranscriptionResult>();
        var diarizationList = new List<DiarizationResult>();

        var result = Task.Run(() =>
        {
            AbortCallback abortCb = _ =>
            {
                try
                {
                    return ct.IsCancellationRequested;
                }
                catch
                {
                    return false;
                }
            };

            var lang = Marshal.StringToCoTaskMemUTF8(_setting.LanguageCode);
            try
            {
                var stAsr = transcribe_open(modelPathAsr, IntPtr.Zero, IntPtr.Zero, out var asrSession);
                if (stAsr != 0) throw new Exception($"open failed: {StatusText(stAsr)}");

                var stDiar = transcribe_open(modelPathDiar, IntPtr.Zero, IntPtr.Zero, out var diarSession);
                if (stDiar != 0) throw new Exception($"open failed: {StatusText(stDiar)}");

                transcribe_set_abort_callback(asrSession, abortCb, IntPtr.Zero);
                transcribe_set_abort_callback(diarSession, abortCb, IntPtr.Zero);

                try
                {
                    var p = new TranscribeRunParams();
                    transcribe_run_params_init(ref p);
                    if (_setting.LanguageCode != "auto") p.language = lang;

                    p.timestamps = 2;

                    stAsr = transcribe_run(asrSession, pcm, pcm.Length, ref p);
                    if (stAsr == 13) throw new OperationCanceledException(ct);
                    if (stAsr != 0) throw new Exception($"run failed: {StatusText(stAsr)}");

                    //transcription + timestamps
                    var transcription = Marshal.PtrToStringUTF8(transcribe_full_text(asrSession)) ?? "";
                    var n = transcribe_n_segments(asrSession);
                    for (var i = 0; i < n; i++)
                    {
                        var w = new TranscribeSegment();
                        transcribe_segment_init(ref w);
                        if (transcribe_get_segment(asrSession, i, ref w) != 0) continue;
                        if (w.text == IntPtr.Zero) continue;

                        var text = Marshal.PtrToStringUTF8(w.text)!;
                        Console.WriteLine($"[{w.t0_ms}–{w.t1_ms}ms] {text}");
                        var temp = new TranscriptionResult(text, w.t0_ms, w.t1_ms);
                        transcriptionList.Add(temp);
                    }

                    //diarization
                    stDiar = transcribe_run_default(diarSession, pcm, pcm.Length, IntPtr.Zero);
                    if (stDiar == 13) throw new OperationCanceledException(ct);
                    if (stDiar != 0) throw new Exception($"run failed: {StatusText(stDiar)}");
                    var m = transcribe_n_speaker_segments(diarSession);
                    for (var i = 0; i < m; i++)
                    {
                        var w = new TranscribeSpeakerSegments();
                        transcribe_speaker_segment_init(ref w);
                        if (transcribe_get_speaker_segment(diarSession, i, ref w) != 0) continue;
                        if (w.speaker_id == 0) continue;
                        Console.WriteLine($"[{w.t0_ms}–{w.t1_ms}ms] {w.speaker_id}");
                        var temp = new DiarizationResult(w.t0_ms, w.t1_ms, w.speaker_id);
                        diarizationList.Add(temp);
                    }

                    var resultList = new RecordingResult(transcriptionList, diarizationList);
                    return resultList;
                }
                finally
                {
                    transcribe_set_abort_callback(asrSession, null, IntPtr.Zero);
                    transcribe_set_abort_callback(diarSession, null, IntPtr.Zero);
                    transcribe_session_free(asrSession);
                    transcribe_session_free(diarSession);
                }
            }
            finally
            {
                Marshal.FreeCoTaskMem(lang);
                GC.KeepAlive(abortCb);
            }
        }, ct);
        return result;
    }

    public Task<string> MemoTranscribe(float[] pcm, string modelPathAsr, CancellationToken ct =
        default)
    {
        var result = Task.Run(() =>
        {
            AbortCallback abortCb = _ =>
            {
                try
                {
                    return ct.IsCancellationRequested;
                }
                catch
                {
                    return false;
                }
            };

            var lang = Marshal.StringToCoTaskMemUTF8(_setting.LanguageCode);
            try
            {
                var stAsr = transcribe_open(modelPathAsr, IntPtr.Zero, IntPtr.Zero, out var
                    asrSession);
                if (stAsr != 0) throw new Exception($"open failed: {StatusText(stAsr)}");


                transcribe_set_abort_callback(asrSession, abortCb, IntPtr.Zero);

                try
                {
                    var p = new TranscribeRunParams();
                    transcribe_run_params_init(ref p);
                    if (_setting.LanguageCode != "auto") p.language = lang;
                    p.timestamps = 2;

                    stAsr = transcribe_run(asrSession, pcm, pcm.Length, ref p);
                    if (stAsr == 13) throw new OperationCanceledException(ct);
                    if (stAsr != 0) throw new Exception($"run failed: {StatusText(stAsr)}");

                    return Marshal.PtrToStringUTF8(transcribe_full_text(asrSession)) ?? "";
                }
                finally
                {
                    transcribe_set_abort_callback(asrSession, null, IntPtr.Zero);
                    transcribe_session_free(asrSession);
                }
            }
            finally
            {
                Marshal.FreeCoTaskMem(lang);
                GC.KeepAlive(abortCb);
            }
        }, ct);

        return result;
    }


    public List<CombinedResult> CombineTranscriptionDiarization(RecordingResult result)
    {
        var combined = new List<CombinedResult>();


        if (result.Transcription is not { Count: > 0 }) return combined;

        foreach (var segment in result.Transcription)
        {
            if (string.IsNullOrWhiteSpace(segment.Transcription)) continue;
            var text = segment.Transcription.Trim();

            var speakerId = BestOverlapSpeaker(segment, result.Diarization);

            if (combined.Count > 0 && combined[^1].SpeakerId == speakerId)
            {
                var turn = combined[^1];
                turn.Text += " " + text;
                turn.T1 = Math.Max(turn.T1, segment.T1);
                combined[^1] = turn;
            }
            else
            {
                combined.Add(new CombinedResult(text, speakerId, segment.T0, segment.T1));
            }
        }

        return combined;
    }


    private static int BestOverlapSpeaker(TranscriptionResult segment, List<DiarizationResult>? diarization)
    {
        var bestSpeaker = 0;
        long bestOverlap = 0;

        if (diarization is null) return bestSpeaker;

        foreach (var d in diarization)
        {
            var overlap = Math.Min(segment.T1, d.D1) - Math.Max(segment.T0, d.D0);
            if (overlap > bestOverlap)
            {
                bestOverlap = overlap;
                bestSpeaker = d.SpeakerId;
            }
        }

        return bestSpeaker;
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.U1)]
    private delegate bool AbortCallback(IntPtr userData);

    [StructLayout(LayoutKind.Sequential)]
    public struct TranscribeRunParams
    {
        public ulong struct_size;
        public int task;
        public int timestamps;
        public int pnc;
        public int itn;
        public int diarize;
        public IntPtr language;
        public IntPtr target_language;
        [MarshalAs(UnmanagedType.U1)] public bool keep_special_tags;
        public IntPtr family;
        public int spec_k_drafts;
    }


    [StructLayout(LayoutKind.Sequential)]
    public struct TranscribeSegment
    {
        public ulong struct_size;
        public long t0_ms;
        public long t1_ms;
        public int first_word;
        public int n_words;
        public int first_token;
        public int n_tokens;
        public IntPtr text;
        public int speaker_id;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct TranscribeSpeakerSegments
    {
        public ulong struct_size;
        public long t0_ms;
        public long t1_ms;
        public int speaker_id;
        public float p;
    }
}