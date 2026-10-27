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
                        var picker = (TabControl)settings.FindName("SettingsTabs");
                        for (int section = 0; section < 4; section++)
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
        Run("Settings categories are four directly visible tabs", () =>
        {
            var view = new SettingsView { DataContext = Context() };
            Check(view.FindName("SettingsTabs") is TabControl tabs && tabs.Items.Count == 4,
                "Settings categories are still hidden behind a dropdown");
        });
        Run("Inspector actions target their bot rather than the bulk selection", () =>
        {
            var vm = Context();
            var a = Row(1, BotState.InSpace); var b = Row(2, BotState.InSpace);
            vm.Bots.Add(a); vm.Bots.Add(b); a.IsSelected = true;
            vm.ShowBotActionsCommand.Execute(b);
            Check(ReferenceEquals(vm.FocusedBot, b) && a.IsSelected && !b.IsSelected, "Context action changed bulk scope");
            vm.DockTarget = "Li01_01_Base";
            FleetActionPlan plan = vm.CreateFocusedRelocationPlan();
            Check(plan.Assignments.Count == 1 && plan.Assignments[0].Name == "Bot02", "Inspector targeted the selected Bot01");
        });
        Run("Removing or filtering a focused bot clears inspector scope", () =>
        {
            var vm = Context(); var a = Row(1, BotState.InSpace); vm.Bots.Add(a);
            vm.FocusBotCommand.Execute(a); vm.FleetSearch = "no-match";
            Check(vm.FocusedBot == null && !(bool)vm.ShowInspector, "Filtered bot kept active inspector");
            vm.FleetSearch = ""; vm.FocusBotCommand.Execute(a); vm.Bots.Remove(a);
            Check(vm.FocusedBot == null, "Removed bot kept active inspector");
        });
        Run("Card/table switch and inspector collapse preserve selection", () =>
        {
            var vm = Context(); var a = Row(1, BotState.InSpace); vm.Bots.Add(a); a.IsSelected = true;
            vm.FocusBotCommand.Execute(a); vm.UseCardView = false; vm.CloseInspectorCommand.Execute(null);
            Check(a.IsSelected && ReferenceEquals(vm.FocusedBot, a) && !(bool)vm.ShowInspector, "Presentation changed action scope");
            vm.UseCardView = true; vm.IsInspectorOpen = true;
            Check((bool)vm.ShowInspector, "Inspector could not be restored");
        });
        Run("Inspector chat contains only the focused bot feed", () =>
        {
            var vm = Context(); var a = Row(1, BotState.InSpace); vm.Bots.Add(a);
            vm.Chat.Add(new FeedEntry("12:00", "Bot01", "one", Tone.Normal));
            vm.Chat.Add(new FeedEntry("12:00", "Bot02", "two", Tone.Normal));
            vm.FocusBotCommand.Execute(a);
            var feed = ((System.ComponentModel.ICollectionView)vm.FocusedChat).Cast<FeedEntry>().ToArray();
            Check(feed.Length == 1 && feed[0].Text == "one", "Inspector leaked another bot's feed");
        });
        Run("Single-bot launch is available only for a currently docked bot", () =>
        {
            var vm = Context(); var docked = Row(1, BotState.InBase); var space = Row(2, BotState.InSpace);
            vm.Bots.Add(docked); vm.Bots.Add(space);
            Check(vm.BotLaunchCommand.CanExecute(docked) && !vm.BotLaunchCommand.CanExecute(space), "Launch availability ignores state");
            vm.Bots.Remove(docked);
            Check(!vm.BotLaunchCommand.CanExecute(docked), "Removed bot remains actionable");
        });
        Run("Populated compact cards lay out with readonly location fields", () =>
        {
            var vm = Context(); var row = Row(1, BotState.InBase); vm.Bots.Add(row);
            var view = new FleetView { DataContext = vm };
            view.Measure(new Size(1100, 720)); view.Arrange(new Rect(0, 0, 1100, 720)); view.UpdateLayout();
        });
        Run("Changing inspector bot never reuses another bot chat draft", () =>
        {
            var vm = Context(); var a = Row(1, BotState.InSpace); var b = Row(2, BotState.InSpace); vm.Bots.Add(a); vm.Bots.Add(b);
            vm.ShowBotChatCommand.Execute(a); vm.FocusedChatInput = "/test-for-a"; vm.ShowBotChatCommand.Execute(b);
            Check(vm.FocusedChatInput == "", "Bot A draft remains armed for Bot B");
        });
        Run("Real card-to-table layout does not replace separately focused bot", () =>
        {
            var vm = Context(); var a = Row(1, BotState.InSpace); var b = Row(2, BotState.InSpace); vm.Bots.Add(a); vm.Bots.Add(b); a.IsSelected = true;
            vm.FocusBotCommand.Execute(b);
            var view = new FleetView { DataContext = vm };
            view.Measure(new Size(1200, 800)); view.Arrange(new Rect(0, 0, 1200, 800)); view.UpdateLayout();
            vm.UseCardView = false;
            view.Measure(new Size(1200, 800)); view.Arrange(new Rect(0, 0, 1200, 800)); view.UpdateLayout();
            Check(ReferenceEquals(vm.FocusedBot, b), "Table realization replaced explicitly inspected Bot02");
        });
        Run("Keyboard table menu resolves the current row instead of old inspector focus", () =>
        {
            var vm = Context(); var a = Row(1, BotState.InSpace); var b = Row(2, BotState.InSpace); vm.Bots.Add(a); vm.Bots.Add(b);
            vm.FocusBotCommand.Execute(a); vm.UseCardView = false;
            var view = new FleetView { DataContext = vm };
            var table = (DataGrid)view.FindName("FleetTable"); table.CurrentItem = b;
            var resolve = typeof(FleetView).GetMethod("ResolveTableMenuBot", BindingFlags.Instance | BindingFlags.NonPublic);
            Check(resolve != null && ReferenceEquals(resolve.Invoke(view, new object[] { true }), b), "Keyboard menu does not resolve current Bot02");
        });
        Run("Every single-bot menu command retains the explicitly named bot", () =>
        {
            var vm = Context(); var a = Row(1, BotState.InBase); var b = Row(2, BotState.InSpace);
            vm.Bots.Add(a); vm.Bots.Add(b); a.IsSelected = true;
            var view = new FleetView { DataContext = vm };
            var menu = (ContextMenu)typeof(FleetView).GetMethod("CreateBotMenu", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(view, new object[] { b })!;
            menu.Measure(new Size(320, 400)); menu.Arrange(new Rect(0, 0, 320, 400)); menu.UpdateLayout();
            var actions = menu.Items.OfType<MenuItem>().Where(item => item.Command != null).ToArray();
            Check(actions.Length == 5 && actions.All(item => ReferenceEquals(item.CommandParameter, b)), "Menu used shared bulk selection");
        });
        Run("The retained table handles a 200-bot fleet without changing scope", () =>
        {
            var vm = Context();
            for (int i = 1; i <= 200; i++) vm.Bots.Add(Row(i, BotState.InSpace));
            vm.Bots[99].IsSelected = true; vm.FocusBotCommand.Execute(vm.Bots[99]); vm.UseCardView = false;
            var view = new FleetView { DataContext = vm };
            view.Measure(new Size(1200, 800)); view.Arrange(new Rect(0, 0, 1200, 800)); view.UpdateLayout();
            Check(vm.SelectedCount == 1 && vm.FocusedBot?.Index == 100 && vm.FleetRows.Cast<object>().Count() == 200, "Large-fleet presentation changed scope");
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
                for (int i = 1; i <= 12; i++) { var row = Row(i, i == 2 ? BotState.InBase : BotState.InSpace); vm.Bots.Add(row); row.IsSelected = i == 3; }
                foreach (UserControl view in new UserControl[] { new FleetView(), new BehaviourView(), new CommsView(), new CommandView(), new DiagnosticsView(), new SettingsView() })
                {
                    view.DataContext = vm;
                    if (view is FleetView) vm.ShowBotActionsCommand.Execute(vm.Bots[2]);
                    Capture(view, 860, 520, System.IO.Path.Combine(args[1], $"{view.GetType().Name}-{lang}-{theme}.png"));
                    if (view is SettingsView settings)
                    {
                        for (int section = 1; section < 4; section++)
                        {
                            ((TabControl)settings.FindName("SettingsTabs")).SelectedIndex = section;
                            Capture(view, 860, 520, System.IO.Path.Combine(args[1], $"Settings-{section}-{lang}-{theme}.png"));
                        }
                    }
                    if (view is DiagnosticsView diagnostics)
                    {
                        ((RadioButton)diagnostics.FindName("ServerTab")).IsChecked = true;
                        Capture(view, 860, 520, System.IO.Path.Combine(args[1], $"ServerLog-{lang}-{theme}.png"));
                    }
                }
                var mainContext = Context(lang, theme == "Dark");
                for (int i = 1; i <= 12; i++) { var bot = DemoRow(i); mainContext.Bots.Add(bot); bot.IsSelected = i == 3; }
                mainContext.ShowBotActionsCommand.Execute(mainContext.Bots[2]);
                var main = new MainWindow(mainContext);
                var mainContent = (FrameworkElement)main.Content;
                Capture(mainContent, 1440, 1024, System.IO.Path.Combine(args[1], $"Main-cards-{lang}-{theme}.png"));
                mainContext.UseCardView = false;
                Capture(mainContent, 1440, 1024, System.IO.Path.Combine(args[1], $"Main-table-{lang}-{theme}.png"));
                mainContext.UseCardView = true;
                Capture(mainContent, 1100, 680, System.IO.Path.Combine(args[1], $"Main-minimum-{lang}-{theme}.png"));
                mainContext.IsInspectorOpen = false;
                Capture(mainContent, 1440, 1024, System.IO.Path.Combine(args[1], $"Main-collapsed-{lang}-{theme}.png"));
                var settingsContext = Context(lang, theme == "Dark"); settingsContext.MainPageIndex = 5;
                var settingsMain = new MainWindow(settingsContext);
                Capture((FrameworkElement)settingsMain.Content, 1440, 1024, System.IO.Path.Combine(args[1], $"Main-settings-{lang}-{theme}.png"));
                var add = new AddBotsWindow(Context(lang, theme == "Dark"));
                Capture((FrameworkElement)add.Content, 460, 310, System.IO.Path.Combine(args[1], $"AddBots-{lang}-{theme}.png"));
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
        view.DataContext = view.DataContext; // Retain inherited window context when rendering its content separately.
        view.Measure(new Size(width, height)); view.Arrange(new Rect(0, 0, width, height)); view.UpdateLayout();
        var host = new Grid { Background = (Brush)Application.Current.FindResource("WindowBackground") };
        if (view.Parent is Panel panel) panel.Children.Remove(view);
        else if (view.Parent is Window window) window.Content = null;
        host.Children.Add(view);
        System.Windows.Documents.TextElement.SetFontFamily(host, new FontFamily("Segoe UI"));
        System.Windows.Documents.TextElement.SetFontSize(host, 13);
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
    private static MainViewModel Context(string language = "en", bool dark = true)
    {
        var vm = new MainViewModel(new GuiSettings { Language = language, DarkTheme = dark }, _ => { });
        ((DispatcherTimer)typeof(MainViewModel).GetField("timer", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(vm)!).Stop();
        return vm;
    }
    private static BotRowViewModel DemoRow(int index)
    {
        var bot = new BotRowViewModel(index, $"TestBot{index:D2}");
        var state = index == 2 ? BotState.InBase : index == 8 ? BotState.Failed : index == 12 ? BotState.Offline : BotState.InSpace;
        bot.Update(new BotSnapshot(index, bot.Name, "demo", state, 1, (uint)((index - 1) % 4 + 1), state == BotState.InBase ? 101u : 0, 1, 15000, 24, 0, Vector3.Zero),
            id => id switch { 1 => "New York", 2 => "Texas", 3 => "California", 4 => "Colorado", 101 => "Manhattan", _ => "" });
        return bot;
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
