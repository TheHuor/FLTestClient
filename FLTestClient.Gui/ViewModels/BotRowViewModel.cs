using System.Globalization;

namespace FLTestClient.Gui.ViewModels;

/// <summary>A row of the fleet list; updated from a <see cref="BotSnapshot"/>.</summary>
public sealed class BotRowViewModel : ObservableObject
{
    private BotState state;
    private string system = "", baseName = "", ship = "", cash = "", ping = "";
    private uint rawCash;
    private int unknown;
    private bool isSelected;

    /// <summary>Creates a row for a bot.</summary>
    /// <param name="index">Bot number.</param>
    /// <param name="name">Character name.</param>
    public BotRowViewModel(int index, string name)
    {
        Index = index;
        Name = name;
    }

    /// <summary>Bot number.</summary>
    public int Index { get; }

    /// <summary>Character name.</summary>
    public string Name { get; }

    /// <summary>State.</summary>
    public BotState State
    {
        get => state;
        private set
        {
            if (Set(ref state, value))
            {
                OnPropertyChanged(nameof(StateLabel));
                OnPropertyChanged(nameof(StateTone));
            }
        }
    }

    /// <summary>State as a short text in the active language.</summary>
    public string StateLabel => Texts.StateLabel(State);

    /// <summary>Colour of the state.</summary>
    public Tone StateTone => State switch
    {
        BotState.InSpace => Tone.Success,
        BotState.InBase => Tone.Accent,
        BotState.Failed => Tone.Danger,
        BotState.Disconnected or BotState.Offline => Tone.Muted,
        _ => Tone.Warning,
    };

    /// <summary>System nickname or empty.</summary>
    public string System { get => system; private set => Set(ref system, value); }

    /// <summary>Base nickname while the bot is docked.</summary>
    public string Base { get => baseName; private set => Set(ref baseName, value); }

    /// <summary>Object ID of the ship in space.</summary>
    public string Ship { get => ship; private set => Set(ref ship, value); }

    /// <summary>Money, formatted for the active language.</summary>
    public string Cash { get => cash; private set => Set(ref cash, value); }

    /// <summary>Ping, formatted.</summary>
    public string Ping { get => ping; private set => Set(ref ping, value); }

    /// <summary>Number of packets that could not be decoded.</summary>
    public int Unknown { get => unknown; private set => Set(ref unknown, value); }

    /// <summary>Selected in the grid (bound to the row selection).</summary>
    public bool IsSelected { get => isSelected; set => Set(ref isSelected, value); }

    /// <summary>Takes over a new snapshot.</summary>
    /// <param name="s">State of the bot.</param>
    /// <param name="names">Lookup from hash to nickname.</param>
    public void Update(BotSnapshot s, Func<uint, string> names)
    {
        State = s.State;
        System = names(s.SystemId);
        Base = s.State is BotState.InBase or BotState.Launching ? names(s.BaseId) : "";
        Ship = s.ShipId == 0 || s.State != BotState.InSpace ? "" : s.ShipId.ToString(CultureInfo.InvariantCulture);
        rawCash = s.Cash;
        Cash = rawCash.ToString("N0", Texts.Culture);
        Ping = s.PingMs < 0 ? "" : $"{s.PingMs} ms";
        Unknown = s.UnknownPackets;
    }

    /// <summary>Refreshes language-dependent texts after the language was switched.</summary>
    public void RefreshTexts()
    {
        OnPropertyChanged(nameof(StateLabel));
        Cash = rawCash.ToString("N0", Texts.Culture);
    }
}
