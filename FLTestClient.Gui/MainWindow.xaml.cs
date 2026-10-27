using System.Windows;
using FLTestClient.Gui.ViewModels;

namespace FLTestClient.Gui;

/// <summary>Main window; connects the <see cref="MainViewModel"/> with theme, language and the window lifetime.</summary>
public partial class MainWindow : Window
{
    private readonly MainViewModel viewModel;

    /// <summary>Creates the window and applies the stored theme and language.</summary>
    public MainWindow() : this(new MainViewModel()) { }

    internal MainWindow(MainViewModel model)
    {
        viewModel = model;
        App.ApplyLanguage(viewModel.Language);
        App.ApplyTheme(viewModel.IsDarkTheme);
        InitializeComponent();
        DataContext = viewModel;
        viewModel.ThemeChanged += App.ApplyTheme;
        viewModel.LanguageChanged += App.ApplyLanguage;
        SourceInitialized += (_, _) => App.SetDarkTitleBar(this, viewModel.IsDarkTheme);
        viewModel.RequestAddBots += () => new Views.AddBotsWindow(viewModel) { Owner = this }.ShowDialog();
        viewModel.RequestFleetAction += distribute => new Views.FleetActionWindow(viewModel, distribute) { Owner = this }.ShowDialog();
        Closed += (_, _) => viewModel.Dispose();
    }
}
