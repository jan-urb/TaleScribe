using Microsoft.EntityFrameworkCore;
using TaleScribe.Models;
using TaleScribe.Services;

namespace TaleScribe.Context;

public class AppDbContext: DbContext
{
    public DbSet<SpeechRecognitionResult> Results => Set<SpeechRecognitionResult>();

    public static string DbPath => AppPathsService.DatabaseFile;

    protected override void OnConfiguring(DbContextOptionsBuilder options)
    {
        AppPathsService.EnsureCreated();
        options.UseSqlite($"Data Source={DbPath}");
    }
}