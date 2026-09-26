using CommunityToolkit.Mvvm.ComponentModel;

namespace TaleScribe.ViewModels;

/// <summary>
///     Base class for every page's view model. Inheriting ObservableObject is what lets
///     [ObservableProperty] and [RelayCommand] generate properties and commands in the view models.
/// </summary>
public class ViewModelBase : ObservableObject
{
}