using System;
using System.IO;

namespace TaleScribe.Services;

/// <summary>
///     Every file and folder the app owns, in one place. The per-user application data folder is
///     ~/Library/Application Support on macOS and %AppData% on Windows.
/// </summary>
public static class AppPathsService
{
    public static string AppDataDir { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "TaleScribe");
    
    public static string ModelsDir { get; private set; } = Path.Combine(AppDataDir, "models");
    
    public static string RecordingsDir { get; } = Path.Combine(AppDataDir, "recordings");

    public static string SettingsFile { get; } = Path.Combine(AppDataDir, "settings.json");

    public static string DatabaseFile { get; } = Path.Combine(AppDataDir, "taleScribe.db");


    public static void EnsureCreated()
    {
        Directory.CreateDirectory(AppDataDir);
        Directory.CreateDirectory(ModelsDir);
        Directory.CreateDirectory(RecordingsDir);
    }
    
    public static void ChangeModelDirectory(string path)
    {
        Directory.CreateDirectory(path);
        ModelsDir = path;
    }
}