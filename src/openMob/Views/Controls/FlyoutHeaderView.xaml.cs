namespace openMob.Views.Controls;

/// <summary>Flyout header with app logo and new chat button.</summary>
public partial class FlyoutHeaderView : ContentView
{
    /// <summary>Initialises the flyout header view.</summary>
    public FlyoutHeaderView()
    {
        InitializeComponent();
    }

    private async void OnNewChatTapped(object? sender, EventArgs e)
    {
        Shell.Current.FlyoutIsPresented = false;
        // New chat creation will be handled by ChatViewModel when available.
        await Task.CompletedTask;
    }
}
