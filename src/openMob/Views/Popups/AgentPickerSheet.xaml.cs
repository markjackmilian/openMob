using openMob.Core.ViewModels;
using UXDivers.Popups.Maui;
using UXDivers.Popups.Services;

namespace openMob.Views.Popups;

/// <summary>
/// Agent picker popup — displays available agents for selection.
/// Agent loading is handled by MauiPopupService before this popup is pushed.
/// </summary>
public partial class AgentPickerSheet : PopupPage
{
    /// <summary>Initialises the agent picker sheet with its ViewModel.</summary>
    /// <param name="viewModel">The agent picker ViewModel.</param>
    public AgentPickerSheet(AgentPickerViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    /// <summary>Loads agents when the popup is navigated to.</summary>
    public override void OnNavigatedTo(IReadOnlyDictionary<string, object?> parameters)
    {
        base.OnNavigatedTo(parameters);

        if (BindingContext is AgentPickerViewModel vm && vm.LoadAgentsCommand.CanExecute(null))
        {
            _ = vm.LoadAgentsCommand.ExecuteAsync(null);
        }
    }

    /// <summary>Closes the popup when the close button is tapped.</summary>
    private async void OnCloseButtonTapped(object? sender, EventArgs e)
    {
        await IPopupService.Current.PopAsync(this);
    }
}
