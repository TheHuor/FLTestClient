using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using FLTestClient.Gui.ViewModels;

namespace FLTestClient.Gui.Views;

public partial class FleetView
{
    private BotRowViewModel? tableContextBot;
    private MainViewModel? Model => DataContext as MainViewModel;

    private void CardsSizeChanged(object sender, SizeChangedEventArgs e) => UpdateColumns();
    private void CardsLoaded(object sender, RoutedEventArgs e) => UpdateColumns();
    private void UpdateColumns()
    {
        if (FindChild<UniformGrid>(CardsItems) is { } panel) panel.Columns = CardsItems.ActualWidth >= 480 ? 2 : 1;
    }

    private void CardClicked(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Border { DataContext: BotRowViewModel row } card || Model == null) return;
        if (FindParent<ButtonBase>(e.OriginalSource as DependencyObject) != null) return;
        SelectCard(row);
        card.Focus();
        e.Handled = true;
    }

    private void CardKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not Border { DataContext: BotRowViewModel row } || Model == null) return;
        if (e.Key is Key.Enter or Key.Space) { SelectCard(row); e.Handled = true; }
    }

    private void SelectCard(BotRowViewModel row)
    {
        if (Model == null) return;
        if ((Keyboard.Modifiers & ModifierKeys.Control) != 0) row.IsSelected = !row.IsSelected;
        else { foreach (var bot in Model.Bots) bot.IsSelected = bot == row; }
        Model.FocusBotCommand.Execute(row);
    }

    private void ToggleCardSelection(object sender, MouseButtonEventArgs e)
    {
        if (sender is CheckBox { DataContext: BotRowViewModel row })
        {
            row.IsSelected = !row.IsSelected;
            Model?.FocusBotCommand.Execute(row);
            e.Handled = true;
        }
    }

    private void CardCheckChanged(object sender, RoutedEventArgs e)
    {
        if (sender is CheckBox { IsKeyboardFocusWithin: true, DataContext: BotRowViewModel row }) Model?.FocusBotCommand.Execute(row);
    }

    private void CardMenuOpening(object sender, ContextMenuEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: BotRowViewModel row } element && Model != null)
        {
            Model.FocusBotCommand.Execute(row);
            element.ContextMenu = CreateBotMenu(row);
        }
    }

    private void OpenCardMenu(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: BotRowViewModel row } button || Model == null) return;
        Model.FocusBotCommand.Execute(row);
        var menu = CreateBotMenu(row);
        menu.PlacementTarget = button;
        menu.Placement = PlacementMode.Bottom;
        menu.IsOpen = true;
    }

    private ContextMenu CreateBotMenu(BotRowViewModel row)
    {
        var menu = new ContextMenu { Style = (Style)FindResource("BotContextMenu") };
        menu.Items.Add(new MenuItem { Header = Loc.T("Cards.ContextScope", row.Name), IsEnabled = false, Style = (Style)FindResource("BotMenuItem") });
        Add(menu, "Fleet.Launch", Model!.BotLaunchCommand, row);
        Add(menu, "Fleet.Dock", Model.ShowBotActionsCommand, row);
        Add(menu, "Cards.OpenChat", Model.ShowBotChatCommand, row);
        menu.Items.Add(new Separator());
        Add(menu, "Fleet.Reconnect", Model.BotReconnectCommand, row);
        Add(menu, "Fleet.Disconnect", Model.BotRemoveCommand, row);
        return menu;
    }

    private static void Add(ContextMenu menu, string key, ICommand command, object? parameter = null)
    {
        var item = new MenuItem { Header = Loc.T(key), Command = command, CommandParameter = parameter, Style = (Style)menu.FindResource("BotMenuItem") };
        if (key == "Fleet.Disconnect") item.SetResourceReference(ForegroundProperty, "Danger");
        menu.Items.Add(item);
    }

    private void OpenBulkMenu(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || Model == null) return;
        var menu = new ContextMenu { PlacementTarget = button, Placement = PlacementMode.Top, Style = (Style)FindResource("BotContextMenu") };
        menu.Items.Add(new MenuItem { Header = Model.SelectionSummary, IsEnabled = false, Style = (Style)FindResource("BotMenuItem") });
        Add(menu, "Fleet.Launch", Model.LaunchSelectedCommand);
        Add(menu, "Fleet.Dock", Model.DockSelectedCommand);
        Add(menu, "Fleet.Distribute", Model.DistributeSelectedCommand);
        Add(menu, "Fleet.Reconnect", Model.ReconnectSelectedCommand);
        Add(menu, "Fleet.Disconnect", Model.RemoveSelectedCommand);
        menu.IsOpen = true;
    }

    private void TableSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!FleetTable.IsKeyboardFocusWithin) return;
        if (e.AddedItems.OfType<BotRowViewModel>().LastOrDefault() is { } row) Model?.FocusBotCommand.Execute(row);
    }
    private void TableLeftClick(object sender, MouseButtonEventArgs e)
    {
        if (FindParent<DataGridRow>(e.OriginalSource as DependencyObject)?.Item is BotRowViewModel row)
            Model?.FocusBotCommand.Execute(row);
    }
    private void TableRightClick(object sender, MouseButtonEventArgs e)
    {
        tableContextBot = FindParent<DataGridRow>(e.OriginalSource as DependencyObject)?.Item as BotRowViewModel;
        if (tableContextBot is { } row)
        {
            Model?.FocusBotCommand.Execute(row);
            FleetTable.ContextMenu = CreateBotMenu(row);
        }
        else e.Handled = true;
    }
    internal BotRowViewModel? ResolveTableMenuBot(bool keyboard) =>
        keyboard ? FleetTable.CurrentItem as BotRowViewModel : tableContextBot;

    private void TableMenuOpening(object sender, ContextMenuEventArgs e)
    {
        var row = ResolveTableMenuBot(e.CursorLeft < 0 && e.CursorTop < 0);
        if (row != null && Model?.FocusBotCommand.CanExecute(row) == true)
        {
            Model.FocusBotCommand.Execute(row);
            FleetTable.ContextMenu = CreateBotMenu(row);
        }
        else e.Handled = true;
    }

    private static T? FindChild<T>(DependencyObject element) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(element); i++)
        {
            var child = VisualTreeHelper.GetChild(element, i);
            if (child is T typed) return typed;
            if (FindChild<T>(child) is { } nested) return nested;
        }
        return null;
    }
    private static T? FindParent<T>(DependencyObject? element) where T : DependencyObject
    {
        while (element != null)
        {
            if (element is T typed) return typed;
            element = element is Visual ? VisualTreeHelper.GetParent(element) : LogicalTreeHelper.GetParent(element);
        }
        return null;
    }
}
