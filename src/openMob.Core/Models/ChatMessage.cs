using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using openMob.Core.Infrastructure.Http.Dtos.Opencode;

namespace openMob.Core.Models;

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

    /// <summary>Gets or sets the sender type of this message (User, Agent, or Subagent).</summary>
    [ObservableProperty]
    private SenderType _senderType;

    /// <summary>Gets or sets the display name of the sender.</summary>
    [ObservableProperty]
    private string _senderName = string.Empty;

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
    internal ChatMessage(
        string id,
        string sessionId,
        bool isFromUser,
        string textContent,
        DateTimeOffset timestamp,
        MessageDeliveryStatus deliveryStatus,
        bool isStreaming,
        SenderType senderType = SenderType.Agent,
        string senderName = "")
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
    }

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
            senderName: "You");
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
