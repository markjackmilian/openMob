namespace openMob.Views.Controls;

/// <summary>
/// Empty state view displayed when the chat has no messages.
/// Shows an icon, title, and optional subtitle centered in the message area.
/// </summary>
public partial class EmptyStateView : ContentView
{
    /// <summary>Bindable property for the empty state title.</summary>
    public static readonly BindableProperty TitleProperty =
        BindableProperty.Create(nameof(Title), typeof(string), typeof(EmptyStateView), "How can I help you?");

    /// <summary>Bindable property for the empty state subtitle.</summary>
    public static readonly BindableProperty SubtitleProperty =
        BindableProperty.Create(nameof(Subtitle), typeof(string), typeof(EmptyStateView), string.Empty);

    /// <summary>Initialises the empty state view.</summary>
    public EmptyStateView()
    {
        InitializeComponent();
    }

    /// <summary>Gets or sets the title text.</summary>
    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <summary>Gets or sets the subtitle text.</summary>
    public string Subtitle
    {
        get => (string)GetValue(SubtitleProperty);
        set => SetValue(SubtitleProperty, value);
    }
}
