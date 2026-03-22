using openMob.Core.ViewModels;
using UXDivers.Popups.Maui;
using UXDivers.Popups.Services;

namespace openMob.Views.Popups;

/// <summary>
/// Command Palette popup — displays a searchable list of commands
/// loaded from the opencode server.
/// Command loading is handled by MauiPopupService before this popup is pushed.
/// </summary>
public partial class CommandPaletteSheet : PopupPage
{
    /// <summary>Initialises the command palette sheet with its ViewModel.</summary>
    /// <param name="viewModel">The command palette ViewModel.</param>
    public CommandPaletteSheet(CommandPaletteViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    /// <summary>Closes the popup when the close button is tapped.</summary>
    private async void OnCloseButtonTapped(object? sender, EventArgs e)
    {
        await IPopupService.Current.PopAsync(this);
    }
}
