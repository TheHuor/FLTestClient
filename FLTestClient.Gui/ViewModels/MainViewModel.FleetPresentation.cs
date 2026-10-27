using System.ComponentModel;
using System.Windows.Data;
using System.Windows.Input;

namespace FLTestClient.Gui.ViewModels;

public sealed partial class MainViewModel
{
    private BotRowViewModel? focusedBot;
    private bool useCardView = true, isInspectorOpen;
    private int mainPageIndex, settingsPageIndex, inspectorPageIndex;
    private string focusedChatInput = "";

    /// <summary>Raised to open the compact bot creation form.</summary>
    public event Action? RequestAddBots;
    /// <summary>Active main navigation page.</summary>
    public int MainPageIndex { get => mainPageIndex; set => Set(ref mainPageIndex, value); }
    /// <summary>Active visible settings tab.</summary>
    public int SettingsPageIndex { get => settingsPageIndex; set => Set(ref settingsPageIndex, value); }
    /// <summary>Active inspector tab: details, chat, actions.</summary>
    public int InspectorPageIndex { get => inspectorPageIndex; set => Set(ref inspectorPageIndex, value); }
    /// <summary>Compact cards or the original table; does not change selection.</summary>
    public bool UseCardView { get => useCardView; set => Set(ref useCardView, value); }
    /// <summary>Whether the inspector is expanded.</summary>
    public bool IsInspectorOpen { get => isInspectorOpen; set { if (Set(ref isInspectorOpen, value)) OnPropertyChanged(nameof(ShowInspector)); } }
    /// <summary>True only when an existing visible bot is focused and the inspector is expanded.</summary>
    public bool ShowInspector => IsInspectorOpen && FocusedBot != null;
    /// <summary>Bot inspected independently from the bulk selection.</summary>
    public BotRowViewModel? FocusedBot
    {
        get => focusedBot;
        set
        {
            if (value != null && (!Bots.Contains(value) || !MatchesFleet(value))) return;
            if (Set(ref focusedBot, value))
            {
                FocusedChatInput = "";
                FocusedChat?.Refresh();
                OnPropertyChanged(nameof(ShowInspector));
                OnPropertyChanged(nameof(FocusedRelocationSummary));
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }
    /// <summary>Feed of the focused bot only.</summary>
    public ICollectionView FocusedChat { get; private set; } = null!;
    /// <summary>Text sent by the explicitly named inspector bot.</summary>
    public string FocusedChatInput { get => focusedChatInput; set => Set(ref focusedChatInput, value); }
    /// <summary>Relocation eligibility of the focused bot and entered target.</summary>
    public string FocusedRelocationSummary
    {
        get
        {
            if (FocusedBot == null) return Loc.T("Inspector.SelectBot");
            if (FocusedBot.State != BotState.InSpace) return Loc.T("Inspector.NotInSpace");
            try { CreateFocusedRelocationPlan(); return Loc.T("Inspector.RelocationScope", FocusedBot.Name); }
            catch (ArgumentException e) { return Loc.T(e.Message); }
        }
    }
    /// <summary>Opens bot creation.</summary>
    public ICommand ShowAddBotsCommand { get; private set; } = null!;
    /// <summary>Opens settings at the connection tab.</summary>
    public ICommand OpenConnectionSettingsCommand { get; private set; } = null!;
    /// <summary>Inspects one bot without changing bulk selection.</summary>
    public ICommand FocusBotCommand { get; private set; } = null!;
    /// <summary>Opens actions for the bot parameter.</summary>
    public ICommand ShowBotActionsCommand { get; private set; } = null!;
    /// <summary>Opens chat for the bot parameter.</summary>
    public ICommand ShowBotChatCommand { get; private set; } = null!;
    /// <summary>Closes the inspector without changing action scope.</summary>
    public ICommand CloseInspectorCommand { get; private set; } = null!;
    /// <summary>Launches only the explicitly supplied docked bot.</summary>
    public ICommand BotLaunchCommand { get; private set; } = null!;
    /// <summary>Reconnects only the explicitly supplied bot.</summary>
    public ICommand BotReconnectCommand { get; private set; } = null!;
    /// <summary>Disconnects and removes only the explicitly supplied bot.</summary>
    public ICommand BotRemoveCommand { get; private set; } = null!;
    /// <summary>Relocates only the bot named in the inspector.</summary>
    public ICommand RelocateFocusedCommand { get; private set; } = null!;
    /// <summary>Sends chat through only the bot named in the inspector.</summary>
    public ICommand SendFocusedChatCommand { get; private set; } = null!;

    private void InitializeFleetPresentation()
    {
        useCardView = settings.CardsView;
        FocusedChat = new ListCollectionView(Chat) { Filter = item => FocusedBot != null && ((FeedEntry)item).Source == FocusedBot.Name };
        ShowAddBotsCommand = new RelayCommand(() => RequestAddBots?.Invoke());
        OpenConnectionSettingsCommand = new RelayCommand(() => { SettingsPageIndex = 0; MainPageIndex = 5; });
        FocusBotCommand = new RelayCommand(p => Inspect(p, 0), HasBot);
        ShowBotActionsCommand = new RelayCommand(p => Inspect(p, 2), HasBot);
        ShowBotChatCommand = new RelayCommand(p => Inspect(p, 1), HasBot);
        CloseInspectorCommand = new RelayCommand(() => IsInspectorOpen = false);
        BotLaunchCommand = new RelayCommand(p => ForBot(p, (_, bot) => bot.Launch(), "Act.Launch"),
            p => HasBot(p) && ((BotRowViewModel)p!).State == BotState.InBase);
        BotReconnectCommand = new RelayCommand(p => ForBot(p, (_, bot) => { bot.Stop(); bot.Start(); }, "Act.Reconnect"), HasBot);
        BotRemoveCommand = new RelayCommand(p => ForBot(p, (m, bot) => m.Remove(bot), "Act.Disconnect"), HasBot);
        RelocateFocusedCommand = new RelayCommand(() =>
        {
            try
            {
                if (!ExecuteFleetPlan(CreateFocusedRelocationPlan())) Status(Loc.T("Action.Changed"));
            }
            catch (ArgumentException e) { Status(Loc.T(e.Message)); }
        }, CanRelocateFocused);
        SendFocusedChatCommand = new RelayCommand(SendFocusedChat,
            () => FocusedBot != null && HasBot(FocusedBot) && manager != null && FocusedChatInput.Trim().Length > 0);
    }

    private bool HasBot(object? parameter) => parameter is BotRowViewModel row && Bots.Contains(row) && MatchesFleet(row);

    private void Inspect(object? parameter, int tab)
    {
        if (!HasBot(parameter)) return;
        FocusedBot = (BotRowViewModel)parameter!;
        InspectorPageIndex = tab;
        IsInspectorOpen = true;
    }

    private void ForBot(object? parameter, Action<BotManager, Bot> action, string actionKey)
    {
        if (!HasBot(parameter) || manager == null) return;
        var row = (BotRowViewModel)parameter!;
        int index = row.Index;
        manager.Post(m => { if (m.Find(index) is { } bot) action(m, bot); });
        Status(Loc.T("Inspector.ActionRequested", Loc.T(actionKey), row.Name));
    }

    /// <summary>Creates a validated plan for the focused bot, ignoring bulk selection.</summary>
    /// <returns>Immutable plan containing at most the named bot.</returns>
    public FleetActionPlan CreateFocusedRelocationPlan() => FleetActionPlan.Create(
        FocusedBot != null && HasBot(FocusedBot) ? new[] { FocusedBot } : Array.Empty<BotRowViewModel>(), DockTarget);

    private bool CanRelocateFocused()
    {
        try { return FocusedBot != null && CreateFocusedRelocationPlan().Assignments.Count == 1; }
        catch (ArgumentException) { return false; }
    }

    private void SendFocusedChat()
    {
        var row = FocusedBot;
        if (row == null || !HasBot(row) || manager == null) return;
        string text = FocusedChatInput.Trim();
        if (text.Length == 0) return;
        int index = row.Index;
        manager.Post(m => m.Find(index)?.Say(text));
        AddFeed(Chat, Entry(row.Name, "→ " + text, Tone.Accent));
        FocusedChatInput = "";
    }

    private void ValidateFocusedBot()
    {
        if (FocusedBot != null && !HasBot(FocusedBot)) FocusedBot = null;
        OnPropertyChanged(nameof(FocusedRelocationSummary));
        OnPropertyChanged(nameof(ShowInspector));
    }
}
