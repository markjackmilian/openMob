using System.Collections.ObjectModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using openMob.Core.Infrastructure.Http.Dtos.Opencode;

namespace openMob.Core.Models;

/// <summary>Discriminates the kind of chat message.</summary>
public enum MessageKind
{
    /// <summary>Standard chat content.</summary>
    Standard,

    /// <summary>An inline permission request card.</summary>
    PermissionRequest,
}

/// <summary>Tracks the approval state of a permission request card.</summary>
public enum PermissionStatus
{
    /// <summary>The request is waiting for user action.</summary>
    Pending,

    /// <summary>The request has been resolved by the user.</summary>
    Resolved,
}

/// <summary>
/// Domain model for a chat message displayed in the UI.
/// Inherits from <see cref="ObservableObject"/> because several properties
/// (<see cref="TextContent"/>, <see cref="DeliveryStatus"/>, <see cref="IsStreaming"/>,
/// <see cref="IsFirstInGroup"/>, <see cref="IsLastInGroup"/>) are mutated after creation
/// (by SSE streaming and grouping recalculation) and must notify the UI via
/// <see cref="System.ComponentModel.INotifyPropertyChanged"/>.
/// </summary>
public sealed partial class ChatMessage : ObservableObject
{
    /// <summary>Gets the unique message identifier.</summary>
    public string Id { get; }

    /// <summary>Gets the ID of the session this message belongs to.</summary>
    public string SessionId { get; }

    /// <summary>Gets a value indicating whether this message was sent by the user.</summary>
    public bool IsFromUser { get; }

    /// <summary>Gets the timestamp when this message was created.</summary>
    public DateTimeOffset Timestamp { get; }

    /// <summary>Gets or sets the text content of the message, updated by SSE streaming.</summary>
    [ObservableProperty]
    private string _textContent = string.Empty;

    /// <summary>Gets or sets the delivery status of this message.</summary>
    [ObservableProperty]
    private MessageDeliveryStatus _deliveryStatus;

    /// <summary>Gets or sets whether this message is the first in a consecutive group from the same sender.</summary>
    [ObservableProperty]
    private bool _isFirstInGroup;

    /// <summary>Gets or sets whether this message is the last in a consecutive group from the same sender.</summary>
    [ObservableProperty]
    private bool _isLastInGroup;

    /// <summary>Gets or sets whether this message is currently being streamed from the AI.</summary>
    [ObservableProperty]
    private bool _isStreaming;

    /// <summary>Gets or sets the sender type of this message (User, Agent, Subagent, or Fallback).</summary>
    [ObservableProperty]
    private SenderType _senderType;

    /// <summary>Gets or sets the display name of the sender.</summary>
    [ObservableProperty]
    private string _senderName = string.Empty;

    /// <summary>Gets or sets whether this message is an optimistic placeholder awaiting server confirmation.</summary>
    [ObservableProperty]
    private bool _isOptimistic;

    /// <summary>Gets or sets the kind of message.</summary>
    [ObservableProperty]
    private MessageKind _messageKind = MessageKind.Standard;

    /// <summary>Gets or sets the permission type for inline permission cards.</summary>
    [ObservableProperty]
    private string _permissionType = string.Empty;

    /// <summary>Gets or sets the requested patterns for inline permission cards.</summary>
    [ObservableProperty]
    private IReadOnlyList<string> _permissionPatterns = Array.Empty<string>();

    /// <summary>Gets or sets the permission request identifier.</summary>
    [ObservableProperty]
    private string _requestId = string.Empty;

    /// <summary>Gets or sets the permission status.</summary>
    [ObservableProperty]
    private PermissionStatus _permissionStatus = PermissionStatus.Pending;

    /// <summary>Gets or sets the raw reply value chosen by the user.</summary>
    [ObservableProperty]
    private string? _resolvedReply;

    /// <summary>Gets or sets the label shown for the chosen reply.</summary>
    [ObservableProperty]
    private string? _resolvedReplyLabel;

    /// <summary>Gets or sets the AI reasoning/thinking text for this message.</summary>
    [ObservableProperty]
    private string _reasoningText = string.Empty;

    /// <summary>Gets or sets the number of agentic steps taken to produce this message.</summary>
    [ObservableProperty]
    private int _stepCount;

    /// <summary>Gets or sets the cost of the last step in USD.</summary>
    [ObservableProperty]
    private decimal? _lastStepCost;

    /// <summary>Gets or sets the compaction notice text. When non-null, this message is a context compaction marker.</summary>
    [ObservableProperty]
    private string? _compactionNotice;

    /// <summary>Gets or sets the raw SSE event-type string for fallback messages. Null on non-fallback messages.</summary>
    [ObservableProperty]
    private string? _fallbackRawType;

    /// <summary>Gets or sets the pretty-printed JSON payload for fallback messages (DEBUG builds only). Null in Release builds and on non-fallback messages.</summary>
    [ObservableProperty]
    private string? _fallbackRawJson;

    /// <summary>Gets or sets whether the reasoning block is expanded in the UI.</summary>
    [ObservableProperty]
    private bool _isReasoningExpanded;

    /// <summary>Gets the list of tool call invocations within this message.</summary>
    public ObservableCollection<ToolCallInfo> ToolCalls { get; }

    /// <summary>Gets a value indicating whether this message contains any tool calls.</summary>
    public bool HasToolCalls => ToolCalls.Count > 0;

    /// <summary>Gets a value indicating whether this message contains AI reasoning text.</summary>
    public bool HasReasoning => !string.IsNullOrEmpty(ReasoningText);

    /// <summary>Gets the list of subtask and agent labels for this message.</summary>
    public ObservableCollection<string> SubtaskLabels { get; }

    /// <summary>
    /// Initialises a new <see cref="ChatMessage"/> with the specified immutable properties.
    /// </summary>
    /// <param name="id">The unique message identifier.</param>
    /// <param name="sessionId">The session this message belongs to.</param>
    /// <param name="isFromUser">Whether the message was sent by the user.</param>
    /// <param name="textContent">The initial text content.</param>
    /// <param name="timestamp">The creation timestamp.</param>
    /// <param name="deliveryStatus">The initial delivery status.</param>
    /// <param name="isStreaming">Whether the message is currently being streamed.</param>
    /// <param name="senderType">The sender type (User, Agent, Subagent, or Fallback).</param>
    /// <param name="senderName">The display name of the sender.</param>
    /// <param name="isOptimistic">Whether this is an optimistic placeholder awaiting server confirmation.</param>
    internal ChatMessage(
        string id,
        string sessionId,
        bool isFromUser,
        string textContent,
        DateTimeOffset timestamp,
        MessageDeliveryStatus deliveryStatus,
        bool isStreaming,
        SenderType senderType = SenderType.Agent,
        string senderName = "",
        bool isOptimistic = false,
        MessageKind messageKind = MessageKind.Standard,
        string permissionType = "",
        IReadOnlyList<string>? permissionPatterns = null,
        string requestId = "",
        PermissionStatus permissionStatus = PermissionStatus.Pending,
        string? resolvedReply = null,
        string? resolvedReplyLabel = null,
        string? fallbackRawType = null,
        string? fallbackRawJson = null)
    {
        Id = id;
        SessionId = sessionId;
        IsFromUser = isFromUser;
        _textContent = textContent;
        Timestamp = timestamp;
        _deliveryStatus = deliveryStatus;
        _isStreaming = isStreaming;
        _senderType = senderType;
        _senderName = senderName;
        _isOptimistic = isOptimistic;
        _messageKind = messageKind;
        _permissionType = permissionType;
        _permissionPatterns = permissionPatterns ?? Array.Empty<string>();
        _requestId = requestId;
        _permissionStatus = permissionStatus;
        _resolvedReply = resolvedReply;
        _resolvedReplyLabel = resolvedReplyLabel;
        _fallbackRawType = fallbackRawType;
        _fallbackRawJson = fallbackRawJson;

        ToolCalls = new ObservableCollection<ToolCallInfo>();
        SubtaskLabels = new ObservableCollection<string>();

        // Wire HasToolCalls notification
        ToolCalls.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasToolCalls));
    }

    /// <summary>Raises <see cref="HasReasoning"/> change notification when <see cref="ReasoningText"/> changes.</summary>
    partial void OnReasoningTextChanged(string value)
    {
        OnPropertyChanged(nameof(HasReasoning));
    }

    /// <summary>
    /// Toggles the <see cref="IsReasoningExpanded"/> state.
    /// Bound to the "Show thinking" / "Hide thinking" tap gesture in the chat DataTemplate.
    /// </summary>
    [RelayCommand]
    private void ToggleReasoning() => IsReasoningExpanded = !IsReasoningExpanded;

    /// <summary>
    /// Creates a <see cref="ChatMessage"/> from a server DTO.
    /// Extracts text content by concatenating all parts of type <c>"text"</c>,
    /// and determines streaming state from the <c>completed</c> field in the time object.
    /// </summary>
    /// <param name="dto">The message DTO received from the server.</param>
    /// <returns>A new <see cref="ChatMessage"/> mapped from the DTO.</returns>
    public static ChatMessage FromDto(MessageWithPartsDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var isFromUser = string.Equals(dto.Info.Role, "user", StringComparison.OrdinalIgnoreCase);

        // Extract text content by concatenating all "text" parts (Parts may be null in streaming events)
        var textContent = ExtractTextContent(dto.Parts ?? []);

        // Extract timestamp from the "created" field in the Time JSON
        var timestamp = ExtractTimestamp(dto.Info.Time);

        // Determine streaming state: user messages are never streaming.
        // For assistant messages, check if "completed" exists and is non-null.
        var isStreaming = !isFromUser && !HasCompletedTimestamp(dto.Info.Time);

        // Determine sender type and name
        var senderType = isFromUser ? SenderType.User : SenderType.Agent;
        var senderName = isFromUser ? "You" : "Assistant";

        return new ChatMessage(
            id: dto.Info.Id,
            sessionId: dto.Info.SessionId,
            isFromUser: isFromUser,
            textContent: textContent,
            timestamp: timestamp,
            deliveryStatus: MessageDeliveryStatus.Sent,
            isStreaming: isStreaming,
            senderType: senderType,
            senderName: senderName);
    }

    /// <summary>
    /// Creates an inline permission request card.
    /// </summary>
    /// <param name="id">The permission request identifier.</param>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="permissionType">The requested permission label.</param>
    /// <param name="patterns">The requested pattern list.</param>
    /// <returns>A new permission request message.</returns>
    public static ChatMessage CreatePermissionRequest(string id, string sessionId, string permissionType, IReadOnlyList<string> patterns)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(sessionId);
        ArgumentNullException.ThrowIfNull(permissionType);
        ArgumentNullException.ThrowIfNull(patterns);

        return new ChatMessage(
            id: id,
            sessionId: sessionId,
            isFromUser: false,
            textContent: string.Empty,
            timestamp: DateTimeOffset.UtcNow,
            deliveryStatus: MessageDeliveryStatus.Sent,
            isStreaming: false,
            senderType: SenderType.Agent,
            senderName: "Permission",
            messageKind: MessageKind.PermissionRequest,
            permissionType: permissionType,
            permissionPatterns: patterns,
            requestId: id,
            permissionStatus: PermissionStatus.Pending);
    }

    /// <summary>
    /// Creates an optimistic <see cref="ChatMessage"/> for a user-sent message
    /// before server confirmation. The <see cref="Id"/> is a temporary GUID that
    /// may be replaced when the server confirms the message.
    /// </summary>
    /// <param name="sessionId">The session to send the message to.</param>
    /// <param name="text">The message text content.</param>
    /// <returns>A new optimistic <see cref="ChatMessage"/> with <see cref="MessageDeliveryStatus.Sending"/>.</returns>
    public static ChatMessage CreateOptimistic(string sessionId, string text)
    {
        ArgumentNullException.ThrowIfNull(sessionId);
        ArgumentNullException.ThrowIfNull(text);

        return new ChatMessage(
            id: Guid.NewGuid().ToString(),
            sessionId: sessionId,
            isFromUser: true,
            textContent: text,
            timestamp: DateTimeOffset.UtcNow,
            deliveryStatus: MessageDeliveryStatus.Sending,
            isStreaming: false,
            senderType: SenderType.User,
            senderName: "You",
            isOptimistic: true);
    }

    /// <summary>
    /// Creates a <see cref="ChatMessage"/> representing an unhandled SSE event.
    /// In DEBUG builds, the raw event type and JSON payload are preserved for diagnostics.
    /// In Release builds, both fields are always <c>null</c> to prevent data leakage.
    /// </summary>
    /// <param name="rawType">The raw SSE event-type string from <see cref="UnknownEvent.RawType"/>.</param>
    /// <param name="rawJson">The pretty-printed JSON payload, or <c>null</c> if no payload was present.</param>
    /// <returns>A new fallback <see cref="ChatMessage"/> with <see cref="SenderType.Fallback"/>.</returns>
    /// <remarks>
    /// <para>
    /// <see cref="SessionId"/> is intentionally set to <see cref="string.Empty"/> because fallback messages
    /// are ephemeral in-memory entries that are never persisted to the database. They are not filtered
    /// by session ID within the ViewModel's <c>Messages</c> collection. If future code needs to associate
    /// a fallback message with a specific session, thread the session ID through this factory.
    /// </para>
    /// </remarks>
    public static ChatMessage CreateFallback(string rawType, string? rawJson)
    {
        ArgumentNullException.ThrowIfNull(rawType);

#if DEBUG
        return new ChatMessage(
            id: Guid.NewGuid().ToString(),
            sessionId: string.Empty, // Intentionally empty — see remarks
            isFromUser: false,
            textContent: string.Empty,
            timestamp: DateTimeOffset.UtcNow,
            deliveryStatus: MessageDeliveryStatus.Sent,
            isStreaming: false,
            senderType: SenderType.Fallback,
            senderName: string.Empty,
            fallbackRawType: rawType,
            fallbackRawJson: rawJson);
#else
        return new ChatMessage(
            id: Guid.NewGuid().ToString(),
            sessionId: string.Empty, // Intentionally empty — see remarks
            isFromUser: false,
            textContent: string.Empty,
            timestamp: DateTimeOffset.UtcNow,
            deliveryStatus: MessageDeliveryStatus.Sent,
            isStreaming: false,
            senderType: SenderType.Fallback,
            senderName: string.Empty,
            fallbackRawType: null,
            fallbackRawJson: null);
#endif
    }

    /// <summary>
    /// Extracts and concatenates text content from all parts of type <c>"text"</c>.
    /// </summary>
    /// <param name="parts">The message parts to extract text from.</param>
    /// <returns>The concatenated text content, or <see cref="string.Empty"/> if no text parts exist.</returns>
    internal static string ExtractTextContent(IReadOnlyList<PartDto> parts)
    {
        if (parts is null || parts.Count == 0)
            return string.Empty;

        var textParts = new List<string>();

        foreach (var part in parts)
        {
            if (!string.Equals(part.Type, "text", StringComparison.OrdinalIgnoreCase))
                continue;

            // The opencode server returns text directly in the "text" field of the part object.
            if (!string.IsNullOrEmpty(part.Text))
                textParts.Add(part.Text);
        }

        return string.Join("", textParts);
    }

    /// <summary>
    /// Extracts the <c>created</c> timestamp from the message time JSON element.
    /// Falls back to <see cref="DateTimeOffset.UtcNow"/> if the field is missing.
    /// </summary>
    /// <param name="time">The raw JSON time element.</param>
    /// <returns>The extracted or fallback timestamp.</returns>
    private static DateTimeOffset ExtractTimestamp(JsonElement time)
    {
        if (time.ValueKind == JsonValueKind.Object &&
            time.TryGetProperty("created", out var createdEl) &&
            createdEl.ValueKind == JsonValueKind.Number)
        {
            return DateTimeOffset.FromUnixTimeMilliseconds(createdEl.GetInt64());
        }

        return DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Checks whether the time JSON element contains a non-null <c>completed</c> property,
    /// indicating the message has finished streaming.
    /// </summary>
    /// <param name="time">The raw JSON time element.</param>
    /// <returns><c>true</c> if a non-null <c>completed</c> property exists; otherwise <c>false</c>.</returns>
    internal static bool HasCompletedTimestamp(JsonElement time)
    {
        if (time.ValueKind == JsonValueKind.Object &&
            time.TryGetProperty("completed", out var completedEl) &&
            completedEl.ValueKind != JsonValueKind.Null)
        {
            return true;
        }

        return false;
    }
}
