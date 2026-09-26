namespace TaleScribe.Models;

public class Setting
{
    public string? ModelPath { get; set; } = "";

    public string? DiarModelPath { get; set; } = "";

    public string? DeviceUID { get; set; } = "";

    public string? LanguageCode { get; set; } = "auto";

    public string? ModelDirectory { get; set; } = "";
}