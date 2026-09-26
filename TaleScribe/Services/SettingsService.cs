using System;
using System.IO;
using System.Text.Json;
using TaleScribe.Models;

namespace TaleScribe.Services;

/// <summary>
///     Loads and saves the user's settings (settings.json in the app data folder). A missing or
///     unreadable file falls back to defaults.
/// </summary>
public static class SettingsService
{
    public static Setting Load()
    {
        try
        {
            if (!File.Exists(AppPathsService.SettingsFile))
                return new Setting();

            var json = File.ReadAllText(AppPathsService.SettingsFile);
            return JsonSerializer.Deserialize<Setting>(json) ?? new Setting();
        }
        catch (Exception)
        {
            return new Setting();
        }
    }

    public static bool Save(Setting setting)
    {
        try
        {
            Directory.CreateDirectory(AppPathsService.AppDataDir);

            var json = JsonSerializer.Serialize(setting, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            File.WriteAllText(AppPathsService.SettingsFile, json);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}