using System.Windows.Input;

namespace openMob.Views.Controls;

/// <summary>
/// Suggestion chip component displayed when the chat is empty.
/// Shows a title and subtitle, and triggers a command when tapped.
/// </summary>
public partial class SuggestionChipView : ContentView
{
    /// <summary>Bindable property for the chip title.</summary>
    public static readonly BindableProperty TitleProperty =
        BindableProperty.Create(nameof(Title), typeof(string), typeof(SuggestionChipView), string.Empty);

    /// <summary>Bindable property for the chip subtitle.</summary>
    public static readonly BindableProperty SubtitleProperty =
        BindableProperty.Create(nameof(Subtitle), typeof(string), typeof(SuggestionChipView), string.Empty);

    /// <summary>Bindable property for the tap command.</summary>
    public static readonly BindableProperty TapCommandProperty =
        BindableProperty.Create(nameof(TapCommand), typeof(ICommand), typeof(SuggestionChipView));

    /// <summary>Bindable property for the command parameter.</summary>
    public static readonly BindableProperty CommandParameterProperty =
        BindableProperty.Create(nameof(CommandParameter), typeof(object), typeof(SuggestionChipView));

    /// <summary>Initialises the suggestion chip view.</summary>
    public SuggestionChipView()
    {
        InitializeComponent();
    }

    /// <summary>Gets or sets the chip title text.</summary>
    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <summary>Gets or sets the chip subtitle text.</summary>
    public string Subtitle
    {
        get => (string)GetValue(SubtitleProperty);
        set => SetValue(SubtitleProperty, value);
    }

    /// <summary>Gets or sets the command executed when the chip is tapped.</summary>
    public ICommand? TapCommand
    {
        get => (ICommand?)GetValue(TapCommandProperty);
        set => SetValue(TapCommandProperty, value);
    }

    /// <summary>Gets or sets the parameter passed to the tap command.</summary>
    public object? CommandParameter
    {
        get => GetValue(CommandParameterProperty);
        set => SetValue(CommandParameterProperty, value);
    }
}
