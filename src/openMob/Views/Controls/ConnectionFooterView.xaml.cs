namespace openMob.Views.Controls;

/// <summary>
/// Always-visible footer at the bottom of ChatPage that displays the active server name
/// and a traffic-light health indicator dot driven by <c>ChatViewModel.ConnectionHealthState</c>.
/// Binds directly to <c>ChatViewModel</c> via <c>x:DataType</c> compiled bindings —
/// no BindableProperties are exposed.
/// </summary>
public partial class ConnectionFooterView : ContentView
{
    /// <summary>Initialises the connection footer view.</summary>
    public ConnectionFooterView()
    {
        InitializeComponent();
    }
}
