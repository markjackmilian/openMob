namespace openMob.Views.Controls;

/// <summary>
/// Renders an unhandled SSE event as a visible fallback card in the chat message list.
/// In DEBUG builds, displays the raw event type and JSON payload for diagnostics.
/// In Release builds, displays only a generic user-friendly message.
/// </summary>
public partial class FallbackMessageView : ContentView
{
    /// <summary>Bindable property for the raw SSE event-type string.</summary>
    public static readonly BindableProperty RawTypeProperty =
        BindableProperty.Create(nameof(RawType), typeof(string), typeof(FallbackMessageView), null);

    /// <summary>Bindable property for the pretty-printed JSON payload (DEBUG builds only).</summary>
    public static readonly BindableProperty RawJsonProperty =
        BindableProperty.Create(nameof(RawJson), typeof(string), typeof(FallbackMessageView), null);

    /// <summary>Bindable property indicating whether this is a DEBUG build.</summary>
    public static readonly BindableProperty IsDebugBuildProperty =
        BindableProperty.Create(nameof(IsDebugBuild), typeof(bool), typeof(FallbackMessageView), false);

    /// <summary>
    /// Initialises the fallback message view and sets <see cref="IsDebugBuild"/> from the compile-time constant.
    /// </summary>
    public FallbackMessageView()
    {
        InitializeComponent();
#if DEBUG
        IsDebugBuild = true;
#endif
    }

    /// <summary>Gets or sets the raw SSE event-type string.</summary>
    public string? RawType
    {
        get => (string?)GetValue(RawTypeProperty);
        set => SetValue(RawTypeProperty, value);
    }

    /// <summary>Gets or sets the pretty-printed JSON payload. Null in Release builds.</summary>
    public string? RawJson
    {
        get => (string?)GetValue(RawJsonProperty);
        set => SetValue(RawJsonProperty, value);
    }

    /// <summary>Gets or sets whether this is a DEBUG build. Set once at construction from a compile-time constant.</summary>
    public bool IsDebugBuild
    {
        get => (bool)GetValue(IsDebugBuildProperty);
        set => SetValue(IsDebugBuildProperty, value);
    }
}
