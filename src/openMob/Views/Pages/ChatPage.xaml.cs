using openMob.Core.ViewModels;

namespace openMob.Views.Pages;

/// <summary>
/// Chat page — root screen of the app. Shows the header bar with project/session context,
/// status banners, and a placeholder body (full chat UI pending chat-ui-design-guidelines feature).
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

    private void OnHamburgerClicked(object? sender, EventArgs e)
    {
        Shell.Current.FlyoutIsPresented = true;
    }
}
