using openMob.Core.ViewModels;

namespace openMob.Views.Pages;

/// <summary>
/// Chat page — root screen of the app. Shows a custom topbar with model indicator,
/// status banners, an empty-state message area, and a ChatGPT-inspired input bar
/// with mic icon toggle and send button color state.
/// </summary>
public partial class ChatPage : ContentPage
{
    /// <summary>Initialises the chat page with the injected ChatViewModel.</summary>
    /// <param name="viewModel">The ChatViewModel resolved from DI.</param>
    public ChatPage(ChatViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    /// <inheritdoc />
    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (BindingContext is ChatViewModel vm)
        {
            await vm.LoadContextCommand.ExecuteAsync(null);
        }
    }

    /// <summary>Opens the Shell flyout when the hamburger button is tapped.</summary>
    private void OnHamburgerClicked(object? sender, EventArgs e)
    {
        Shell.Current.FlyoutIsPresented = true;
    }

    /// <summary>
    /// Handles text changes in the message editor to update UI state.
    /// Toggles mic icon visibility and send button appearance (active/inactive).
    /// This is purely visual state management — no business logic.
    /// </summary>
    private void OnMessageEditorTextChanged(object? sender, TextChangedEventArgs e)
    {
        var hasText = !string.IsNullOrEmpty(e.NewTextValue);

        // Toggle mic icon: visible when empty, hidden when typing
        MicIcon.IsVisible = !hasText;

        // Toggle send button: primary accent when text present, muted when empty
        if (hasText)
        {
            SendButton.SetAppThemeColor(
                Border.BackgroundColorProperty,
                (Color)Application.Current!.Resources["ColorPrimaryLight"],
                (Color)Application.Current!.Resources["ColorPrimaryDark"]);

            SendIcon.SetAppThemeColor(
                Label.TextColorProperty,
                (Color)Application.Current!.Resources["ColorOnPrimaryLight"],
                (Color)Application.Current!.Resources["ColorOnPrimaryDark"]);
        }
        else
        {
            SendButton.SetAppThemeColor(
                Border.BackgroundColorProperty,
                (Color)Application.Current!.Resources["ColorSurfaceSecondaryLight"],
                (Color)Application.Current!.Resources["ColorSurfaceSecondaryDark"]);

            SendIcon.SetAppThemeColor(
                Label.TextColorProperty,
                (Color)Application.Current!.Resources["ColorOnBackgroundTertiaryLight"],
                (Color)Application.Current!.Resources["ColorOnBackgroundTertiaryDark"]);
        }
    }
}
