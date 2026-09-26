using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace TaleScribe.Models;

public class SpeechRecognitionResult
{
    public int Id { get; set; }
    [StringLength(250)]
    public required string Title { get; set; }
    [StringLength(250)]
    public required string Path { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public List<CombinedResult> Combined { get; set; } = new();
}