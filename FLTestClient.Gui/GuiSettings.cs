using System.IO;
using System.Text.Json;

namespace FLTestClient.Gui;

/// <summary>
/// Last used inputs of the GUI, stored in <c>%APPDATA%\FLTestClient\settings.json</c>.
/// </summary>
/// <remarks>Passwords are deliberately not stored.</remarks>
public sealed class GuiSettings
{
    private static readonly string FilePath =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FLTestClient", "settings.json");

    /// <summary>Server address.</summary>
    public string Host { get; set; } = "";

    /// <summary>Server port.</summary>
    public int Port { get; set; } = 2302;

    /// <summary>Account prefix of the bots.</summary>
    public string AccountPrefix { get; set; } = "fltest";

    /// <summary>Character prefix of the bots.</summary>
    public string NamePrefix { get; set; } = "TestBot";

    /// <summary>First bot number when adding bots.</summary>
    public int StartIndex { get; set; } = 1;

    /// <summary>Number of bots when adding bots.</summary>
    public int Count { get; set; } = 5;

    /// <summary>Launch the bots automatically after every docking.</summary>
    public bool AutoLaunch { get; set; } = true;

    /// <summary>DATA folder of the server for base and system names.</summary>
    public string DataDirectory { get; set; } = "";

    /// <summary>Host of the FLHook admin interface.</summary>
    public string AdminHost { get; set; } = "127.0.0.1";

    /// <summary>Port of the FLHook admin interface.</summary>
    public int AdminPort { get; set; } = 9000;

    /// <summary>flhook.ini the admin password can be read from.</summary>
    public string FlhookIni { get; set; } = "";

    /// <summary>Target bases for distributing, one per line.</summary>
    public string DistributeBases { get; set; } = string.Join(Environment.NewLine,
        "Li02_01_Base", "Li03_01_Base", "Li04_01_Base", "Br01_01_Base", "Br02_01_Base", "Br03_01_Base",
        "Br04_01_Base", "Ku01_01_Base", "Ku02_01_Base", "Ku03_01_Base", "Rh01_01_Base", "Rh02_01_Base",
        "Rh03_01_Base", "Iw01_01_Base", "Iw02_01_Base");

    /// <summary>Faction for new characters (newcharacter.ini).</summary>
    public string Faction { get; set; } = "new_player";

    /// <summary>Start base for new characters.</summary>
    public string NewCharBase { get; set; } = "li01_01_base";

    /// <summary>Ship package for new characters.</summary>
    public string Package { get; set; } = "ge_fighter";

    /// <summary>Pilot for new characters.</summary>
    public string Pilot { get; set; } = "trent";

    /// <summary>Fly a circle in space instead of standing still.</summary>
    public bool CircleFlight { get; set; }

    /// <summary>Radius of the circle flight in metres.</summary>
    public double CircleRadius { get; set; } = 600;

    /// <summary>Speed of the circle flight in metres per second.</summary>
    public double CircleSpeed { get; set; } = 80;

    /// <summary>Log out and in periodically.</summary>
    public bool SessionCycle { get; set; }

    /// <summary>Shortest time online per cycle in seconds.</summary>
    public double OnlineMinSeconds { get; set; } = 120;

    /// <summary>Longest time online per cycle in seconds.</summary>
    public double OnlineMaxSeconds { get; set; } = 300;

    /// <summary>Time offline between sessions in seconds.</summary>
    public double OfflineSeconds { get; set; } = 30;

    /// <summary>Interval of the chat script in seconds; 0 = off.</summary>
    public double ChatIntervalSeconds { get; set; }

    /// <summary>Chat script, one line per message.</summary>
    public string ChatScript { get; set; } = string.Join(Environment.NewLine, "/ping", "/time", "/id");

    /// <summary>Last target base for "Dock at base".</summary>
    public string DockTarget { get; set; } = "Li01_01_Base";

    /// <summary>Dark (true) or bright theme.</summary>
    public bool DarkTheme { get; set; } = true;

    /// <summary>UI language (<c>en</c> or <c>de</c>); English is the default.</summary>
    public string Language { get; set; } = "en";

    /// <summary>Path of the FLHook log for the log monitor.</summary>
    public string ServerLog { get; set; } = "";

    /// <summary>Loads the stored settings or returns defaults.</summary>
    /// <returns>Settings.</returns>
    public static GuiSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<GuiSettings>(File.ReadAllText(FilePath)) ?? new GuiSettings();
        }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException)
        {
            // Broken or locked file: carry on with defaults.
        }
        return new GuiSettings();
    }

    /// <summary>Saves the settings; errors are ignored.</summary>
    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Not critical.
        }
    }
}
