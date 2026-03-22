using openMob.Core.ViewModels;
using UXDivers.Popups.Maui;

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
}
