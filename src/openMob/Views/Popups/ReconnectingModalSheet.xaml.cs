using UXDivers.Popups.Maui;

namespace openMob.Views.Popups;

/// <summary>
/// Non-dismissible reconnection modal sheet (REQ-006, AC-005).
/// Shown automatically when the connection health state transitions to <c>Lost</c>.
/// Background tap dismissal is disabled via <c>CloseWhenBackgroundIsClicked="False"</c> in XAML.
/// </summary>
public partial class ReconnectingModalSheet : PopupPage
{
    /// <summary>Initialises the reconnecting modal sheet.</summary>
    public ReconnectingModalSheet()
    {
        InitializeComponent();
    }
}
