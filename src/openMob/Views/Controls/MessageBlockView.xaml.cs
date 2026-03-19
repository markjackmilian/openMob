using Microsoft.Maui.Controls.Shapes;
using openMob.Core.Models;

namespace openMob.Views.Controls;

/// <summary>
/// Message block component replacing the bubble layout. Displays messages in a
/// full-width block format with a colored left bar indicating sender type.
/// Supports streaming indicator with pulsing dot animation.
/// </summary>
public partial class MessageBlockView : ContentView
{
    private CancellationTokenSource? _animationCts;

    /// <summary>Bindable property for the message text content.</summary>
    public static readonly BindableProperty TextContentProperty =
        BindableProperty.Create(nameof(TextContent), typeof(string), typeof(MessageBlockView), string.Empty);

    /// <summary>Bindable property indicating whether the message is from the user.</summary>
    public static readonly BindableProperty IsFromUserProperty =
        BindableProperty.Create(nameof(IsFromUser), typeof(bool), typeof(MessageBlockView), false);

    /// <summary>Bindable property for the message timestamp.</summary>
    public static readonly BindableProperty TimestampProperty =
        BindableProperty.Create(nameof(Timestamp), typeof(DateTimeOffset), typeof(MessageBlockView), DateTimeOffset.MinValue);

    /// <summary>Bindable property for the delivery status.</summary>
    public static readonly BindableProperty DeliveryStatusProperty =
        BindableProperty.Create(nameof(DeliveryStatus), typeof(MessageDeliveryStatus), typeof(MessageBlockView), MessageDeliveryStatus.Sending);

    /// <summary>Bindable property indicating whether this is the first message in a consecutive group.</summary>
    public static readonly BindableProperty IsFirstInGroupProperty =
        BindableProperty.Create(nameof(IsFirstInGroup), typeof(bool), typeof(MessageBlockView), true);

    /// <summary>Bindable property indicating whether this is the last message in a consecutive group.</summary>
    public static readonly BindableProperty IsLastInGroupProperty =
        BindableProperty.Create(nameof(IsLastInGroup), typeof(bool), typeof(MessageBlockView), true);

    /// <summary>Bindable property indicating whether the message is currently being streamed.</summary>
    public static readonly BindableProperty IsStreamingProperty =
        BindableProperty.Create(nameof(IsStreaming), typeof(bool), typeof(MessageBlockView), false,
            propertyChanged: OnIsStreamingChanged);

    /// <summary>Bindable property for the sender type (User, Agent, Subagent).</summary>
    public static readonly BindableProperty SenderTypeProperty =
        BindableProperty.Create(nameof(SenderType), typeof(SenderType), typeof(MessageBlockView), SenderType.User);

    /// <summary>Bindable property for the sender display name.</summary>
    public static readonly BindableProperty SenderNameProperty =
        BindableProperty.Create(nameof(SenderName), typeof(string), typeof(MessageBlockView), string.Empty);

    /// <summary>Initialises the message block view.</summary>
    public MessageBlockView()
    {
        InitializeComponent();
    }

    /// <summary>Gets or sets the message text content.</summary>
    public string TextContent
    {
        get => (string)GetValue(TextContentProperty);
        set => SetValue(TextContentProperty, value);
    }

    /// <summary>Gets or sets whether the message is from the user.</summary>
    public bool IsFromUser
    {
        get => (bool)GetValue(IsFromUserProperty);
        set => SetValue(IsFromUserProperty, value);
    }

    /// <summary>Gets or sets the message timestamp.</summary>
    public DateTimeOffset Timestamp
    {
        get => (DateTimeOffset)GetValue(TimestampProperty);
        set => SetValue(TimestampProperty, value);
    }

    /// <summary>Gets or sets the delivery status.</summary>
    public MessageDeliveryStatus DeliveryStatus
    {
        get => (MessageDeliveryStatus)GetValue(DeliveryStatusProperty);
        set => SetValue(DeliveryStatusProperty, value);
    }

    /// <summary>Gets or sets whether this is the first message in a consecutive group.</summary>
    public bool IsFirstInGroup
    {
        get => (bool)GetValue(IsFirstInGroupProperty);
        set => SetValue(IsFirstInGroupProperty, value);
    }

    /// <summary>Gets or sets whether this is the last message in a consecutive group.</summary>
    public bool IsLastInGroup
    {
        get => (bool)GetValue(IsLastInGroupProperty);
        set => SetValue(IsLastInGroupProperty, value);
    }

    /// <summary>Gets or sets whether the message is currently being streamed.</summary>
    public bool IsStreaming
    {
        get => (bool)GetValue(IsStreamingProperty);
        set => SetValue(IsStreamingProperty, value);
    }

    /// <summary>Gets or sets the sender type (User, Agent, Subagent).</summary>
    public SenderType SenderType
    {
        get => (SenderType)GetValue(SenderTypeProperty);
        set => SetValue(SenderTypeProperty, value);
    }

    /// <summary>Gets or sets the sender display name.</summary>
    public string SenderName
    {
        get => (string)GetValue(SenderNameProperty);
        set => SetValue(SenderNameProperty, value);
    }

    private static void OnIsStreamingChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is MessageBlockView view)
        {
            if (newValue is true)
            {
                view.StartStreamingAnimation();
            }
            else
            {
                view.StopStreamingAnimation();
            }
        }
    }

    /// <summary>
    /// Starts the pulsing dot animation for the streaming indicator.
    /// Three dots animate opacity 0.3 -> 1.0 -> 0.3 with staggered delays.
    /// </summary>
    private void StartStreamingAnimation()
    {
        StopStreamingAnimation();
        _animationCts = new CancellationTokenSource();
        var ct = _animationCts.Token;

        _ = AnimateDotLoopAsync(Dot1, 0, ct);
        _ = AnimateDotLoopAsync(Dot2, 200, ct);
        _ = AnimateDotLoopAsync(Dot3, 400, ct);
    }

    /// <summary>Stops the streaming dot animation and resets dot opacity.</summary>
    private void StopStreamingAnimation()
    {
        _animationCts?.Cancel();
        _animationCts?.Dispose();
        _animationCts = null;

        Dot1.Opacity = 0.3;
        Dot2.Opacity = 0.3;
        Dot3.Opacity = 0.3;
    }

    /// <summary>
    /// Animates a single dot in a continuous opacity loop.
    /// </summary>
    /// <param name="dot">The ellipse to animate.</param>
    /// <param name="delayMs">Initial delay before starting the animation.</param>
    /// <param name="ct">Cancellation token to stop the animation.</param>
    private static async Task AnimateDotLoopAsync(Ellipse dot, int delayMs, CancellationToken ct)
    {
        try
        {
            if (delayMs > 0)
            {
                await Task.Delay(delayMs, ct);
            }

            while (!ct.IsCancellationRequested)
            {
                await dot.FadeToAsync(1.0, 300, Easing.CubicOut);
                await dot.FadeToAsync(0.3, 300, Easing.CubicIn);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when animation is cancelled.
        }
        catch (ObjectDisposedException)
        {
            // Expected when the view is disposed during animation.
        }
    }
}
