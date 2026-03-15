namespace openMob.Views.Pages;

/// <summary>
/// Chat page — root screen of the app. Shows the header bar with project/session context,
/// status banners, and a placeholder body (full chat UI pending chat-ui-design-guidelines feature).
/// </summary>
public partial class ChatPage : ContentPage
{
    /// <summary>Initialises the chat page.</summary>
    public ChatPage()
    {
        InitializeComponent();
    }

    private void OnHamburgerClicked(object? sender, EventArgs e)
    {
        Shell.Current.FlyoutIsPresented = true;
    }

    private async void OnProjectNameTapped(object? sender, EventArgs e)
    {
        // ProjectSwitcherSheet will be pushed via IAppPopupService when fully integrated.
        // For now, this is a placeholder.
        await Task.CompletedTask;
    }

    private async void OnNewChatClicked(object? sender, EventArgs e)
    {
        // New chat creation will be handled by ChatViewModel when available.
        await Task.CompletedTask;
    }

    private async void OnMoreMenuClicked(object? sender, EventArgs e)
    {
        // More menu will be shown via IAppPopupService when fully integrated.
        var action = await DisplayActionSheet("Options", "Cancel", null,
            "Rename Session", "Change Agent", "Change Model", "Fork Session", "Archive", "Delete");

        // Actions will be routed to ChatViewModel commands when available.
        _ = action;
    }
}
