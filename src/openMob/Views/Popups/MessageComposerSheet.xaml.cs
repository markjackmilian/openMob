using openMob.Core.ViewModels;
using UXDivers.Popups.Maui;
using UXDivers.Popups.Services;

namespace openMob.Views.Popups;

/// <summary>
/// Message composer popup — provides a large writing area with session controls,
/// picker buttons, and a send action. Replaces the inline InputBarView.
/// ViewModel initialisation is handled by MauiPopupService before this popup is pushed.
/// </summary>
public partial class MessageComposerSheet : PopupPage
{
    /// <summary>Initialises the message composer sheet with its ViewModel.</summary>
    /// <param name="viewModel">The message composer ViewModel.</param>
    public MessageComposerSheet(MessageComposerViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    /// <summary>Closes the popup when the close button is tapped, saving draft first.</summary>
    private async void OnCloseButtonTapped(object? sender, EventArgs e)
    {
        // Save draft before closing
        if (BindingContext is MessageComposerViewModel vm)
        {
            vm.CloseCommand.Execute(null);
            return;
        }

        await IPopupService.Current.PopAsync(this);
    }
}
