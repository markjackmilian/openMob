using openMob.Core.ViewModels;
using UXDivers.Popups.Maui;
using UXDivers.Popups.Services;

namespace openMob.Views.Popups;

/// <summary>
/// Model picker popup — displays a flat, virtualised list of AI models for selection.
/// Model loading is handled by MauiPopupService before this popup is pushed.
/// </summary>
public partial class ModelPickerSheet : PopupPage
{
    /// <summary>Initialises the model picker sheet with its ViewModel.</summary>
    /// <param name="viewModel">The model picker ViewModel.</param>
    public ModelPickerSheet(ModelPickerViewModel viewModel)
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
