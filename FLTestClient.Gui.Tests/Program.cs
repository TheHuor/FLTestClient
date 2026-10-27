using FLTestClient;
using FLTestClient.Gui;
using FLTestClient.Gui.ViewModels;
using FLTestClient.Gui.Views;
using System.Numerics;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        var app = new App();
        app.InitializeComponent();
        app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        // The test host owns its windows; suppress the production StartupUri before draining layout work.
        typeof(Application).GetField("_startupUri", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(app, null);
        int failed = 0, passed = 0;
        Run("Quick chat prepares input without sending", () =>
        {
            var vm = Context();
            Check(vm.QuickChatCommand.CanExecute("/ping"), "Quick command unavailable");
            vm.QuickChatCommand.Execute("/ping");
            Check(vm.ChatInput == "/ping" && vm.Chat.Count == 0, "Quick command sent a message");
        });
        Run("Mixed fleet distributes eligible bots in deterministic order", () =>
        {
            var plan = FleetActionPlan.Create(new[] { Row(3, BotState.InSpace), Row(2, BotState.InBase), Row(1, BotState.InSpace), Row(4, BotState.InSpace) }, " Li01_01_Base\r\n\nBr01_01_Base ");
            Check(plan.Skipped == 1, "Docked bot not skipped");
            Check(string.Join("|", plan.Assignments.Select(a => a.Command)) == "beam Bot01 Li01_01_Base|beam Bot03 Br01_01_Base|beam Bot04 Li01_01_Base", "Incorrect round-robin commands");
        });
        Run("Invalid targets never produce commands", () =>
        {
            foreach (var target in new[] { "", " \n", "Li01 Base", "Li01\rkick", "Li01;rehash", "Li01\tBase" })
                Throws(() => FleetActionPlan.Create(new[] { Row(1, BotState.InSpace) }, target));
        });
        Run("Unsafe bot names never produce commands", () =>
        {
            var row = new BotRowViewModel(1, "Bot\nrehash");
            row.Update(new BotSnapshot(1, row.Name, "account", BotState.InSpace, 1, 1, 0, 1, 0, 1, 0, Vector3.Zero), _ => "system");
            Throws(() => FleetActionPlan.Create(new[] { row }, "Li01_01_Base"));
        });
        Run("Empty fleet yields no assignments", () =>
            Check(FleetActionPlan.Create(Array.Empty<BotRowViewModel>(), "Li01_01_Base").Assignments.Count == 0, "Empty fleet received commands"));
        Run("Selection updates immediately and removing a row clears scope", () =>
        {
            var vm = Context();
            var row = Row(1, BotState.InSpace);
            vm.Bots.Add(row);
            row.IsSelected = true;
            Check(vm.SelectedCount == 1 && vm.SelectionNames.Contains("Bot01"), "Selection not propagated");
            vm.Bots.Remove(row);
            Check(vm.SelectedCount == 0, "Removed bot remains selected");
        });
        Run("Filtering clears hidden targets and select-visible respects filter", () =>
        {
            var vm = Context();
            var a = Row(1, BotState.InSpace); var b = Row(2, BotState.InBase);
            vm.Bots.Add(a); vm.Bots.Add(b); a.IsSelected = b.IsSelected = true;
            vm.FleetStateFilter = 1;
            Check(vm.SelectedCount == 0 && vm.FleetRows.Cast<object>().Count() == 1, "Hidden selection retained");
            vm.SelectVisibleCommand.Execute(null);
            Check(a.IsSelected && !b.IsSelected, "Select visible targeted hidden bot");
            a.Update(new BotSnapshot(1, "Bot01", "account", BotState.InBase, 1, 1, 1, 0, 0, 1, 0, Vector3.Zero), _ => "system");
            Check(vm.SelectedCount == 0 && vm.FleetRows.Cast<object>().Count() == 0, "State change left hidden target");
        });
        Run("Stale relocation plan is rejected without contacting server", () =>
        {
            var vm = Context(); var row = Row(1, BotState.InSpace); vm.Bots.Add(row);
            var plan = FleetActionPlan.Create(vm.Bots, "Li01_01_Base");
            vm.Bots.Clear();
            Check(!vm.ExecuteFleetPlan(plan) && vm.AdminOutput.Count == 0, "Stale plan executed");
        });
        Run("Auto-scroll notifies other views", () =>
        {
            var vm = Context(); bool notified = false;
            vm.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(vm.AutoScrollLogs)) notified = true; };
            vm.AutoScrollLogs = false;
            Check(notified, "Other log views do not observe pause");
        });
        Run("All views instantiate and lay out in both languages and themes", () =>
        {
            foreach (string lang in new[] { "en", "de" })
            foreach (string theme in new[] { "Dark", "Bright" })
            {
                app.Resources.MergedDictionaries[0] = new ResourceDictionary { Source = new Uri($"/FLTestClientGui;component/Themes/{theme}.xaml", UriKind.Relative) };
                app.Resources.MergedDictionaries[2] = new ResourceDictionary { Source = new Uri($"/FLTestClientGui;component/Languages/{lang}.xaml", UriKind.Relative) };
                foreach (UserControl view in new UserControl[] { new FleetView(), new BehaviourView(), new CommsView(), new CommandView(), new DiagnosticsView(), new SettingsView() })
                {
                    view.DataContext = Context();
                    view.Measure(new Size(850, 510)); view.Arrange(new Rect(0, 0, 850, 510)); view.UpdateLayout();
                    Check(view.DesiredSize.Width < 1200, "Unexpected horizontal overflow: " + view.GetType().Name);
                    if (view is SettingsView settings)
                    {
                        var picker = (ComboBox)settings.FindName("SectionPicker");
                        for (int section = 0; section < 5; section++)
                        {
                            picker.SelectedIndex = section;
                            view.Measure(new Size(850, 510)); view.Arrange(new Rect(0, 0, 850, 510)); view.UpdateLayout();
                        }
                    }
                }
                var vm = Context(); var row = Row(1, BotState.InSpace); vm.Bots.Add(row); row.IsSelected = true;
                foreach (bool distribute in new[] { false, true })
                {
                    var dialog = new FleetActionWindow(vm, distribute);
                    dialog.Measure(new Size(760, 680)); dialog.Arrange(new Rect(0, 0, 760, 680)); dialog.UpdateLayout();
                }
            }
        });
        Run("Relocation dialog remains available with an empty saved target", () =>
        {
            var vm = Context(); vm.DockTarget = "";
            var row = Row(1, BotState.InSpace); vm.Bots.Add(row); row.IsSelected = true;
            Check(vm.DockSelectedCommand.CanExecute(null), "Empty saved target prevents opening the target picker");
        });
        Run("Changing monitored path stops watching the old server file", () =>
        {
            var vm = Context();
            string file = System.IO.Path.GetTempFileName();
            try
            {
                vm.ServerLogPath = file; vm.FollowServerLog = true;
                Check(vm.FollowServerLog, "Temporary log could not be monitored");
                vm.ServerLogPath = file + ".missing";
                Check(!vm.FollowServerLog, "Old file still monitored under a new displayed path");
            }
            finally { vm.FollowServerLog = false; System.IO.File.Delete(file); }
        });
        Run("Edited relocation target updates visible preview before execution", () =>
        {
            var vm = Context(); vm.DockTarget = "Li01_01_Base";
            var row = Row(1, BotState.InSpace); vm.Bots.Add(row); row.IsSelected = true;
            var dialog = new FleetActionWindow(vm, false);
            dialog.Measure(new Size(760, 680)); dialog.Arrange(new Rect(0, 0, 760, 680)); dialog.UpdateLayout();
            var combo = (ComboBox)dialog.FindName("SingleBase");
            combo.ApplyTemplate();
            var editor = (TextBox)combo.Template.FindName("PART_EditableTextBox", combo);
            editor.Text = "Br01_01_Base";
            var preview = (DataGrid)dialog.FindName("Preview");
            Check(preview.Items.Cast<FleetAssignment>().Single().Base == "Br01_01_Base", "Preview remained on the old target");
        });
        if (args.Length == 2 && args[0] == "--render")
        {
            System.IO.Directory.CreateDirectory(args[1]);
            foreach (string lang in new[] { "en", "de" })
            foreach (string theme in new[] { "Dark", "Bright" })
            {
                app.Resources.MergedDictionaries[0] = new ResourceDictionary { Source = new Uri($"/FLTestClientGui;component/Themes/{theme}.xaml", UriKind.Relative) };
                app.Resources.MergedDictionaries[2] = new ResourceDictionary { Source = new Uri($"/FLTestClientGui;component/Languages/{lang}.xaml", UriKind.Relative) };
                Texts.Language = lang;
                var vm = Context();
                for (int i = 1; i <= 5; i++) { var row = Row(i, i == 2 ? BotState.InBase : BotState.InSpace); vm.Bots.Add(row); row.IsSelected = true; }
                foreach (UserControl view in new UserControl[] { new FleetView(), new BehaviourView(), new CommsView(), new CommandView(), new DiagnosticsView(), new SettingsView() })
                {
                    view.DataContext = vm;
                    Capture(view, 860, 520, System.IO.Path.Combine(args[1], $"{view.GetType().Name}-{lang}-{theme}.png"));
                    if (view is SettingsView settings)
                    {
                        for (int section = 1; section < 5; section++)
                        {
                            ((ComboBox)settings.FindName("SectionPicker")).SelectedIndex = section;
                            Capture(view, 860, 520, System.IO.Path.Combine(args[1], $"Settings-{section}-{lang}-{theme}.png"));
                        }
                    }
                    if (view is DiagnosticsView diagnostics)
                    {
                        ((RadioButton)diagnostics.FindName("ServerTab")).IsChecked = true;
                        Capture(view, 860, 520, System.IO.Path.Combine(args[1], $"ServerLog-{lang}-{theme}.png"));
                    }
                }
                var dialog = new FleetActionWindow(vm, true);
                Capture((FrameworkElement)dialog.Content, 720, 610, System.IO.Path.Combine(args[1], $"Distribution-{lang}-{theme}.png"));
            }
        }
        Console.WriteLine($"{passed} passed, {failed} failed");
        return failed == 0 ? 0 : 1;
        void Run(string name, Action test)
        {
            try { test(); passed++; Console.WriteLine("PASS " + name); }
            catch (Exception e) { failed++; Console.WriteLine("FAIL " + name + ": " + e); }
        }
    }
    private static void Capture(FrameworkElement view, int width, int height, string file)
    {
        view.Measure(new Size(width, height)); view.Arrange(new Rect(0, 0, width, height)); view.UpdateLayout();
        var host = new Grid { Background = (Brush)Application.Current.FindResource("WindowBackground") };
        if (view.Parent is Panel panel) panel.Children.Remove(view);
        else if (view.Parent is Window window) window.Content = null;
        host.Children.Add(view);
        System.Windows.Documents.TextElement.SetForeground(host, (Brush)Application.Current.FindResource("TextPrimary"));
        var surface = new Window { Content = host, Width = width, Height = height, WindowStyle = WindowStyle.None, ResizeMode = ResizeMode.NoResize, ShowActivated = false, ShowInTaskbar = false, Left = -10000, Top = -10000 };
        surface.Show();
        Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.ContextIdle, new Action(() => { }));
        host.Measure(new Size(width, height)); host.Arrange(new Rect(0, 0, width, height)); host.UpdateLayout();
        var image = new System.Windows.Media.Imaging.RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        image.Render(host);
        var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
        encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(image));
        using var output = System.IO.File.Create(file); encoder.Save(output);
        surface.Close();
    }
    private static MainViewModel Context()
    {
        var vm = new MainViewModel(new GuiSettings(), _ => { });
        ((DispatcherTimer)typeof(MainViewModel).GetField("timer", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(vm)!).Stop();
        return vm;
    }
    private static BotRowViewModel Row(int index, BotState state)
    {
        var row = new BotRowViewModel(index, $"Bot{index:D2}");
        row.Update(new BotSnapshot(index, row.Name, "account", state, 1, 1, 1, 1, 0, 1, 0, Vector3.Zero), _ => "system");
        return row;
    }
    private static void Check(bool result, string message) { if (!result) throw new InvalidOperationException(message); }
    private static void Throws(Action action)
    {
        try { action(); } catch (ArgumentException) { return; }
        throw new InvalidOperationException("Expected invalid command parameter rejection");
    }
}
