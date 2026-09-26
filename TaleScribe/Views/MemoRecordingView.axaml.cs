using Avalonia;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using TaleScribe.ViewModels;

namespace TaleScribe.Views;

public partial class MemoRecordingView : UserControl
{
    public MemoRecordingView()
    {
        InitializeComponent();

        MemoButton.PropertyChanged += (sender, e) =>
        {
            if (e.Property == Button.IsPressedProperty && DataContext is MemoViewModel memoViewModel)
            {
                var isPressed = e.GetNewValue<bool>();
                
                if (isPressed)
                {
                    if (memoViewModel.StartCommand.CanExecute(null))
                        memoViewModel.StartCommand.Execute(null);
                }
                else
                {
                    if (memoViewModel.StopCommand.CanExecute(null))
                        memoViewModel.StopCommand.Execute(null);
                }
            }
        };
    }
    
    private async void CopyButton_Click(object? sender, RoutedEventArgs e)
    {
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard is null) return;

        await clipboard.SetTextAsync(MemoBlock.Text ?? string.Empty);
    }
}