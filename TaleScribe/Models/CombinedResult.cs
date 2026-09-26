namespace TaleScribe.Models;

public class CombinedResult
{
    
    public CombinedResult(string text, int speakerId, long t0, long t1)
    {
        Text = text;
        SpeakerId = speakerId;
        T0 = t0;
        T1 = t1;
    }

    public int Id { get; set; }
    public string Text { get; set; } = "";
    public int SpeakerId { get; set; }
    public long T0 { get; set; }
    public long T1 { get; set; }

    public int SpeechRecognitionResultId { get; set; }
}