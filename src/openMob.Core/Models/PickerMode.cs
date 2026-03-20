namespace openMob.Core.Models;

/// <summary>
/// Specifies the operating mode of the agent picker sheet.
/// </summary>
public enum PickerMode
{
    /// <summary>Primary agent selection mode — shows primary-mode agents, prepends "Default" entry.</summary>
    Primary = 0,

    /// <summary>Subagent invocation mode — shows subagent-mode agents only, no "Default" entry.</summary>
    Subagent = 1,
}
