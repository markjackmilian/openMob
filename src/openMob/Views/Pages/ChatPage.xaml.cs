using System.ComponentModel;
using Microsoft.Maui.Controls.Shapes;
using openMob.Core.ViewModels;

namespace openMob.Views.Pages;

/// <summary>
/// Chat page — root screen of the app. Shows a redesigned header with session name,
/// context status bar, status banners, a message CollectionView with MessageBlockView items,
/// subagent indicator, suggestion chips, an InputBarView, and an error banner.
/// </summary>
public partial class ChatPage : ContentPage, IQueryAttributable
{
    private double _lastScrollY;

    /// <summary>Cancellation token source for the typing indicator dot animation.</summary>
    private CancellationTokenSource? _typingAnimationCts;

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
            // Subscribe to ViewModel property changes for animated transitions (REQ-022)
            vm.PropertyChanged += OnViewModelPropertyChanged;

            await vm.LoadContextCommand.ExecuteAsync(null);

            // If no session was set via navigation, create one so the user can chat immediately
            if (vm.CurrentSessionId is null)
            {
                await vm.NewChatCommand.ExecuteAsync(null);
            }

            // Sync typing indicator in case IsAiResponding was already true on page re-entry
            if (vm.IsAiResponding)
                StartTypingAnimation();
        }
    }

    /// <inheritdoc />
    protected override void OnDisappearing()
    {
        base.OnDisappearing();

        if (BindingContext is ChatViewModel vm)
        {
            vm.PropertyChanged -= OnViewModelPropertyChanged;
        }

        StopTypingAnimation();
    }

    /// <summary>Opens the Shell flyout when the hamburger button is tapped.</summary>
    private void OnHamburgerClicked(object? sender, EventArgs e)
    {
        Shell.Current.FlyoutIsPresented = true;
    }

    /// <summary>
    /// Handles the CollectionView Scrolled event to detect scroll direction
    /// and show/hide the context status bar (REQ-022).
    /// </summary>
    private void OnMessagesScrolled(object? sender, ItemsViewScrolledEventArgs e)
    {
        var isScrollingDown = e.VerticalOffset > _lastScrollY;
        _lastScrollY = e.VerticalOffset;

        if (BindingContext is ChatViewModel vm)
        {
            vm.OnScrollDirectionChanged(isScrollingDown);
        }
    }

    /// <summary>
    /// Handles ViewModel property changes to animate the context status bar
    /// collapse/expand with a 150ms fade transition (REQ-022), and to start/stop
    /// the typing indicator dot animation when <see cref="ChatViewModel.IsAiResponding"/> changes.
    /// </summary>
    private async void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ChatViewModel.IsContextBarVisible) && sender is ChatViewModel vm)
        {
            if (vm.IsContextBarVisible)
            {
                ContextStatusBar.IsVisible = true;
                await ContextStatusBar.FadeTo(1, 150, Easing.CubicOut);
            }
            else
            {
                await ContextStatusBar.FadeTo(0, 150, Easing.CubicIn);
                ContextStatusBar.IsVisible = false;
            }
        }
        else if (e.PropertyName == nameof(ChatViewModel.IsAiResponding) && sender is ChatViewModel respondingVm)
        {
            if (respondingVm.IsAiResponding)
                StartTypingAnimation();
            else
                StopTypingAnimation();
        }
    }

    // ─── Typing indicator animation ───────────────────────────────────────────

    /// <summary>
    /// Starts the pulsing dot animation for the typing indicator.
    /// Three dots animate opacity 0.3 → 1.0 → 0.3 with staggered delays (0ms, 200ms, 400ms).
    /// </summary>
    private void StartTypingAnimation()
    {
        StopTypingAnimation();
        _typingAnimationCts = new CancellationTokenSource();
        var ct = _typingAnimationCts.Token;

        _ = AnimateTypingDotAsync(TypingDot1, 0, ct);
        _ = AnimateTypingDotAsync(TypingDot2, 200, ct);
        _ = AnimateTypingDotAsync(TypingDot3, 400, ct);
    }

    /// <summary>Stops the typing indicator animation and resets dot opacity.</summary>
    private void StopTypingAnimation()
    {
        _typingAnimationCts?.Cancel();
        _typingAnimationCts?.Dispose();
        _typingAnimationCts = null;

        // Reset dot opacity to resting state
        TypingDot1.Opacity = 0.3;
        TypingDot2.Opacity = 0.3;
        TypingDot3.Opacity = 0.3;
    }

    /// <summary>
    /// Animates a single typing indicator dot in a continuous opacity loop.
    /// </summary>
    /// <param name="dot">The ellipse to animate.</param>
    /// <param name="delayMs">Initial delay before starting the animation loop.</param>
    /// <param name="ct">Cancellation token to stop the animation.</param>
    private static async Task AnimateTypingDotAsync(Ellipse dot, int delayMs, CancellationToken ct)
    {
        try
        {
            if (delayMs > 0)
                await Task.Delay(delayMs, ct);

            while (!ct.IsCancellationRequested)
            {
                await dot.FadeToAsync(1.0, 300, Easing.CubicOut);
                await dot.FadeToAsync(0.3, 300, Easing.CubicIn);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when animation is cancelled — exit cleanly.
        }
        catch (ObjectDisposedException)
        {
            // Expected when the view is disposed during animation.
        }
    }
}
