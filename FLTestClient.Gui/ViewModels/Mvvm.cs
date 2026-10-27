using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace FLTestClient.Gui.ViewModels;

/// <summary>Base class for view models with change notification.</summary>
public abstract class ObservableObject : INotifyPropertyChanged
{
    /// <summary>Raised when a property changes.</summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Sets a field and reports the change if the value differs.</summary>
    /// <typeparam name="T">Type of the field.</typeparam>
    /// <param name="field">Field.</param>
    /// <param name="value">New value.</param>
    /// <param name="name">Name of the property (filled in automatically).</param>
    /// <returns>True if the value changed.</returns>
    protected bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;
        field = value;
        OnPropertyChanged(name);
        return true;
    }

    /// <summary>Reports a property change.</summary>
    /// <param name="name">Name of the property.</param>
    protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>Simple <see cref="ICommand"/> based on delegates.</summary>
public sealed class RelayCommand : ICommand
{
    private readonly Action<object?> execute;
    private readonly Func<object?, bool>? canExecute;

    /// <summary>Creates a command without parameter.</summary>
    /// <param name="execute">Action to run.</param>
    /// <param name="canExecute">Optional: whether the command is currently possible.</param>
    public RelayCommand(Action execute, Func<bool>? canExecute = null)
        : this(_ => execute(), canExecute == null ? null : _ => canExecute())
    {
    }

    /// <summary>Creates a command with parameter.</summary>
    /// <param name="execute">Action to run.</param>
    /// <param name="canExecute">Optional: whether the command is currently possible.</param>
    public RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute)
    {
        this.execute = execute;
        this.canExecute = canExecute;
    }

    /// <summary>Raised when WPF should check the availability again.</summary>
    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    /// <summary>Checks whether the command can run.</summary>
    /// <param name="parameter">Command parameter.</param>
    /// <returns>True if possible.</returns>
    public bool CanExecute(object? parameter) => canExecute?.Invoke(parameter) ?? true;

    /// <summary>Runs the command.</summary>
    /// <param name="parameter">Command parameter.</param>
    public void Execute(object? parameter) => execute(parameter);
}

/// <summary>Colour category of a line or state; maps to a brush of the theme.</summary>
public enum Tone
{
    /// <summary>Normal text.</summary>
    Normal,
    /// <summary>Muted (secondary information).</summary>
    Muted,
    /// <summary>Accent colour (own actions, hints).</summary>
    Accent,
    /// <summary>Success / in space.</summary>
    Success,
    /// <summary>Warning / time lag.</summary>
    Warning,
    /// <summary>Error / kick.</summary>
    Danger,
    /// <summary>Special case / cheat notice.</summary>
    Special,
}

/// <summary>A line in a log, chat or console window.</summary>
/// <param name="Time">Time as text.</param>
/// <param name="Source">Source, e.g. a bot name.</param>
/// <param name="Text">Content.</param>
/// <param name="Tone">Colour.</param>
public sealed record FeedEntry(string Time, string Source, string Text, Tone Tone);
