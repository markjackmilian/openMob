using openMob.Core.ViewModels;

namespace openMob.Views.Controls;

/// <summary>Custom flyout body with session list for the current project.</summary>
public partial class FlyoutContentView : ContentView
{
    /// <summary>Initialises the flyout content view.</summary>
    public FlyoutContentView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object? sender, EventArgs e)
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

        if (BindingContext is FlyoutViewModel flyoutVm)
        {
            await flyoutVm.LoadSessionsCommand.ExecuteAsync(null);
        }
    }
}
