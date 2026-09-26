namespace TaleScribe.Models;

public class DiarizationResult
{
    public DiarizationResult(long d0, long d1, int speakerId)
    {
        D0 = d0;
        D1 = d1;
        SpeakerId = speakerId;
    }

    public long D0 { get; set; }
    public long D1 { get; set; }
    public int SpeakerId { get; set; }
}