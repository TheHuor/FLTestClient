using System.Windows.Controls;

namespace FLTestClient.Gui.Views;

/// <summary>"Fleet" page: start, select and control bots, activity log.</summary>
public partial class FleetView : UserControl
{
    /// <summary>Creates the page.</summary>
    public FleetView() => InitializeComponent();
}

/// <summary>"Comms" page: chat of the bots and sending text or FLHook commands.</summary>
public partial class CommsView : UserControl
{
    /// <summary>Creates the page.</summary>
    public CommsView() => InitializeComponent();
}

/// <summary>"Command" page: FLHook admin interface and distributing bots.</summary>
public partial class CommandView : UserControl
{
    /// <summary>Creates the page.</summary>
    public CommandView() => InitializeComponent();
}

/// <summary>"Server log" page: live FLHook.log with counters.</summary>
public partial class ServerLogView : UserControl
{
    /// <summary>Creates the page.</summary>
    public ServerLogView() => InitializeComponent();
}

/// <summary>"Settings" page: server, bot identity, paths, appearance and language.</summary>
public partial class SettingsView : UserControl
{
    /// <summary>Creates the page.</summary>
    public SettingsView() => InitializeComponent();
}
