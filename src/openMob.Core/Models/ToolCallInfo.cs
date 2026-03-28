using CommunityToolkit.Mvvm.ComponentModel;

namespace openMob.Core.Models;

/// <summary>Discriminates the execution state of a tool call.</summary>
public enum ToolCallStatus
{
    /// <summary>The tool call has been queued but not yet started.</summary>
    Pending,

    /// <summary>The tool call is currently executing.</summary>
    Running,

    /// <summary>The tool call completed successfully.</summary>
    Completed,

    /// <summary>The tool call failed with an error.</summary>
    Error,
}

/// <summary>
/// Represents a single tool call invocation within an assistant message.
/// Observable so XAML bindings update as the tool call progresses through states.
/// </summary>
public sealed partial class ToolCallInfo : ObservableObject
{
    /// <summary>Gets the unique part identifier for this tool call.</summary>
    public string PartId { get; }

    /// <summary>Gets the name of the tool being called.</summary>
    public string ToolName { get; }

    /// <summary>Gets or sets the current execution status of this tool call.</summary>
    [ObservableProperty]
    private ToolCallStatus _status;

    /// <summary>Gets or sets the display title of this tool call (populated when running or completed).</summary>
    [ObservableProperty]
    private string? _title;

    /// <summary>Gets or sets the output text of this tool call (populated when completed).</summary>
    [ObservableProperty]
    private string? _output;

    /// <summary>Gets or sets the error message for this tool call (populated when error).</summary>
    [ObservableProperty]
    private string? _errorText;

    /// <summary>Gets or sets the execution duration in milliseconds (populated when completed).</summary>
    [ObservableProperty]
    private long? _durationMs;

    /// <summary>
    /// Initialises a new <see cref="ToolCallInfo"/> with the specified immutable identifiers.
    /// </summary>
    /// <param name="partId">The unique part identifier.</param>
    /// <param name="toolName">The name of the tool being called.</param>
    public ToolCallInfo(string partId, string toolName)
    {
        PartId = partId;
        ToolName = toolName;
    }
}
