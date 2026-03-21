namespace openMob.Helpers;

/// <summary>
/// Centralised icon glyph constants. All icon references in XAML must use
/// <c>{x:Static helpers:IconKeys.XxxName}</c> — no raw Unicode strings in XAML.
/// </summary>
/// <remarks>
/// Currently uses MaterialSymbols codepoints. Will be migrated to Tabler Icons
/// codepoints when the TablerIcons.ttf font file is integrated.
/// </remarks>
public static class IconKeys
{
    /// <summary>Hamburger menu icon (three horizontal lines).</summary>
    public const string Menu = "\ue5d2";

    /// <summary>Add / plus icon.</summary>
    public const string Add = "\ue145";

    /// <summary>Send message icon.</summary>
    public const string Send = "\ue163";

    /// <summary>Arrow upward icon (used for send button in ChatGPT style).</summary>
    public const string ArrowUp = "\ue5d8";

    /// <summary>Auto-awesome / sparkle icon (for auto-accept).</summary>
    public const string AutoAwesome = "\ue65f";

    /// <summary>Microphone icon.</summary>
    public const string Mic = "\ue029";

    /// <summary>Edit / pencil icon.</summary>
    public const string Edit = "\ue3c9";

    /// <summary>Settings / gear icon.</summary>
    public const string Settings = "\ue8b8";

    /// <summary>Chevron right icon (used as disclosure indicator).</summary>
    public const string ChevronRight = "\ue5cc";

    /// <summary>Code icon (for command palette button).</summary>
    public const string Code = "\ue86f";

    /// <summary>Close / X icon.</summary>
    public const string X = "\ue5cd";

    /// <summary>Copy / clipboard icon (for code block copy action).</summary>
    public const string Copy = "\ue14d";

    /// <summary>Check / checkmark icon.</summary>
    public const string Check = "\ue5ca";

    /// <summary>Folder icon.</summary>
    public const string Folder = "\ue2c7";

    /// <summary>Chat / message bubble icon.</summary>
    public const string Chat = "\ue0b7";

    /// <summary>More vertical (three dots) icon.</summary>
    public const string DotsVertical = "\ue5d4";

    /// <summary>Delete / trash icon.</summary>
    public const string Trash = "\ue872";

    /// <summary>Notifications / bell icon.</summary>
    public const string Bell = "\ue7f4";

    /// <summary>Public / globe icon.</summary>
    public const string Globe = "\ue80b";

    /// <summary>Psychology / brain icon (for thinking level).</summary>
    public const string Brain = "\uea4a";

    /// <summary>Warning / alert triangle icon.</summary>
    public const string AlertTriangle = "\ue002";

    /// <summary>Error / alert circle icon.</summary>
    public const string AlertCircle = "\ue000";

    /// <summary>Info / circle with i icon.</summary>
    public const string InfoCircle = "\ue88e";

    /// <summary>Search icon.</summary>
    public const string Search = "\ue8b6";

    /// <summary>Arrow left / back icon.</summary>
    public const string ArrowLeft = "\ue5c4";

    /// <summary>Chevron down / expand more icon.</summary>
    public const string ChevronDown = "\ue5cf";

    /// <summary>Smart toy / robot icon (for AI/agent).</summary>
    public const string Robot = "\ue99a";

    /// <summary>Stop icon (for cancel/stop action).</summary>
    public const string PlayerStop = "\ue047";

    /// <summary>Key icon (for API keys).</summary>
    public const string Key = "\ue73c";

    /// <summary>Link icon (for URLs/connections).</summary>
    public const string Link = "\ue157";

    /// <summary>Check circle icon (for success/completion states).</summary>
    public const string CircleCheck = "\ue86c";

    /// <summary>Radio button unchecked icon.</summary>
    public const string Circle = "\ue836";

    /// <summary>Radio button checked icon.</summary>
    public const string CircleDot = "\ue837";

    /// <summary>Schedule / clock icon.</summary>
    public const string Clock = "\ue8b5";

    /// <summary>Terminal icon (for command palette).</summary>
    public const string Terminal = "\ueb8e";

    /// <summary>Tune / adjustments icon (for context/settings).</summary>
    public const string Adjustments = "\ue429";
}
