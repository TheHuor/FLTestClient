using System.Collections.Concurrent;

namespace FLTestClient;

/// <summary>
/// Manages a group of bots on a dedicated network thread.
/// </summary>
/// <remarks>
/// <para>Bots are only touched on this thread (it is a COM MTA, as DirectPlay requires). Other threads, e.g.
/// a GUI, send work with <see cref="Post"/> and read the state through <see cref="Snapshots"/>.</para>
/// <para>The events <see cref="Logged"/> and <see cref="ChatReceived"/> are raised on the network thread;
/// a GUI has to marshal them to its own thread.</para>
/// </remarks>
public sealed class BotManager : IDisposable
{
    private readonly BotOptions options;
    private readonly List<Bot> bots = new();
    private readonly Queue<Bot> startQueue = new();
    private readonly ConcurrentQueue<Action<BotManager>> work = new();
    private readonly Thread thread;
    private volatile bool running = true;
    private volatile BotSnapshot[] snapshots = Array.Empty<BotSnapshot>();
    private DateTime nextStartAt = DateTime.MinValue;

    /// <summary>Delay between two connection attempts in seconds.</summary>
    public double StaggerSeconds { get; set; } = 0.5;

    /// <summary>Shared settings of all bots of this manager.</summary>
    public BotOptions Options => options;

    /// <summary>State of all bots, refreshed about four times per second. Thread-safe.</summary>
    public IReadOnlyList<BotSnapshot> Snapshots => snapshots;

    /// <summary>Log line of a bot or of the manager (bot = null). Raised on the network thread.</summary>
    public event Action<Bot?, LogLevel, string>? Logged;

    /// <summary>Chat message to a bot. Raised on the network thread.</summary>
    public event Action<Bot, ChatMessage>? ChatReceived;

    /// <summary>Creates the manager and starts its network thread.</summary>
    /// <param name="options">Settings for all bots.</param>
    public BotManager(BotOptions options)
    {
        this.options = options;
        thread = new Thread(Run) { IsBackground = true, Name = "FLTestClient-Net" };
        thread.SetApartmentState(ApartmentState.MTA);
        thread.Start();
    }

    /// <summary>Runs an action on the network thread.</summary>
    /// <param name="action">Action with access to the manager and its bots.</param>
    public void Post(Action<BotManager> action) => work.Enqueue(action);

    /// <summary>
    /// All bots. Only use on the network thread (inside <see cref="Post"/>).
    /// </summary>
    public IReadOnlyList<Bot> Bots => bots;

    /// <summary>
    /// Creates bots with consecutive numbers and connects them one after another, <see cref="StaggerSeconds"/>
    /// apart. Numbers that already exist are skipped. Only call on the network thread.
    /// </summary>
    /// <param name="first">First bot number.</param>
    /// <param name="count">Number of bots.</param>
    public void AddBots(int first, int count)
    {
        for (int n = first; n < first + count; n++)
        {
            if (bots.Any(b => b.Index == n))
                continue;
            var bot = new Bot(n, options);
            bot.Logged += (b, level, text) => Logged?.Invoke(b, level, text);
            bot.ChatReceived += (b, msg) => ChatReceived?.Invoke(b, msg);
            bots.Add(bot);
            startQueue.Enqueue(bot);
        }
    }

    /// <summary>Finds a bot by number. Only call on the network thread.</summary>
    /// <param name="index">Bot number.</param>
    /// <returns>The bot or null.</returns>
    public Bot? Find(int index) => bots.FirstOrDefault(b => b.Index == index);

    /// <summary>Disconnects a bot cleanly and removes it. Only call on the network thread.</summary>
    /// <param name="bot">Bot.</param>
    public void Remove(Bot bot)
    {
        bot.Stop();
        bots.Remove(bot);
        // DirectPlay needs a moment to log out; release afterwards.
        var due = DateTime.UtcNow.AddSeconds(1);
        Post(m => m.DisposeLater(bot, due));
    }

    private void DisposeLater(Bot bot, DateTime due)
    {
        if (DateTime.UtcNow < due)
            Post(m => m.DisposeLater(bot, due));
        else
            bot.Dispose();
    }

    /// <summary>Disconnects all bots cleanly, releases them and ends the network thread.</summary>
    public void Dispose()
    {
        if (!running)
            return;
        Post(m =>
        {
            foreach (var bot in m.bots)
                bot.Stop();
        });
        Thread.Sleep(1000);
        running = false;
        thread.Join(3000);
    }

    private void Run()
    {
        DP8.CoInitializeEx(IntPtr.Zero, 0 /* COINIT_MULTITHREADED */);
        DateTime nextSnapshot = DateTime.MinValue;
        while (running)
        {
            // Only the work queued so far; whatever it queues itself runs on the next tick.
            for (int pendingWork = work.Count; pendingWork > 0 && work.TryDequeue(out var action); pendingWork--)
            {
                try
                {
                    action(this);
                }
                catch (Exception e)
                {
                    Logged?.Invoke(null, LogLevel.Error, Texts.Get("Manager.Error", e.Message));
                }
            }

            var now = DateTime.UtcNow;
            if (startQueue.Count > 0 && now >= nextStartAt)
            {
                var bot = startQueue.Dequeue();
                try
                {
                    bot.Start();
                }
                catch (Exception e)
                {
                    bot.Log(LogLevel.Error, "Bot.StartFailed", e.Message);
                }
                nextStartAt = now.AddSeconds(StaggerSeconds);
            }

            foreach (var bot in bots)
            {
                try
                {
                    bot.Tick();
                }
                catch (Exception e)
                {
                    bot.Log(LogLevel.Error, "Bot.TickError", e.Message);
                }
            }

            if (now >= nextSnapshot)
            {
                snapshots = bots.Select(b => b.Snapshot()).ToArray();
                nextSnapshot = now.AddMilliseconds(250);
            }
            Thread.Sleep(50);
        }

        foreach (var bot in bots)
            bot.Dispose();
        bots.Clear();
        snapshots = Array.Empty<BotSnapshot>();
    }
}
