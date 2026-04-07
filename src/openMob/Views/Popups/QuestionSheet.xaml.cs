using UXDivers.Popups.Maui;
using UXDivers.Popups.Services;

namespace openMob.Views.Popups;

/// <summary>
/// Bottom sheet popup for answering AI questions.
/// Displays the full question text, option chips, and optional free-text input.
/// BindingContext is set by <see cref="Services.MauiPopupService.ShowQuestionSheetAsync"/>.
/// </summary>
public partial class QuestionSheet : PopupPage
{
    /// <summary>Initialises the question sheet.</summary>
    public QuestionSheet()
    {
        InitializeComponent();
    }

    /// <summary>Closes the popup when the close button is tapped.</summary>
    private async void OnCloseButtonTapped(object? sender, EventArgs e)
    {
        await IPopupService.Current.PopAsync(this);
    }
}
