namespace FLTestClient;

/// <summary>Decides which real players a bot contacts on its own initiative, and when.</summary>
/// <remarks>
/// <para>There is one planner per <see cref="BotManager"/>, so a player hears from one bot and not from all
/// of them. Players who are online while the bots log in only count as known: the list of a fresh bot says
/// nothing about who has just arrived, so the planner waits <see cref="SettleTime"/> before it greets anyone.</para>
/// <para>The class is not thread-safe; it runs on the network thread of the manager.</para>
/// </remarks>
public sealed class PlayerContactPlanner
{
    /// <summary>Time after the first bot is in the game until newly seen players count as arrivals.</summary>
    public static readonly TimeSpan SettleTime = TimeSpan.FromSeconds(10);

    /// <summary>Shortest time between two contacts with the same player.</summary>
    public static readonly TimeSpan PlayerPause = TimeSpan.FromMinutes(5);

    private const double MinGreetingDelaySeconds = 5;
    private const double MaxGreetingDelaySeconds = 15;

    private readonly Random _random;
    private readonly HashSet<string> _knownPlayers = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DateTime> _pendingGreetings = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DateTime> _lastContacts = new(StringComparer.OrdinalIgnoreCase);
    private DateTime? _settledAt;
    private DateTime? _nextApproachAt;

    /// <summary>Creates a planner.</summary>
    /// <param name="random">Source of the random delays and the choice of player.</param>
    public PlayerContactPlanner(Random random) => _random = random;

    /// <summary>Returns the players to contact now and counts them as contacted.</summary>
    /// <param name="now">Current time.</param>
    /// <param name="onlinePlayers">Real players online, without bots.</param>
    /// <param name="botsInGame">True if at least one bot is in the game and can see the player list.</param>
    /// <param name="greetArrivals">Greet players who log in.</param>
    /// <param name="approachIntervalSeconds">Average time between two random contacts; 0 switches them off.</param>
    /// <returns>Names of the players to contact.</returns>
    public IReadOnlyList<string> Due(DateTime now, IReadOnlyCollection<string> onlinePlayers, bool botsInGame,
        bool greetArrivals, double approachIntervalSeconds)
    {
        if (!botsInGame)
        {
            _settledAt = null;
            _nextApproachAt = null;
            _pendingGreetings.Clear();
            return Array.Empty<string>();
        }
        _settledAt ??= now + SettleTime;
        if (now < _settledAt)
        {
            _knownPlayers.UnionWith(onlinePlayers);
            return Array.Empty<string>();
        }

        var online = new HashSet<string>(onlinePlayers, StringComparer.OrdinalIgnoreCase);
        TrackArrivals(now, online, greetArrivals);

        var due = new List<string>();
        foreach (var (name, greetAt) in _pendingGreetings.ToArray())
        {
            if (now < greetAt)
                continue;
            _pendingGreetings.Remove(name);
            if (MayContact(name, now))
                due.Add(name);
        }
        if (ApproachDue(now, approachIntervalSeconds))
        {
            var candidates = online.Where(name => !due.Contains(name) && MayContact(name, now)).ToArray();
            if (candidates.Length > 0)
                due.Add(candidates[_random.Next(candidates.Length)]);
        }
        foreach (var name in due)
            _lastContacts[name] = now;
        return due;
    }

    private void TrackArrivals(DateTime now, HashSet<string> online, bool greetArrivals)
    {
        _knownPlayers.IntersectWith(online);
        foreach (var name in online)
        {
            if (_knownPlayers.Add(name) && greetArrivals)
            {
                double delay = MinGreetingDelaySeconds + _random.NextDouble() * (MaxGreetingDelaySeconds - MinGreetingDelaySeconds);
                _pendingGreetings[name] = now.AddSeconds(delay);
            }
        }
        foreach (var name in _pendingGreetings.Keys.ToArray())
        {
            if (!greetArrivals || !online.Contains(name))
                _pendingGreetings.Remove(name);
        }
    }

    private bool ApproachDue(DateTime now, double intervalSeconds)
    {
        if (intervalSeconds <= 0)
        {
            _nextApproachAt = null;
            return false;
        }
        bool due = _nextApproachAt != null && now >= _nextApproachAt;
        if (due || _nextApproachAt == null)
            _nextApproachAt = now.AddSeconds(intervalSeconds * (0.5 + _random.NextDouble()));
        return due;
    }

    private bool MayContact(string name, DateTime now) =>
        !_lastContacts.TryGetValue(name, out var last) || now - last >= PlayerPause;
}
