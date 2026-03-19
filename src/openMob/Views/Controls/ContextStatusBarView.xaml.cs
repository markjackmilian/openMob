using System.Windows.Input;

namespace openMob.Views.Controls;

/// <summary>
/// Context status bar component displaying project name, model name, and thinking level
/// in a compact, tappable bar below the header. Collapses on scroll down (REQ-022).
/// Implements priority-based hiding (REQ-020): when horizontal space is insufficient,
/// thinking level is hidden first, then model name; project name is always visible.
/// </summary>
public partial class ContextStatusBarView : ContentView
{
    /// <summary>Width threshold below which the thinking level is hidden.</summary>
    private const double HideThinkingThreshold = 300;

    /// <summary>Width threshold below which the model name is also hidden.</summary>
    private const double HideModelThreshold = 200;

    /// <summary>Bindable property for the project name.</summary>
    public static readonly BindableProperty ProjectNameProperty =
        BindableProperty.Create(nameof(ProjectName), typeof(string), typeof(ContextStatusBarView), "No project");

    /// <summary>Bindable property for the model name.</summary>
    public static readonly BindableProperty ModelNameProperty =
        BindableProperty.Create(nameof(ModelName), typeof(string), typeof(ContextStatusBarView), "No model");

    /// <summary>Bindable property for the thinking level display text.</summary>
    public static readonly BindableProperty ThinkingLevelProperty =
        BindableProperty.Create(nameof(ThinkingLevel), typeof(string), typeof(ContextStatusBarView), "Medium");

    /// <summary>Bindable property for the tap command that opens the Context Sheet.</summary>
    public static readonly BindableProperty TapCommandProperty =
        BindableProperty.Create(nameof(TapCommand), typeof(ICommand), typeof(ContextStatusBarView));

    /// <summary>Initialises the context status bar view.</summary>
    public ContextStatusBarView()
    {
        InitializeComponent();
    }

    /// <summary>Gets or sets the project name.</summary>
    public string ProjectName
    {
        get => (string)GetValue(ProjectNameProperty);
        set => SetValue(ProjectNameProperty, value);
    }

    /// <summary>Gets or sets the model name.</summary>
    public string ModelName
    {
        get => (string)GetValue(ModelNameProperty);
        set => SetValue(ModelNameProperty, value);
    }

    /// <summary>Gets or sets the thinking level display text.</summary>
    public string ThinkingLevel
    {
        get => (string)GetValue(ThinkingLevelProperty);
        set => SetValue(ThinkingLevelProperty, value);
    }

    /// <summary>Gets or sets the command executed when the bar is tapped.</summary>
    public ICommand? TapCommand
    {
        get => (ICommand?)GetValue(TapCommandProperty);
        set => SetValue(TapCommandProperty, value);
    }

    /// <summary>
    /// Handles the root grid's SizeChanged event to implement priority-based content hiding (REQ-020).
    /// When width is below <see cref="HideThinkingThreshold"/>, thinking level is hidden first.
    /// When width is below <see cref="HideModelThreshold"/>, model name is also hidden.
    /// Project name is always visible.
    /// </summary>
    private void OnRootGridSizeChanged(object? sender, EventArgs e)
    {
        if (sender is not Grid grid)
            return;

        var width = grid.Width;

        // Priority: thinking level hidden first, then model name
        var showThinking = width >= HideThinkingThreshold;
        var showModel = width >= HideModelThreshold;

        ThinkingLabel.IsVisible = showThinking;
        ThinkingSeparator.IsVisible = showThinking;
        ModelLabel.IsVisible = showModel;
        ModelSeparator.IsVisible = showModel;
    }
}
