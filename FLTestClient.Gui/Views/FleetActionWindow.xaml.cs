using System.Windows;
using System.Windows.Controls;
using FLTestClient.Gui.ViewModels;

namespace FLTestClient.Gui.Views;

/// <summary>Explicit selection and target preview before beaming bots.</summary>
public partial class FleetActionWindow : Window
{
    private readonly MainViewModel vm;
    private readonly bool distribute;
    private readonly int[] selected;
    private FleetActionPlan? plan;
    private bool ready;

    /// <summary>Creates a relocation preview for a captured selection.</summary>
    /// <param name="vm">Fleet context.</param>
    /// <param name="distribute">True for multiple target bases.</param>
    public FleetActionWindow(MainViewModel vm, bool distribute)
    {
        this.vm = vm;
        this.distribute = distribute;
        selected = vm.Bots.Where(b => b.IsSelected).Select(b => b.Index).ToArray();
        InitializeComponent();
        Title = Heading.Text = Loc.T(distribute ? "Fleet.Distribute" : "Fleet.Dock");
        SingleBase.ItemsSource = vm.BaseList;
        SingleBase.Text = vm.DockTarget;
        SingleBase.Visibility = distribute ? Visibility.Collapsed : Visibility.Visible;
        TargetBases.Visibility = distribute ? Visibility.Visible : Visibility.Collapsed;
        TargetBases.Text = vm.DistributeBases;
        SingleBase.AddHandler(TextBox.TextChangedEvent, new TextChangedEventHandler((_, _) => { if (ready) UpdatePreview(); }));
        ready = true;
        SourceInitialized += (_, _) => App.SetDarkTitleBar(this, vm.IsDarkTheme);
        UpdatePreview();
    }

    private void TargetsChanged(object sender, TextChangedEventArgs e) { if (ready) UpdatePreview(); }
    private void InputsChanged(object sender, System.Windows.Input.KeyboardFocusChangedEventArgs e) { if (ready) UpdatePreview(); }
    private void RefreshPreview(object sender, RoutedEventArgs e) => UpdatePreview();

    private void UpdatePreview()
    {
        plan = null;
        Execute.IsEnabled = false;
        Preview.ItemsSource = null;
        Endpoint.Text = Loc.T("Action.Endpoint", vm.AdminHost, vm.AdminPort) + " · " + vm.AdminCheckSummary;
        LaunchHint.Text = vm.AutoLaunchSummary;
        try
        {
            plan = FleetActionPlan.Create(vm.Bots.Where(b => selected.Contains(b.Index)), distribute ? TargetBases.Text : SingleBase.Text);
            Preview.ItemsSource = plan.Assignments;
            int missing = selected.Count(i => !vm.Bots.Any(b => b.Index == i));
            Summary.Text = Loc.T("Action.Preview", plan.Assignments.Count, plan.Skipped + missing);
            Execute.IsEnabled = plan.Assignments.Count > 0;
        }
        catch (ArgumentException e) { Summary.Text = Loc.T(e.Message); }
    }

    private void ExecutePlan(object sender, RoutedEventArgs e)
    {
        // Keep execution tied to the visible plan; editing the focused ComboBox must first refresh it.
        string targets = distribute ? TargetBases.Text : SingleBase.Text;
        FleetActionPlan current;
        try { current = FleetActionPlan.Create(vm.Bots.Where(b => selected.Contains(b.Index)), targets); }
        catch (ArgumentException) { UpdatePreview(); return; }
        if (plan == null || !current.Assignments.SequenceEqual(plan.Assignments) || !vm.ExecuteFleetPlan(plan))
        {
            UpdatePreview();
            Summary.Text = Loc.T("Action.Changed") + " " + Summary.Text;
            return;
        }
        if (distribute) vm.DistributeBases = targets;
        else vm.DockTarget = targets;
        DialogResult = true;
    }
}
