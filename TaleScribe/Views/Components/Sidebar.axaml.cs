using System.Windows.Input;
using Avalonia.Controls;

namespace TaleScribe.Views.Components;

public partial class Sidebar : UserControl
{
    public Sidebar()
    {
        InitializeComponent();
    }

    private void OnNavSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems.Count == 0) return;
        
        var other = ReferenceEquals(sender, NavList) ? BottomNavList : NavList;
        if (other is not null) other.SelectedItem = null;

        if (e.AddedItems[0] is ListBoxItem { Tag: ICommand command } &&
            command.CanExecute(null))
        {
            command.Execute(null);
        }
    }
}
