namespace TaleScribe.Models;

public class TranscriptionResult
{
    public TranscriptionResult(string transcription, long t0, long t1)
    {
        Transcription = transcription;
        T0 = t0;
        T1 = t1;
    }

    public long T0 { get; set; }
    public long T1 { get; set; }
    public string Transcription { get; set; }
}