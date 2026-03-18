using openMob.Core.ViewModels;

namespace openMob.Views.Pages;

/// <summary>
/// Chat page — root screen of the app. Shows a custom topbar with model indicator,
/// status banners, a message CollectionView with MessageBubbleView items,
/// suggestion chips, an InputBarView, and an error banner.
/// </summary>
public partial class ChatPage : ContentPage, IQueryAttributable
{
    /// <summary>Bindable property for the maximum bubble width in absolute points.</summary>
    public static readonly BindableProperty BubbleMaxWidthProperty =
        BindableProperty.Create(nameof(BubbleMaxWidth), typeof(double), typeof(ChatPage), 300.0);

    /// <summary>Gets or sets the maximum bubble width. Bound by MessageBubbleView items in the DataTemplate.</summary>
    public double BubbleMaxWidth
    {
        get => (double)GetValue(BubbleMaxWidthProperty);
        set => SetValue(BubbleMaxWidthProperty, value);
    }

    /// <summary>Initialises the chat page with the injected ChatViewModel.</summary>
    /// <param name="viewModel">The ChatViewModel resolved from DI.</param>
    public ChatPage(ChatViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    /// <inheritdoc />
    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("sessionId", out var value) &&
            value is string sessionId &&
            !string.IsNullOrWhiteSpace(sessionId) &&
            BindingContext is ChatViewModel vm)
        {
            vm.SetSession(sessionId);
        }
    }

    /// <inheritdoc />
    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (BindingContext is ChatViewModel vm)
        {
            await vm.LoadContextCommand.ExecuteAsync(null);

            // If no session was set via navigation, create one so the user can chat immediately
            if (vm.CurrentSessionId is null)
            {
                await vm.NewChatCommand.ExecuteAsync(null);
            }
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
    /// page-level property. MessageBubbleView items bind to this via x:Reference.
    /// This is purely visual state management — no business logic.
    /// </summary>
    private void UpdateBubbleMaxWidth()
    {
        var screenWidth = DeviceDisplay.MainDisplayInfo.Width / DeviceDisplay.MainDisplayInfo.Density;
        var calculatedWidth = screenWidth * 0.80;

        if (calculatedWidth <= 0)
            return;

        BubbleMaxWidth = calculatedWidth;
    }
}
