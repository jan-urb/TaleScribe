using System;
using System.Diagnostics;
using System.IO;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.EntityFrameworkCore;
using TaleScribe.Context;
using TaleScribe.Models;
using TaleScribe.Services;
using TaleScribe.ViewModels;
using TaleScribe.Views;

namespace TaleScribe;

public partial class App : Application
{
    
    
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }
    
    public override void OnFrameworkInitializationCompleted()
    {
        // A models folder the user picked earlier wins over the default, and must be applied
        // before anything creates or reads that folder.
        var setting = SettingsService.Load();
        if (!string.IsNullOrWhiteSpace(setting.ModelDirectory))
            AppPathsService.ChangeModelDirectory(setting.ModelDirectory);

        AppPathsService.EnsureCreated();

        try
        {
            using var db = new AppDbContext();
            db.Database.Migrate();
        }
        catch (Exception ex)
        {
            Trace.TraceError($"Database migration failed: {ex}");
            throw;
        }
        
        
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainViewModel(),
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}