using System.Windows.Controls;

namespace FLTestClient.Gui.Views;

/// <summary>"Fleet" page: start, select and control bots, activity log.</summary>
public partial class FleetView : UserControl
{
    /// <summary>Creates the page.</summary>
    public FleetView()
    {
        InitializeComponent();
    }
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

    private void BrowsePath(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is not ViewModels.MainViewModel vm || sender is not Button button) return;
        if ((string)button.Tag == "data")
        {
            var picker = new Microsoft.Win32.OpenFolderDialog();
            if (picker.ShowDialog(System.Windows.Window.GetWindow(this)) == true) vm.DataDirectory = picker.FolderName;
        }
        else
        {
            var picker = new Microsoft.Win32.OpenFileDialog { Filter = (string)button.Tag == "ini" ? "FLHook INI|*.ini|All files|*.*" : "Server log|*.log|All files|*.*" };
            if (picker.ShowDialog(System.Windows.Window.GetWindow(this)) == true)
            {
                if ((string)button.Tag == "ini") vm.FlhookIni = picker.FileName;
                else vm.ServerLogPath = picker.FileName;
            }
        }
    }
}

/// <summary>Global live bot behaviour.</summary>
public partial class BehaviourView : UserControl
{
    /// <summary>Creates the view.</summary>
    public BehaviourView() => InitializeComponent();
}

/// <summary>Expandable bot selection scope.</summary>
public partial class SelectionPanel : UserControl
{
    /// <summary>Creates the view.</summary>
    public SelectionPanel() => InitializeComponent();
}

/// <summary>Bot and server diagnostics.</summary>
public partial class DiagnosticsView : UserControl
{
    /// <summary>Creates the view.</summary>
    public DiagnosticsView() => InitializeComponent();
}

/// <summary>Focused-bot details, scoped chat and actions.</summary>
public partial class BotInspector : UserControl
{
    /// <summary>Creates the inspector.</summary>
    public BotInspector() => InitializeComponent();
}
