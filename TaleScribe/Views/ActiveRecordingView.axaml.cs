using System;
using System.ComponentModel;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using TaleScribe.ViewModels;

namespace TaleScribe.Views;

public partial class ActiveRecordingView : UserControl
{
    private readonly Stopwatch _elapsed = new();
    private readonly DispatcherTimer _tick = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private RecordingFlowViewModel? _viewModel;

    public ActiveRecordingView()
    {
        InitializeComponent();
        _tick.Tick += (_, _) => ShowElapsed();
    }
    
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property != IsVisibleProperty) return;

        if (change.GetNewValue<bool>())
        {
            // A recorder that is still paused keeps the clock at zero until it resumes.
            if (_viewModel?.IsPaused == true) _elapsed.Reset();
            else _elapsed.Restart();

            ShowElapsed();
            _tick.Start();
        }
        else
        {
            _tick.Stop();
            _elapsed.Reset();
        }
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        Watch(DataContext as RecordingFlowViewModel);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Watch(DataContext as RecordingFlowViewModel);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _tick.Stop();
        Watch(null);
    }

    private void Watch(RecordingFlowViewModel? viewModel)
    {
        if (ReferenceEquals(_viewModel, viewModel)) return;

        if (_viewModel is not null) _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _viewModel = viewModel;
        if (_viewModel is not null) _viewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(RecordingFlowViewModel.IsPaused) || _viewModel is null) return;

        if (_viewModel.IsPaused)
        {
            _elapsed.Stop();
            ShowElapsed();
        }
        else if (IsVisible)
        {
            _elapsed.Start();
        }
    }

    private void ShowElapsed()
    {
        // Can run before InitializeComponent has created the named element.
        if (ElapsedText is null) return;

        var time = _elapsed.Elapsed;
        ElapsedText.Text = time.TotalHours >= 1 ? time.ToString(@"h\:mm\:ss") : time.ToString(@"m\:ss");
    }
}