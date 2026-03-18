using System.Windows.Input;

namespace openMob.Views.Controls;

/// <summary>
/// Chat input bar component with text editor, send button, mic placeholder, and attach placeholder.
/// The send button is visible when text is present; the mic icon is visible when text is empty.
/// </summary>
public partial class InputBarView : ContentView
{
    /// <summary>Bindable property for the input text (TwoWay).</summary>
    public static readonly BindableProperty TextProperty =
        BindableProperty.Create(nameof(Text), typeof(string), typeof(InputBarView), string.Empty,
            BindingMode.TwoWay, propertyChanged: OnTextChanged);

    /// <summary>Bindable property for the send command.</summary>
    public static readonly BindableProperty SendCommandProperty =
        BindableProperty.Create(nameof(SendCommand), typeof(ICommand), typeof(InputBarView));

    /// <summary>Bindable property for the input enabled state. Controls whether the editor and buttons are interactive.</summary>
    public static readonly BindableProperty IsInputEnabledProperty =
        BindableProperty.Create(nameof(IsInputEnabled), typeof(bool), typeof(InputBarView), true);

    /// <summary>Bindable property for the placeholder text.</summary>
    public static readonly BindableProperty PlaceholderProperty =
        BindableProperty.Create(nameof(Placeholder), typeof(string), typeof(InputBarView), "Message...");

    /// <summary>Computed property indicating whether the text field has content.</summary>
    public static readonly BindableProperty HasTextProperty =
        BindableProperty.Create(nameof(HasText), typeof(bool), typeof(InputBarView), false);

    /// <summary>Initialises the input bar view.</summary>
    public InputBarView()
    {
        InitializeComponent();
    }

    /// <summary>Gets or sets the current input text.</summary>
    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    /// <summary>Gets or sets the command executed when the send button is tapped.</summary>
    public ICommand? SendCommand
    {
        get => (ICommand?)GetValue(SendCommandProperty);
        set => SetValue(SendCommandProperty, value);
    }

    /// <summary>Gets or sets whether the input controls (editor and buttons) are enabled.</summary>
    public bool IsInputEnabled
    {
        get => (bool)GetValue(IsInputEnabledProperty);
        set => SetValue(IsInputEnabledProperty, value);
    }

    /// <summary>Gets or sets the placeholder text.</summary>
    public string Placeholder
    {
        get => (string)GetValue(PlaceholderProperty);
        set => SetValue(PlaceholderProperty, value);
    }

    /// <summary>Gets whether the text field has content (computed from Text).</summary>
    public bool HasText
    {
        get => (bool)GetValue(HasTextProperty);
        private set => SetValue(HasTextProperty, value);
    }

    private static void OnTextChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is InputBarView view)
        {
            view.HasText = !string.IsNullOrEmpty(newValue as string);
        }
    }
}
