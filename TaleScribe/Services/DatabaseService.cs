using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using TaleScribe.Context;
using TaleScribe.Models;

namespace TaleScribe.Services;

/// <summary>
///     Reads and writes saved recordings in the SQLite database: stores a finished transcript,
///     lists recordings for the Recordings page, and loads one with its transcript turns.
/// </summary>
public class DatabaseService
{
    public async Task<int> AddRecordingAsync(List<CombinedResult> combinedResult, string path, string title)
    {
        await using var db = new AppDbContext();

        var recording = new SpeechRecognitionResult
        {
            Title = title,
            Path = path,
            Combined = combinedResult
        };

        db.Results.Add(recording);
        await db.SaveChangesAsync();
        
        return recording.Id;
    }
    
    public async Task<List<RecordingSummary>> GetRecordsAsync()
    {
        await using var db = new AppDbContext();

        return await db.Results
            .AsNoTracking()
            .OrderByDescending(record => record.CreatedAt)
            .Select(record => new RecordingSummary(record.Id, record.Title, record.CreatedAt))
            .ToListAsync();
    }
    
    public async Task<SpeechRecognitionResult?> GetRecordDetailsAsync(int id)
    {
        await using var db = new AppDbContext();
        return await db.Results
            .AsNoTracking()
            .Include(record => record.Combined.OrderBy(turn => turn.T0))
            .FirstOrDefaultAsync(record => record.Id == id);
    }
}