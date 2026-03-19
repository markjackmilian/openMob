namespace openMob.Views.Controls;

/// <summary>
/// Subagent activity indicator component (REQ-032). Shows the active subagent name
/// with a pulsing icon animation when the subagent is working.
/// </summary>
public partial class SubagentIndicatorView : ContentView
{
    private CancellationTokenSource? _pulseCts;

    /// <summary>Bindable property for the subagent display name.</summary>
    public static readonly BindableProperty SubagentNameProperty =
        BindableProperty.Create(nameof(SubagentName), typeof(string), typeof(SubagentIndicatorView), "Subagent");

    /// <summary>Bindable property indicating whether the subagent is actively working.</summary>
    public static readonly BindableProperty IsActiveProperty =
        BindableProperty.Create(nameof(IsActive), typeof(bool), typeof(SubagentIndicatorView), false,
            propertyChanged: OnIsActiveChanged);

    /// <summary>Bindable property for the status text (e.g. "Working...", "Completed").</summary>
    public static readonly BindableProperty StatusTextProperty =
        BindableProperty.Create(nameof(StatusText), typeof(string), typeof(SubagentIndicatorView), "Working...");

    /// <summary>Initialises the subagent indicator view.</summary>
    public SubagentIndicatorView()
    {
        InitializeComponent();
    }

    /// <summary>Gets or sets the subagent display name.</summary>
    public string SubagentName
    {
        get => (string)GetValue(SubagentNameProperty);
        set => SetValue(SubagentNameProperty, value);
    }

    /// <summary>Gets or sets whether the subagent is actively working.</summary>
    public bool IsActive
    {
        get => (bool)GetValue(IsActiveProperty);
        set => SetValue(IsActiveProperty, value);
    }

    /// <summary>Gets or sets the status text.</summary>
    public string StatusText
    {
        get => (string)GetValue(StatusTextProperty);
        set => SetValue(StatusTextProperty, value);
    }

    private static void OnIsActiveChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is SubagentIndicatorView view)
        {
            if (newValue is true)
            {
                view.StartPulseAnimation();
            }
            else
            {
                view.StopPulseAnimation();
            }
        }
    }

    /// <summary>
    /// Starts the pulsing opacity animation on the subagent icon.
    /// Opacity cycles 0.3 -> 1.0 -> 0.3 continuously.
    /// </summary>
    private void StartPulseAnimation()
    {
        StopPulseAnimation();
        _pulseCts = new CancellationTokenSource();
        var ct = _pulseCts.Token;

        _ = PulseLoopAsync(ct);
    }

    /// <summary>Stops the pulse animation and resets icon opacity.</summary>
    private void StopPulseAnimation()
    {
        _pulseCts?.Cancel();
        _pulseCts?.Dispose();
        _pulseCts = null;

        SubagentIcon.Opacity = 1.0;
    }

    /// <summary>Runs the continuous pulse animation loop.</summary>
    /// <param name="ct">Cancellation token to stop the animation.</param>
    private async Task PulseLoopAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                await SubagentIcon.FadeToAsync(0.3, 400, Easing.CubicIn);
                await SubagentIcon.FadeToAsync(1.0, 400, Easing.CubicOut);
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
