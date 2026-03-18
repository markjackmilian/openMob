using openMob.Core.ViewModels;

namespace openMob.Views.Controls;

/// <summary>
/// Flyout header with app logo and new chat button.
/// Resolves <see cref="FlyoutViewModel"/> from DI on load to enable command binding.
/// </summary>
public partial class FlyoutHeaderView : ContentView
{
    /// <summary>Initialises the flyout header view.</summary>
    public FlyoutHeaderView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object? sender, EventArgs e)
    {
        // Resolve FlyoutViewModel from DI if not already set.
        if (BindingContext is not FlyoutViewModel)
        {
            var vm = Application.Current?.Handler?.MauiContext?.Services.GetService<FlyoutViewModel>();
            if (vm is not null)
            {
                BindingContext = vm;
            }
        }
    }
}
