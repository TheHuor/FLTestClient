using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;

namespace FLTestClient.Gui.ViewModels;

public sealed partial class MainViewModel
{
    private string fleetSearch = "";
    private int fleetStateFilter;
    private readonly HashSet<BotRowViewModel> observedRows = new();
    private string adminCheckKey = "Admin.Unchecked", adminCheckTime = "";
    private int adminCheckGeneration;
    private bool adminCheckBusy;
    private bool autoScrollLogs = true;

    /// <summary>Raised to open relocation (false) or distribution (true) preview.</summary>
    public event Action<bool>? RequestFleetAction;
    /// <summary>Filtered fleet rows.</summary>
    public ICollectionView FleetRows { get; private set; } = null!;
    /// <summary>Name, number or system search. Filtering clears selection to avoid hidden targets.</summary>
    public string FleetSearch { get => fleetSearch; set { if (Set(ref fleetSearch, value)) RefreshFleetFilter(); } }
    /// <summary>0 = all, 1 = in space, 2 = docked, 3 = failed.</summary>
    public int FleetStateFilter { get => fleetStateFilter; set { if (Set(ref fleetStateFilter, value)) RefreshFleetFilter(); } }
    /// <summary>Selected bot count.</summary>
    public int SelectedCount => Bots.Count(b => b.IsSelected);
    /// <summary>Readable scope with eligible state counts.</summary>
    public string SelectionSummary => Loc.T("Selection.Summary", SelectedCount, Bots.Count(b => b.IsSelected && b.State == BotState.InSpace), Bots.Count(b => b.IsSelected && b.State == BotState.InBase));
    /// <summary>Selected bot names for the expandable scope panel.</summary>
    public string SelectionNames => SelectedCount == 0 ? Loc.T("Selection.None") : string.Join(", ", Bots.Where(b => b.IsSelected).Select(b => b.Name));
    /// <summary>Effective automatic-launch behaviour.</summary>
    public string AutoLaunchSummary => Loc.T(AutoLaunch ? "Action.AutoLaunchOn" : "Action.AutoLaunchOff");
    /// <summary>Last authentication check, never a persistent connection indicator.</summary>
    public string AdminCheckSummary => Loc.T(adminCheckKey, adminCheckTime);
    /// <summary>Authenticates against the configured admin endpoint without changing server state.</summary>
    public ICommand CheckAdminCommand { get; private set; } = null!;
    /// <summary>Deselects all bots, including any outside the current filter.</summary>
    public ICommand ClearSelectionCommand { get; private set; } = null!;
    /// <summary>Selects only visible filtered bots.</summary>
    public ICommand SelectVisibleCommand { get; private set; } = null!;
    /// <summary>Whether diagnostics follow newly appended entries visually.</summary>
    public bool AutoScrollLogs { get => autoScrollLogs; set => Set(ref autoScrollLogs, value); }
    /// <summary>Current file-monitoring state; zero counts alone do not establish health.</summary>
    public string LogMonitoringSummary => Loc.T(FollowServerLog ? "Log.Monitoring" : "Log.NotMonitoring");

    private void InitializeInteraction()
    {
        FleetRows = new ListCollectionView(Bots);
        FleetRows.Filter = b => MatchesFleet((BotRowViewModel)b);
        Bots.CollectionChanged += OnBotsChanged;
        CheckAdminCommand = new RelayCommand(CheckAdmin, () => !adminCheckBusy && AdminHost.Trim().Length > 0 && AdminPort is > 0 and <= 65535);
        ClearSelectionCommand = new RelayCommand(() => { foreach (var b in Bots) b.IsSelected = false; });
        SelectVisibleCommand = new RelayCommand(() => { foreach (var b in Bots) b.IsSelected = MatchesFleet(b); });
    }

    private bool MatchesFleet(BotRowViewModel b) =>
        (FleetStateFilter switch { 1 => b.State == BotState.InSpace, 2 => b.State == BotState.InBase, 3 => b.State == BotState.Failed, _ => true }) &&
        (fleetSearch.Trim().Length == 0 || b.Name.Contains(fleetSearch.Trim(), StringComparison.OrdinalIgnoreCase) ||
         b.Index.ToString().Contains(fleetSearch.Trim(), StringComparison.OrdinalIgnoreCase) || b.System.Contains(fleetSearch.Trim(), StringComparison.OrdinalIgnoreCase));

    private void RefreshFleetFilter()
    {
        foreach (var b in Bots) b.IsSelected = false;
        FleetRows.Refresh();
        ValidateFocusedBot();
        NotifyInteraction();
    }

    private void OnBotsChanged(object? sender, NotifyCollectionChangedEventArgs e) => UpdateInteractionRows();

    private void UpdateInteractionRows()
    {
        foreach (var b in observedRows.Where(b => !Bots.Contains(b)).ToArray())
        {
            b.PropertyChanged -= OnRowChanged;
            observedRows.Remove(b);
        }
        foreach (var b in Bots.Where(b => !observedRows.Contains(b)))
        {
            b.PropertyChanged += OnRowChanged;
            observedRows.Add(b);
        }
        ValidateFocusedBot();
        NotifyInteraction();
    }

    private void OnRowChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(BotRowViewModel.State) or nameof(BotRowViewModel.System))
        {
            if (sender is BotRowViewModel b && !MatchesFleet(b)) b.IsSelected = false;
            FleetRows.Refresh();
            ValidateFocusedBot();
        }
        if (e.PropertyName is nameof(BotRowViewModel.IsSelected) or nameof(BotRowViewModel.State)) NotifyInteraction();
    }

    private void NotifyInteraction()
    {
        OnPropertyChanged(nameof(SelectedCount));
        OnPropertyChanged(nameof(SelectionSummary));
        OnPropertyChanged(nameof(SelectionNames));
        OnPropertyChanged(nameof(AutoLaunchSummary));
        OnPropertyChanged(nameof(AdminCheckSummary));
        OnPropertyChanged(nameof(LogMonitoringSummary));
        CommandManager.InvalidateRequerySuggested();
    }

    /// <summary>Executes exactly the reviewed assignments if their current states still permit it.</summary>
    /// <param name="plan">Captured preview.</param>
    /// <returns>False if a bot has disappeared or left space; the caller must refresh the preview.</returns>
    public bool ExecuteFleetPlan(FleetActionPlan plan)
    {
        if (plan.Assignments.Count == 0 || plan.Assignments.Any(a => !Bots.Any(b => b.Index == a.Index && b.Name == a.Name && b.State == BotState.InSpace)))
            return false;
        RunAdmin(plan.Assignments.Select(a => a.Command).ToArray());
        Status(Loc.T("Action.Submitted", plan.Assignments.Count));
        return true;
    }

    private void ResetAdminCheck()
    {
        adminCheckGeneration++;
        adminCheckKey = "Admin.Unchecked";
        OnPropertyChanged(nameof(AdminCheckSummary));
    }

    private async void CheckAdmin()
    {
        if (adminCheckBusy) return;
        int generation = adminCheckGeneration;
        string host = AdminHost.Trim(), password = AdminPassword;
        int port = AdminPort;
        adminCheckBusy = true;
        adminCheckKey = "Admin.Checking";
        NotifyInteraction();
        try
        {
            await Task.Run(() => { using var client = new FLHookAdminClient(host, port, password); });
            if (generation == adminCheckGeneration) adminCheckKey = "Admin.Checked";
        }
        catch (Exception e)
        {
            if (generation == adminCheckGeneration)
            {
                adminCheckKey = "Admin.Failed";
                AddFeed(AdminOutput, Entry(Loc.T("Msg.System"), Loc.T("Msg.AdminFailed", e.Message), Tone.Danger));
            }
        }
        finally
        {
            adminCheckTime = DateTime.Now.ToString("HH:mm:ss");
            adminCheckBusy = false;
            NotifyInteraction();
        }
    }
}
