using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using TaleScribe.Models;

namespace TaleScribe.Services;

/// <summary>
///     Downloads transcription and speaker models into the models folder. Writes to a .part file
///     first and only renames it when complete, so a cancelled download never leaves a broken
///     model.
/// </summary>
public class ModelDownloadService
{
    

    private static readonly HttpClient Http = new() { Timeout = Timeout.InfiniteTimeSpan };
    
    public string PathFor(TranscriptionModel model) => Path.Combine(AppPathsService.ModelsDir, model.Name);
    
    public async Task DownloadAsync(string url, string destination, CancellationToken ct)
    {
        Directory.CreateDirectory(AppPathsService.ModelsDir);
        var partPath = destination + ".part";

        try
        {
            await using (var source = await Http.GetStreamAsync(url, ct))
            await using (var target = new FileStream(partPath, new FileStreamOptions
                         {
                             Mode = FileMode.Create,
                             Access = FileAccess.Write,
                             Share = FileShare.None,
                             Options = FileOptions.Asynchronous
                         }))
            {
                await source.CopyToAsync(target, ct);
            }

            File.Move(partPath, destination, overwrite: true);
        }
        catch
        {
            TryDelete(partPath);
            throw;
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch
        {
            // ignored
        }
    }
}