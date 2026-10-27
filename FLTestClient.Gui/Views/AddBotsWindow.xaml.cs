using System.Windows;
using System.Windows.Controls;
using FLTestClient.Gui.ViewModels;

namespace FLTestClient.Gui.Views;

/// <summary>Creates and connects a bot range without a permanent fleet toolbar form.</summary>
public partial class AddBotsWindow : Window
{
    /// <summary>Creates the range form.</summary>
    /// <param name="model">Fleet settings.</param>
    public AddBotsWindow(MainViewModel model)
    {
        InitializeComponent();
        DataContext = model;
        SourceInitialized += (_, _) => App.SetDarkTitleBar(this, model.IsDarkTheme);
    }
    private void CreateBots(object sender, RoutedEventArgs e)
    {
        BotCount.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
        FirstIndex.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
        if (Validation.GetHasError(BotCount) || Validation.GetHasError(FirstIndex)) return;
        ((MainViewModel)DataContext).StartBotsCommand.Execute(null);
        DialogResult = true;
    }
}
