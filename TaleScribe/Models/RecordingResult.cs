using System.Collections.Generic;

namespace TaleScribe.Models;

public class RecordingResult
{
    public RecordingResult(
        List<TranscriptionResult> transcription,
        List<DiarizationResult> diarization)
    {
        Transcription = transcription;
        Diarization = diarization;
    }

    public List<DiarizationResult> Diarization { get; set; }
    public List<TranscriptionResult> Transcription { get; set; }
}