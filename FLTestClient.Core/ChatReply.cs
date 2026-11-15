namespace FLTestClient;

/// <summary>CHAT_ID values of the chat channels. A CHAT_ID with byte 2 clear names a player instead.</summary>
public static class ChatId
{
    /// <summary>Universe chat; also the target of FLHook commands.</summary>
    public const uint Universe = 0x10000;
    /// <summary>System chat.</summary>
    public const uint System = 0x10001;
    /// <summary>Local chat.</summary>
    public const uint Local = 0x10002;
    /// <summary>Group chat.</summary>
    public const uint Group = 0x10003;
    /// <summary>Group control: 8-byte records instead of text (invite, join, leave).</summary>
    public const uint GroupControl = 0x10004;
}

/// <summary>What a bot does with a chat message it may answer.</summary>
/// <param name="Target">CHAT_ID the answer goes to: the sender of a private message or the channel.</param>
/// <param name="SenderName">Character name of the sender.</param>
/// <param name="AlwaysAnswer">True for private and group messages and for messages that mention the bot by name.</param>
public sealed record ChatReplyPlan(uint Target, string SenderName, bool AlwaysAnswer);

/// <summary>Decides whether a received chat message gets an answer.</summary>
/// <remarks>
/// <para>Only the server's "<c>Name: text</c>" header names the sender of a channel message, because the
/// CHAT_ID of such a message is the channel. The header also tells the copy of a bot's own private message
/// (which the server sends back with the receiver's ID) from a real message. Other bots are never answered,
/// otherwise two bots would keep talking to each other.</para>
/// <para>FLHook (HkCb_SendChat) re-encodes all player chat and delivers it as <see cref="ChatId.Universe"/>;
/// the channel then only shows in the text colour, and the sender must be a known player. That also sorts out
/// FLHook's own messages such as "Your client-id: 3".</para>
/// </remarks>
public static class ChatReply
{
    private const string HeaderSeparator = ": ";

    // Text colours of HkCb_SendChat.
    private const uint PrivateColor = 0x19BD3A;
    private const uint GroupColor = 0xFF7BFF;
    private const uint LocalColor = 0xFF8F40;
    private const uint SystemColor = 0xE6C684;

    /// <summary>Works out the answer to a chat message.</summary>
    /// <param name="message">Received message.</param>
    /// <param name="ownName">Character name of the bot.</param>
    /// <param name="botNamePrefix">Character prefix of all bots, see <see cref="BotOptions.NamePrefix"/>.</param>
    /// <param name="findPlayer">Finds the client ID of a character name; null if there is no such player.</param>
    /// <returns>The plan, or null if the message must not be answered.</returns>
    public static ChatReplyPlan? Plan(ChatMessage message, string ownName, string botNamePrefix, Func<string, uint?> findPlayer)
    {
        int separator = message.Text.IndexOf(HeaderSeparator, StringComparison.Ordinal);
        if (separator <= 0)
            return null;
        string senderName = message.Text[..separator];
        string body = message.Text[(separator + HeaderSeparator.Length)..];
        // A leftover "/..." is a mistyped or unknown command that the server passed on as text, not a chat line.
        if (body.Trim().Length == 0 || body.StartsWith('/') || IsBot(senderName, botNamePrefix)
            || senderName.Equals(ownName, StringComparison.OrdinalIgnoreCase))
            return null;

        bool mentioned = body.Contains(ownName, StringComparison.OrdinalIgnoreCase);
        if (message.From == ChatId.Group)
            return new ChatReplyPlan(ChatId.Group, senderName, true);
        if (message.From is ChatId.System or ChatId.Local)
            return new ChatReplyPlan(message.From, senderName, mentioned);
        if (message.From == ChatId.Universe)
            return PlanFromColor(message.TextColor, senderName, mentioned, findPlayer);
        bool fromPlayer = message.From != 0 && message.From <= 0xFFFF;
        return fromPlayer ? new ChatReplyPlan(message.From, senderName, true) : null;
    }

    private static ChatReplyPlan? PlanFromColor(uint? color, string senderName, bool mentioned, Func<string, uint?> findPlayer)
    {
        if (findPlayer(senderName) is not uint senderId)
            return null;
        return color switch
        {
            PrivateColor => new ChatReplyPlan(senderId, senderName, true),
            GroupColor => new ChatReplyPlan(ChatId.Group, senderName, true),
            LocalColor => new ChatReplyPlan(ChatId.Local, senderName, mentioned),
            SystemColor => new ChatReplyPlan(ChatId.System, senderName, mentioned),
            _ => null,
        };
    }

    /// <summary>Tells bots from real players: bot names are the prefix followed by the bot number.</summary>
    /// <param name="name">Character name.</param>
    /// <param name="prefix">Character prefix of all bots, see <see cref="BotOptions.NamePrefix"/>.</param>
    /// <returns>True if the name belongs to a bot.</returns>
    public static bool IsBot(string name, string prefix) =>
        prefix.Length > 0 && name.Length > prefix.Length && name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
        && name.AsSpan(prefix.Length).IndexOfAnyExceptInRange('0', '9') < 0;
}
