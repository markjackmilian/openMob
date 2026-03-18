namespace openMob.Core.Models;

/// <summary>Represents the delivery status of a chat message.</summary>
public enum MessageDeliveryStatus
{
    /// <summary>Message sent optimistically, awaiting server confirmation.</summary>
    Sending,

    /// <summary>Message confirmed by the server.</summary>
    Sent,

    /// <summary>Message delivery failed.</summary>
    Error,
}
