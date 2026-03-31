using UXDivers.Popups.Maui;

namespace openMob.Views.Popups;

/// <summary>
/// Non-dismissible reconnection modal sheet (REQ-006, AC-005).
/// Shown automatically when the connection health state transitions to <c>Lost</c>.
/// The Android back button is consumed to prevent accidental dismissal.
/// </summary>
public partial class ReconnectingModalSheet : PopupPage
{
    /// <summary>Initialises the reconnecting modal sheet.</summary>
    public ReconnectingModalSheet()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Prevents the Android back button from dismissing the modal (AC-005).
    /// </summary>
    /// <returns><c>true</c> to consume the event and keep the modal visible.</returns>
    protected override bool OnBackButtonPressed()
    {
        return true; // consume the event — do not dismiss
    }
}
