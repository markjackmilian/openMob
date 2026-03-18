using openMob.Core.ViewModels;
using openMob.Views.Controls;

namespace openMob.Views.Pages;

/// <summary>
/// Chat page — root screen of the app. Shows a custom topbar with model indicator,
/// status banners, a message CollectionView with MessageBubbleView items,
/// suggestion chips, an InputBarView, and an error banner.
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

        UpdateBubbleMaxWidth();
    }

    /// <inheritdoc />
    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);
        UpdateBubbleMaxWidth();
    }

    /// <summary>Opens the Shell flyout when the hamburger button is tapped.</summary>
    private void OnHamburgerClicked(object? sender, EventArgs e)
    {
        Shell.Current.FlyoutIsPresented = true;
    }

    /// <summary>
    /// Calculates 80% of the screen width and sets it as the BubbleMaxWidth
    /// on all MessageBubbleView items in the CollectionView.
    /// This is purely visual state management — no business logic.
    /// </summary>
    private void UpdateBubbleMaxWidth()
    {
        var screenWidth = DeviceDisplay.MainDisplayInfo.Width / DeviceDisplay.MainDisplayInfo.Density;
        var maxWidth = screenWidth * 0.80;

        if (maxWidth <= 0)
            return;

        // Set the max width on the CollectionView's item template via a page-level resource
        // that MessageBubbleView can reference. Since we can't easily bind to a page property
        // from a DataTemplate, we iterate visible items after layout.
        if (MessagesCollectionView?.GetVisualTreeDescendants() is { } descendants)
        {
            foreach (var descendant in descendants)
            {
                if (descendant is MessageBubbleView bubble)
                {
                    bubble.BubbleMaxWidth = maxWidth;
                }
            }
        }
    }
}
