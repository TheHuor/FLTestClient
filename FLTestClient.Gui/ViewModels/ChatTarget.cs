namespace FLTestClient.Gui.ViewModels;

/// <summary>Where a message from the chat page goes: a chat channel or a real player.</summary>
/// <param name="Label">Translated text for the list.</param>
/// <param name="Channel">CHAT_ID of the channel, see <see cref="ChatId"/>; ignored for a player.</param>
/// <param name="PlayerName">Character name for a private message; null for a channel.</param>
public sealed record ChatTarget(string Label, uint Channel, string? PlayerName = null)
{
    /// <summary>Builds the list of targets: the channels first, then the players.</summary>
    /// <param name="players">Real players online.</param>
    /// <returns>Targets; the first one is the default for FLHook commands.</returns>
    public static IReadOnlyList<ChatTarget> Build(IEnumerable<string> players)
    {
        var targets = new List<ChatTarget>
        {
            new(Loc.T("Comms.TargetCommands"), ChatId.Universe),
            new(Loc.T("Comms.TargetGroup"), ChatId.Group),
            new(Loc.T("Comms.TargetLocal"), ChatId.Local),
            new(Loc.T("Comms.TargetSystem"), ChatId.System),
        };
        targets.AddRange(players.Select(name => new ChatTarget(Loc.T("Comms.TargetPlayer", name), 0, name)));
        return targets;
    }

    /// <summary>True if both name the same destination, whatever the language of the label.</summary>
    /// <param name="other">Other target.</param>
    /// <returns>True for the same channel or player.</returns>
    public bool SameDestination(ChatTarget other) => Channel == other.Channel && PlayerName == other.PlayerName;
}
