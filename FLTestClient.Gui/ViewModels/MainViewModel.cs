using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Windows.Input;
using System.Windows.Threading;

namespace FLTestClient.Gui.ViewModels;

/// <summary>
/// View model of the main window: fleet, comms, command (FLHook admin), server log and settings.
/// </summary>
/// <remarks>
/// The bots run on the network thread of a <see cref="BotManager"/>. This view model only reads its snapshots
/// and sends work with <see cref="BotManager.Post"/>. Messages from other threads go into queues that a
/// <see cref="DispatcherTimer"/> moves into the lists four times per second.
/// </remarks>
public sealed partial class MainViewModel : ObservableObject, IDisposable
{
    private const int MaxFeedEntries = 3000;

    private readonly GuiSettings settings;
    private readonly Action<GuiSettings> persistSettings;
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private readonly ConcurrentQueue<(int? Bot, FeedEntry Entry)> activityQueue = new();
    private readonly ConcurrentQueue<FeedEntry> chatQueue = new();
    private readonly ConcurrentQueue<FeedEntry> adminQueue = new();
    private readonly ConcurrentQueue<(FeedEntry Entry, LogLineKind Kind)> serverLogQueue = new();
    private readonly Dictionary<int, BotRowViewModel> rowsByIndex = new();
    private BotManager? manager;
    private UniverseNames? universe;
    private ServerLogWatcher? logWatcher;

    private string host, accountPrefix, namePrefix, dataDirectory, adminHost, flhookIni, distributeBases, serverLogPath, language;
    private string password = "", adminPassword = "", chatInput = "", adminCommand = "", statusText = "";
    private int port, startIndex, count, adminPort;
    private bool autoLaunch, isDarkTheme, chatToAll, activitySelectedOnly, followServerLog, importantOnly = true;
    private int onlineCount, inSpaceCount, dockedCount, failedCount, timeLagCount, kickCount, cheatCount, errorCount;
    private string faction, newCharBase, package, pilot, chatScript, dockTarget;
    private bool circleFlight, sessionCycle;
    private double circleRadius, circleSpeed, onlineMinSeconds, onlineMaxSeconds, offlineSeconds, chatIntervalSeconds;

    /// <summary>Raised when the theme should change (true = dark).</summary>
    public event Action<bool>? ThemeChanged;

    /// <summary>Raised when the language should change (<c>en</c> or <c>de</c>).</summary>
    public event Action<string>? LanguageChanged;

    /// <summary>Loads the stored settings and starts the refresh timer.</summary>
    public MainViewModel() : this(GuiSettings.Load(), s => s.Save()) { }

    internal MainViewModel(GuiSettings initialSettings, Action<GuiSettings> persist)
    {
        settings = initialSettings;
        persistSettings = persist;
        host = settings.Host.Length > 0 ? settings.Host : GuessLocalAddress();
        port = settings.Port;
        accountPrefix = settings.AccountPrefix;
        namePrefix = settings.NamePrefix;
        startIndex = Math.Clamp(settings.StartIndex, 1, 999);
        count = Math.Clamp(settings.Count, 1, 200);
        autoLaunch = settings.AutoLaunch;
        dataDirectory = settings.DataDirectory;
        adminHost = settings.AdminHost;
        adminPort = settings.AdminPort;
        flhookIni = settings.FlhookIni;
        distributeBases = settings.DistributeBases;
        serverLogPath = settings.ServerLog;
        isDarkTheme = settings.DarkTheme;
        language = settings.Language == "de" ? "de" : "en";
        faction = settings.Faction;
        newCharBase = settings.NewCharBase;
        package = settings.Package;
        pilot = settings.Pilot;
        circleFlight = settings.CircleFlight;
        circleRadius = settings.CircleRadius;
        circleSpeed = settings.CircleSpeed;
        sessionCycle = settings.SessionCycle;
        onlineMinSeconds = settings.OnlineMinSeconds;
        onlineMaxSeconds = settings.OnlineMaxSeconds;
        offlineSeconds = settings.OfflineSeconds;
        chatIntervalSeconds = settings.ChatIntervalSeconds;
        chatScript = settings.ChatScript;
        dockTarget = settings.DockTarget;
        LoadUniverse(quiet: true);

        StartBotsCommand = new RelayCommand(StartBots);
        DisconnectAllCommand = new RelayCommand(DisconnectAll, () => IsRunning);
        LaunchSelectedCommand = new RelayCommand(() => ForSelected((_, b) => b.Launch(), "Act.Launch"), HasSelection);
        ReconnectSelectedCommand = new RelayCommand(() => ForSelected((_, b) => { b.Stop(); b.Start(); }, "Act.Reconnect"), HasSelection);
        RemoveSelectedCommand = new RelayCommand(() => ForSelected((m, b) => m.Remove(b), "Act.Disconnect"), HasSelection);
        SelectAllCommand = new RelayCommand(() => { foreach (var r in Bots) r.IsSelected = true; }, () => Bots.Count > 0);
        DistributeSelectedCommand = new RelayCommand(() => RequestFleetAction?.Invoke(true), HasSelection);
        SendChatCommand = new RelayCommand(SendChat, () => IsRunning && (ChatToAll ? Bots.Count > 0 : HasSelection()) && ChatInput.Trim().Length > 0);
        QuickChatCommand = new RelayCommand(p => ChatInput = p as string ?? "", _ => true);
        AdminGetPlayersCommand = new RelayCommand(() => RunAdmin("getplayers"));
        AdminRehashCommand = new RelayCommand(() => RunAdmin("rehash"));
        AdminKickSelectedCommand = new RelayCommand(KickSelected, HasSelection);
        AdminRunCommand = new RelayCommand(() => RunAdmin(AdminCommand.Trim()), () => AdminCommand.Trim().Length > 0);
        LoadAdminPasswordCommand = new RelayCommand(LoadAdminPassword);
        ResetCountersCommand = new RelayCommand(() => TimeLagCount = KickCount = CheatCount = ErrorCount = 0);
        ClearActivityCommand = new RelayCommand(() => Activity.Clear());
        ToggleThemeCommand = new RelayCommand(() => IsDarkTheme = !IsDarkTheme);
        ToggleLanguageCommand = new RelayCommand(() => IsGerman = !IsGerman);
        DockSelectedCommand = new RelayCommand(DockSelected, HasSelection);

        InitializeInteraction();

        timer.Tick += (_, _) => Refresh();
        timer.Start();
    }

    // ------------------------------------------------------------------------------------------ lists

    /// <summary>Fleet list.</summary>
    public ObservableCollection<BotRowViewModel> Bots { get; } = new();

    /// <summary>Activity log of all bots.</summary>
    public ObservableCollection<FeedEntry> Activity { get; } = new();

    /// <summary>Received and sent chat.</summary>
    public ObservableCollection<FeedEntry> Chat { get; } = new();

    /// <summary>Output of the FLHook admin console.</summary>
    public ObservableCollection<FeedEntry> AdminOutput { get; } = new();

    /// <summary>Lines followed from the server log.</summary>
    public ObservableCollection<FeedEntry> ServerLog { get; } = new();

    // ------------------------------------------------------------------------------------------ commands

    /// <summary>Creates <see cref="Count"/> bots starting at <see cref="StartIndex"/> and connects them.</summary>
    public ICommand StartBotsCommand { get; }

    /// <summary>Logs out all bots cleanly.</summary>
    public ICommand DisconnectAllCommand { get; }

    /// <summary>Launches the selected bots into space.</summary>
    public ICommand LaunchSelectedCommand { get; }

    /// <summary>Reconnects the selected bots.</summary>
    public ICommand ReconnectSelectedCommand { get; }

    /// <summary>Disconnects and removes the selected bots.</summary>
    public ICommand RemoveSelectedCommand { get; }

    /// <summary>Selects all bots.</summary>
    public ICommand SelectAllCommand { get; }

    /// <summary>Beams the selected bots to the target bases in turn.</summary>
    public ICommand DistributeSelectedCommand { get; }

    /// <summary>Sends <see cref="ChatInput"/>.</summary>
    public ICommand SendChatCommand { get; }

    /// <summary>Sends a given text (parameter), e.g. <c>/help</c>.</summary>
    public ICommand QuickChatCommand { get; }

    /// <summary>FLHook admin: <c>getplayers</c>.</summary>
    public ICommand AdminGetPlayersCommand { get; }

    /// <summary>FLHook admin: <c>rehash</c>.</summary>
    public ICommand AdminRehashCommand { get; }

    /// <summary>FLHook admin: kick the selected bots.</summary>
    public ICommand AdminKickSelectedCommand { get; }

    /// <summary>FLHook admin: run <see cref="AdminCommand"/>.</summary>
    public ICommand AdminRunCommand { get; }

    /// <summary>Takes the admin password from flhook.ini.</summary>
    public ICommand LoadAdminPasswordCommand { get; }

    /// <summary>Resets the server log counters.</summary>
    public ICommand ResetCountersCommand { get; }

    /// <summary>Clears the activity log.</summary>
    public ICommand ClearActivityCommand { get; }

    /// <summary>Switches between dark and bright theme.</summary>
    public ICommand ToggleThemeCommand { get; }

    /// <summary>Switches between English and German.</summary>
    public ICommand ToggleLanguageCommand { get; }

    /// <summary>Docks the selected bots at <see cref="DockTarget"/> (via FLHook beam).</summary>
    public ICommand DockSelectedCommand { get; }

    // ------------------------------------------------------------------------------------------ new characters

    /// <summary>Base nicknames from universe.ini for the dock picker.</summary>
    public ObservableCollection<string> BaseList { get; } = new();

    /// <summary>Target base for "Dock at base" (nickname, e.g. Li01_01_Base).</summary>
    public string DockTarget { get => dockTarget; set => Set(ref dockTarget, value); }

    /// <summary>Faction for new characters ([Faction] in newcharacter.ini).</summary>
    public string Faction { get => faction; set => Set(ref faction, value); }

    /// <summary>Start base for new characters; must be allowed for the faction in newcharacter.ini.</summary>
    public string NewCharBase { get => newCharBase; set => Set(ref newCharBase, value); }

    /// <summary>Ship package for new characters ([Package]).</summary>
    public string Package { get => package; set => Set(ref package, value); }

    /// <summary>Pilot for new characters ([Pilot]).</summary>
    public string Pilot { get => pilot; set => Set(ref pilot, value); }

    // ------------------------------------------------------------------------------------------ behaviour (live)

    /// <summary>Fly a circle in space instead of standing still. Applies to running bots immediately.</summary>
    public bool CircleFlight { get => circleFlight; set { if (Set(ref circleFlight, value)) ApplyBehaviour(); } }

    /// <summary>Radius of the circle flight in metres (takes effect at the next launch).</summary>
    public double CircleRadius { get => circleRadius; set { if (Set(ref circleRadius, Math.Clamp(value, 50, 20000))) ApplyBehaviour(); } }

    /// <summary>Speed of the circle flight in metres per second.</summary>
    public double CircleSpeed { get => circleSpeed; set { if (Set(ref circleSpeed, Math.Clamp(value, 0, 600))) ApplyBehaviour(); } }

    /// <summary>Log out and in periodically.</summary>
    public bool SessionCycle { get => sessionCycle; set { if (Set(ref sessionCycle, value)) ApplyBehaviour(); } }

    /// <summary>Shortest time online per cycle in seconds.</summary>
    public double OnlineMinSeconds { get => onlineMinSeconds; set { if (Set(ref onlineMinSeconds, Math.Max(5, value))) ApplyBehaviour(); } }

    /// <summary>Longest time online per cycle in seconds.</summary>
    public double OnlineMaxSeconds { get => onlineMaxSeconds; set { if (Set(ref onlineMaxSeconds, Math.Max(5, value))) ApplyBehaviour(); } }

    /// <summary>Time offline between sessions in seconds.</summary>
    public double OfflineSeconds { get => offlineSeconds; set { if (Set(ref offlineSeconds, Math.Max(5, value))) ApplyBehaviour(); } }

    /// <summary>Interval of the chat script in seconds; 0 = off.</summary>
    public double ChatIntervalSeconds { get => chatIntervalSeconds; set { if (Set(ref chatIntervalSeconds, Math.Max(0, value))) ApplyBehaviour(); } }

    /// <summary>Chat script, one line per message (text or FLHook command).</summary>
    public string ChatScript { get => chatScript; set { if (Set(ref chatScript, value)) ApplyBehaviour(); } }

    private string[] ChatLines() =>
        ChatScript.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).ToArray();

    // Writes the behaviour settings into the options the running bots read on every tick.
    private void ApplyBehaviour(BotOptions? target = null)
    {
        var o = target ?? manager?.Options;
        if (o == null)
            return;
        o.Movement = CircleFlight ? MovementPattern.Circle : MovementPattern.Stationary;
        o.CircleRadius = CircleRadius;
        o.CircleSpeed = CircleSpeed;
        o.SessionCycle = SessionCycle;
        o.OnlineMinSeconds = OnlineMinSeconds;
        o.OnlineMaxSeconds = Math.Max(OnlineMinSeconds, OnlineMaxSeconds);
        o.OfflineSeconds = OfflineSeconds;
        o.ChatIntervalSeconds = ChatIntervalSeconds;
        o.ChatLines = ChatLines();
    }

    // ------------------------------------------------------------------------------------------ settings

    /// <summary>True while a <see cref="BotManager"/> is running; connection settings are locked then.</summary>
    public bool IsRunning => manager != null;

    /// <summary>Opposite of <see cref="IsRunning"/>, for IsEnabled bindings.</summary>
    public bool CanEditConnection => manager == null;

    /// <summary>Server address (LAN IP; FLServer does not bind to 127.0.0.1).</summary>
    public string Host { get => host; set { if (Set(ref host, value)) OnPropertyChanged(nameof(ServerDisplay)); } }

    /// <summary>Server port.</summary>
    public int Port { get => port; set { if (Set(ref port, value)) OnPropertyChanged(nameof(ServerDisplay)); } }

    /// <summary>Session password of the server (not stored).</summary>
    public string Password { get => password; set => Set(ref password, value); }

    /// <summary>Account prefix of the bots.</summary>
    public string AccountPrefix { get => accountPrefix; set => Set(ref accountPrefix, value); }

    /// <summary>Character prefix of the bots.</summary>
    public string NamePrefix { get => namePrefix; set => Set(ref namePrefix, value); }

    /// <summary>First bot number for "Start bots".</summary>
    public int StartIndex { get => startIndex; set => Set(ref startIndex, Math.Clamp(value, 1, 999)); }

    /// <summary>Number of bots for "Start bots".</summary>
    public int Count { get => count; set => Set(ref count, Math.Clamp(value, 1, 200)); }

    /// <summary>Launch automatically after every docking.</summary>
    public bool AutoLaunch { get => autoLaunch; set { if (Set(ref autoLaunch, value)) { if (manager != null) manager.Options.AutoLaunch = value; OnPropertyChanged(nameof(AutoLaunchSummary)); } } }

    /// <summary>DATA folder of the server (universe.ini) for base and system names.</summary>
    public string DataDirectory { get => dataDirectory; set => Set(ref dataDirectory, value); }

    /// <summary>Host of the FLHook admin interface.</summary>
    public string AdminHost { get => adminHost; set { if (Set(ref adminHost, value)) ResetAdminCheck(); } }

    /// <summary>Port of the FLHook admin interface.</summary>
    public int AdminPort { get => adminPort; set { if (Set(ref adminPort, value)) ResetAdminCheck(); } }

    /// <summary>Admin password (not stored).</summary>
    public string AdminPassword { get => adminPassword; set { if (Set(ref adminPassword, value)) ResetAdminCheck(); } }

    /// <summary>Path of flhook.ini.</summary>
    public string FlhookIni { get => flhookIni; set => Set(ref flhookIni, value); }

    /// <summary>Target bases for distributing, one per line.</summary>
    public string DistributeBases { get => distributeBases; set => Set(ref distributeBases, value); }

    /// <summary>Free admin command.</summary>
    public string AdminCommand { get => adminCommand; set => Set(ref adminCommand, value); }

    /// <summary>Path of the FLHook log.</summary>
    public string ServerLogPath { get => serverLogPath; set { if (Set(ref serverLogPath, value) && FollowServerLog) UpdateServerLogWatcher(); } }

    /// <summary>Follow the server log.</summary>
    public bool FollowServerLog
    {
        get => followServerLog;
        set
        {
            if (Set(ref followServerLog, value))
                UpdateServerLogWatcher();
        }
    }

    /// <summary>Only show time lags, kicks, cheat notices and errors in the server log.</summary>
    public bool ImportantOnly { get => importantOnly; set => Set(ref importantOnly, value); }

    /// <summary>Dark theme active.</summary>
    public bool IsDarkTheme
    {
        get => isDarkTheme;
        set
        {
            if (Set(ref isDarkTheme, value))
            {
                ThemeChanged?.Invoke(value);
                OnPropertyChanged(nameof(ThemeIcon));
                SaveSettings();
            }
        }
    }

    /// <summary>Icon of the theme switch (sun or moon from Segoe MDL2 Assets).</summary>
    public string ThemeIcon => IsDarkTheme ? "" : "";

    /// <summary>Active UI language (<c>en</c> or <c>de</c>).</summary>
    public string Language => language;

    /// <summary>German active (otherwise English).</summary>
    public bool IsGerman
    {
        get => language == "de";
        set
        {
            string next = value ? "de" : "en";
            if (next == language)
                return;
            language = next;
            LanguageChanged?.Invoke(language);
            foreach (var row in Bots)
                row.RefreshTexts();
            OnPropertyChanged();
            OnPropertyChanged(nameof(Language));
            OnPropertyChanged(nameof(LanguageCode));
            NotifyInteraction();
            SaveSettings();
        }
    }

    /// <summary>Short code of the active language for the header button.</summary>
    public string LanguageCode => language.ToUpperInvariant();

    /// <summary>Text in the chat input box.</summary>
    public string ChatInput { get => chatInput; set => Set(ref chatInput, value); }

    /// <summary>Chat to all bots instead of only the selected ones.</summary>
    public bool ChatToAll { get => chatToAll; set => Set(ref chatToAll, value); }

    /// <summary>Only fill the activity log for selected bots.</summary>
    public bool ActivitySelectedOnly { get => activitySelectedOnly; set => Set(ref activitySelectedOnly, value); }

    // ------------------------------------------------------------------------------------------ figures

    /// <summary>Server and port for the header.</summary>
    public string ServerDisplay => $"{Host}:{Port}";

    /// <summary>Connected bots (without disconnected or failed ones).</summary>
    public int OnlineCount { get => onlineCount; private set => Set(ref onlineCount, value); }

    /// <summary>Bots in space.</summary>
    public int InSpaceCount { get => inSpaceCount; private set => Set(ref inSpaceCount, value); }

    /// <summary>Docked bots.</summary>
    public int DockedCount { get => dockedCount; private set => Set(ref dockedCount, value); }

    /// <summary>Bots with an error.</summary>
    public int FailedCount { get => failedCount; private set => Set(ref failedCount, value); }

    /// <summary>Time lag entries in the server log since following started.</summary>
    public int TimeLagCount { get => timeLagCount; private set => Set(ref timeLagCount, value); }

    /// <summary>Kick/disconnect entries in the server log.</summary>
    public int KickCount { get => kickCount; private set => Set(ref kickCount, value); }

    /// <summary>Cheat notices in the server log.</summary>
    public int CheatCount { get => cheatCount; private set => Set(ref cheatCount, value); }

    /// <summary>Errors in the server log.</summary>
    public int ErrorCount { get => errorCount; private set => Set(ref errorCount, value); }

    /// <summary>Last status message for the footer; empty means "ready".</summary>
    public string StatusText
    {
        get => statusText.Length > 0 ? statusText : Loc.T("Msg.Ready");
        private set => Set(ref statusText, value);
    }

    /// <summary>Version shown in the footer (set by MinVer from the git tag, without build metadata).</summary>
    public string AppVersion { get; } = "v" + (typeof(MainViewModel).Assembly
        .GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
        .OfType<System.Reflection.AssemblyInformationalVersionAttribute>()
        .FirstOrDefault()?.InformationalVersion.Split('+')[0] ?? "0.0.0");

    // ------------------------------------------------------------------------------------------ fleet

    private bool HasSelection() => Bots.Any(b => b.IsSelected);

    private List<int> SelectedIndices() => Bots.Where(b => b.IsSelected).Select(b => b.Index).ToList();

    private void StartBots()
    {
        if (manager == null)
        {
            LoadUniverse();
            var options = new BotOptions
            {
                Host = Host.Trim(),
                Port = Port,
                Password = Password,
                AccountPrefix = AccountPrefix.Trim(),
                NamePrefix = NamePrefix.Trim(),
                AutoLaunch = AutoLaunch,
                Universe = universe,
                Faction = Faction.Trim(),
                Base = NewCharBase.Trim(),
                Package = Package.Trim(),
                Pilot = Pilot.Trim(),
            };
            ApplyBehaviour(options);
            manager = new BotManager(options);
            manager.Logged += (bot, level, text) =>
                activityQueue.Enqueue((bot?.Index, Entry(bot?.CharName ?? Loc.T("Msg.System"), text, ToneOf(level))));
            manager.ChatReceived += (bot, msg) => chatQueue.Enqueue(Entry(bot.CharName, msg.Text, Tone.Normal));
            OnPropertyChanged(nameof(IsRunning));
            OnPropertyChanged(nameof(CanEditConnection));
        }
        int first = StartIndex, n = Count;
        manager.Post(m => m.AddBots(first, n));
        Status(Loc.T("Msg.BotsConnecting", n, first));
        StartIndex = Math.Min(999, first + n);
        SaveSettings();
    }

    private void DisconnectAll()
    {
        if (manager == null)
            return;
        manager.Dispose();
        manager = null;
        Bots.Clear();
        rowsByIndex.Clear();
        OnPropertyChanged(nameof(IsRunning));
        OnPropertyChanged(nameof(CanEditConnection));
        Status(Loc.T("Msg.AllLoggedOut"));
    }

    private void ForSelected(Action<BotManager, Bot> action, string actionKey)
    {
        var selected = SelectedIndices();
        if (manager == null || selected.Count == 0)
            return;
        manager.Post(m =>
        {
            foreach (int i in selected)
            {
                if (m.Find(i) is { } bot)
                    action(m, bot);
            }
        });
        Status(Loc.T("Msg.ActionDone", Loc.T(actionKey), selected.Count));
    }

    // Loads base/system names and fills the dock picker. Quiet at startup: no log lines (the UI is not up yet).
    private void LoadUniverse(bool quiet = false)
    {
        universe = null;
        BaseList.Clear();
        if (DataDirectory.Trim().Length == 0)
            return;
        try
        {
            universe = UniverseNames.Load(DataDirectory.Trim());
            foreach (string b in universe.Bases.OrderBy(b => b, StringComparer.OrdinalIgnoreCase))
                BaseList.Add(b);
            if (!quiet)
                AddActivity(null, Entry(Loc.T("Msg.System"), Loc.T("Msg.UniverseLoaded", universe.Bases.Count), Tone.Muted));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            if (!quiet)
                AddActivity(null, Entry(Loc.T("Msg.System"), Loc.T("Msg.UniverseFailed", e.Message), Tone.Warning));
        }
    }

    // "Dock at base": FLHook beam lands the ship at any base, in this or another system. A real approach to
    // the docking ring would need the solar positions and actual flight.
    private void DockSelected()
    {
        RequestFleetAction?.Invoke(false);
    }

    // ------------------------------------------------------------------------------------------ comms

    private void SendChat()
    {
        string text = ChatInput.Trim();
        if (manager == null || text.Length == 0)
            return;
        bool all = ChatToAll;
        var selected = SelectedIndices();
        if (!all && selected.Count == 0)
        {
            AddFeed(Chat, Entry(Loc.T("Msg.Hint"), Loc.T("Msg.NoSelection"), Tone.Warning));
            return;
        }
        manager.Post(m =>
        {
            foreach (var bot in m.Bots.Where(b => all || selected.Contains(b.Index)))
                bot.Say(text);
        });
        AddFeed(Chat, Entry(all ? Loc.T("Msg.ChatToAll") : Loc.T("Msg.ChatToSome", selected.Count), text, Tone.Accent));
        ChatInput = "";
    }

    // ------------------------------------------------------------------------------------------ command

    private void LoadAdminPassword()
    {
        try
        {
            string? pw = FLHookAdminClient.ReadPasswordFromIni(FlhookIni.Trim());
            AdminPassword = pw ?? "";
            AddFeed(AdminOutput, Entry(Loc.T("Msg.System"), Loc.T(pw != null ? "Msg.PasswordLoaded" : "Msg.NoPass0"), Tone.Muted));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            AddFeed(AdminOutput, Entry(Loc.T("Msg.System"), Loc.T("Msg.IniUnreadable", e.Message), Tone.Danger));
        }
    }

    // Admin commands run in the background so the UI does not freeze on a slow answer.
    private void RunAdmin(params string[] commands)
    {
        if (commands.Length == 0)
            return;
        string h = AdminHost.Trim(), pw = AdminPassword;
        int p = AdminPort;
        string system = Loc.T("Msg.System"), failed = Loc.T("Msg.AdminFailed", "{0}");
        SaveSettings();
        Task.Run(() =>
        {
            try
            {
                using var admin = new FLHookAdminClient(h, p, pw);
                foreach (string cmd in commands)
                {
                    adminQueue.Enqueue(Entry(">", cmd, Tone.Accent));
                    string answer = admin.Execute(cmd).TrimEnd();
                    bool ok = answer.EndsWith("OK", StringComparison.Ordinal);
                    foreach (string line in answer.Split('\n').Select(l => l.TrimEnd('\r')).Where(l => l.Length > 0 && !l.StartsWith("---", StringComparison.Ordinal)))
                        adminQueue.Enqueue(Entry("", line, line.StartsWith("ERR", StringComparison.Ordinal) ? Tone.Danger : ok ? Tone.Normal : Tone.Warning));
                }
            }
            catch (Exception e)
            {
                adminQueue.Enqueue(Entry(system, failed.Replace("{0}", e.Message), Tone.Danger));
            }
        });
    }

    private void KickSelected() =>
        RunAdmin(Bots.Where(b => b.IsSelected).Select(b => $"kick {b.Name} Test").ToArray());

    // ------------------------------------------------------------------------------------------ server log

    private void UpdateServerLogWatcher()
    {
        logWatcher?.Dispose();
        logWatcher = null;
        OnPropertyChanged(nameof(LogMonitoringSummary));
        if (!FollowServerLog)
            return;
        string path = ServerLogPath.Trim();
        if (!File.Exists(path))
        {
            AddFeed(ServerLog, Entry(Loc.T("Msg.System"), Loc.T("Msg.FileNotFound", path), Tone.Danger));
            followServerLog = false;
            OnPropertyChanged(nameof(FollowServerLog));
            OnPropertyChanged(nameof(LogMonitoringSummary));
            return;
        }
        SaveSettings();
        logWatcher = new ServerLogWatcher(path, TimeSpan.FromMilliseconds(500));
        logWatcher.LineAdded += (line, kind) => serverLogQueue.Enqueue((Entry("", line, kind switch
        {
            LogLineKind.TimeLag => Tone.Warning,
            LogLineKind.Kick => Tone.Danger,
            LogLineKind.Cheat => Tone.Special,
            LogLineKind.Error => Tone.Danger,
            _ => Tone.Muted,
        }), kind));
        AddFeed(ServerLog, Entry(Loc.T("Msg.System"), Loc.T("Msg.Following", Path.GetFileName(path)), Tone.Accent));
    }

    // ------------------------------------------------------------------------------------------ refresh

    private void Refresh()
    {
        var snaps = manager?.Snapshots ?? Array.Empty<BotSnapshot>();
        foreach (var s in snaps)
        {
            if (!rowsByIndex.TryGetValue(s.Index, out var row))
            {
                row = new BotRowViewModel(s.Index, s.Name);
                rowsByIndex[s.Index] = row;
                Bots.Add(row);
            }
            row.Update(s, IdName);
        }
        var present = snaps.Select(s => s.Index).ToHashSet();
        foreach (var gone in rowsByIndex.Keys.Where(i => !present.Contains(i)).ToList())
        {
            Bots.Remove(rowsByIndex[gone]);
            rowsByIndex.Remove(gone);
        }

        OnlineCount = snaps.Count(s => s.State is not (BotState.Failed or BotState.Disconnected or BotState.Offline));
        InSpaceCount = snaps.Count(s => s.State == BotState.InSpace);
        DockedCount = snaps.Count(s => s.State == BotState.InBase);
        FailedCount = snaps.Count(s => s.State == BotState.Failed);
        UpdateInteractionRows();

        var selected = ActivitySelectedOnly ? SelectedIndices() : null;
        while (activityQueue.TryDequeue(out var a))
        {
            if (selected == null || a.Bot == null || selected.Contains(a.Bot.Value))
                AddActivity(a.Bot, a.Entry);
        }
        while (chatQueue.TryDequeue(out var c))
            AddFeed(Chat, c);
        while (adminQueue.TryDequeue(out var line))
        {
            AddFeed(AdminOutput, line);
            AddActivity(null, line);
        }
        while (serverLogQueue.TryDequeue(out var l))
        {
            switch (l.Kind)
            {
                case LogLineKind.TimeLag: TimeLagCount++; break;
                case LogLineKind.Kick: KickCount++; break;
                case LogLineKind.Cheat: CheatCount++; break;
                case LogLineKind.Error: ErrorCount++; break;
            }
            if (!ImportantOnly || l.Kind != LogLineKind.Normal)
                AddFeed(ServerLog, l.Entry);
        }
    }

    private void AddActivity(int? bot, FeedEntry entry)
    {
        AddFeed(Activity, entry);
        if (bot == null || entry.Tone is Tone.Danger or Tone.Success)
            StatusText = $"{entry.Time}  {entry.Source}: {entry.Text}";
    }

    private static void AddFeed(ObservableCollection<FeedEntry> feed, FeedEntry entry)
    {
        feed.Add(entry);
        while (feed.Count > MaxFeedEntries)
            feed.RemoveAt(0);
    }

    private void Status(string text) => StatusText = $"{DateTime.Now:HH:mm:ss}  {text}";

    private static FeedEntry Entry(string source, string text, Tone tone) => new(DateTime.Now.ToString("HH:mm:ss"), source, text, tone);

    private static Tone ToneOf(LogLevel level) => level switch
    {
        LogLevel.Error => Tone.Danger,
        LogLevel.Success => Tone.Success,
        LogLevel.Event => Tone.Accent,
        _ => Tone.Normal,
    };

    private string IdName(uint id) => id == 0 ? "" : universe?.Name(id) ?? $"0x{id:X8}";

    // FLServer only binds to real interfaces, not to 127.0.0.1.
    private static string GuessLocalAddress() =>
        Dns.GetHostAddresses(Dns.GetHostName())
            .FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(a))?.ToString() ?? "127.0.0.1";

    /// <summary>Saves the current inputs (without passwords).</summary>
    public void SaveSettings()
    {
        settings.Host = Host.Trim();
        settings.Port = Port;
        settings.AccountPrefix = AccountPrefix.Trim();
        settings.NamePrefix = NamePrefix.Trim();
        settings.StartIndex = StartIndex;
        settings.Count = Count;
        settings.AutoLaunch = AutoLaunch;
        settings.DataDirectory = DataDirectory.Trim();
        settings.AdminHost = AdminHost.Trim();
        settings.AdminPort = AdminPort;
        settings.FlhookIni = FlhookIni.Trim();
        settings.DistributeBases = DistributeBases;
        settings.ServerLog = ServerLogPath.Trim();
        settings.DarkTheme = IsDarkTheme;
        settings.Language = language;
        settings.Faction = Faction.Trim();
        settings.NewCharBase = NewCharBase.Trim();
        settings.Package = Package.Trim();
        settings.Pilot = Pilot.Trim();
        settings.CircleFlight = CircleFlight;
        settings.CircleRadius = CircleRadius;
        settings.CircleSpeed = CircleSpeed;
        settings.SessionCycle = SessionCycle;
        settings.OnlineMinSeconds = OnlineMinSeconds;
        settings.OnlineMaxSeconds = OnlineMaxSeconds;
        settings.OfflineSeconds = OfflineSeconds;
        settings.ChatIntervalSeconds = ChatIntervalSeconds;
        settings.ChatScript = ChatScript;
        settings.DockTarget = DockTarget.Trim();
        persistSettings(settings);
    }

    /// <summary>Saves the settings, stops following the log and logs out all bots.</summary>
    public void Dispose()
    {
        SaveSettings();
        timer.Stop();
        logWatcher?.Dispose();
        manager?.Dispose();
        manager = null;
    }
}
