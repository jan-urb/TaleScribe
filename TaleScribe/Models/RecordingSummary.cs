using System;

namespace TaleScribe.Models;

public record RecordingSummary(int Id, string Title, DateTime CreatedAt)
{
    public string RecordedOn => CreatedAt.ToLocalTime().ToString("dd.MM.yyyy");
}
