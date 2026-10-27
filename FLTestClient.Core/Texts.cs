using System.Globalization;

namespace FLTestClient;

/// <summary>Severity of a log message; decides e.g. the colour in the GUI.</summary>
public enum LogLevel
{
    /// <summary>Normal progress (login, packets).</summary>
    Info,
    /// <summary>Notable event (docked, landed, chat sent).</summary>
    Event,
    /// <summary>Success (launched, in space).</summary>
    Success,
    /// <summary>Error (rejected, disconnected, failed).</summary>
    Error,
}

/// <summary>
/// Translatable texts of the core (bot log, command line, error messages).
/// English is the default and fallback, German the second language.
/// </summary>
/// <remarks>
/// Texts are composite format strings (<see cref="string.Format(IFormatProvider, string, object[])"/>).
/// A missing key returns the key itself so that gaps are easy to spot.
/// </remarks>
public static class Texts
{
    /// <summary>Supported languages as ISO codes.</summary>
    public static readonly IReadOnlyList<string> Languages = new[] { "en", "de" };

    private static string language = "en";

    /// <summary>Active language (<c>en</c> or <c>de</c>). Unknown codes fall back to English.</summary>
    public static string Language
    {
        get => language;
        set => language = value?.ToLowerInvariant() == "de" ? "de" : "en";
    }

    /// <summary>Culture of the active language for number and time formats.</summary>
    public static CultureInfo Culture
    {
        get
        {
            try
            {
                return CultureInfo.GetCultureInfo(Language == "de" ? "de-DE" : "en-US");
            }
            catch (CultureNotFoundException)
            {
                // Globalization-invariant hosts only know the invariant culture.
                return CultureInfo.InvariantCulture;
            }
        }
    }

    private static readonly Dictionary<string, (string En, string De)> Table = new()
    {
        // Bot log
        ["Bot.Connecting"] = ("connecting to {0}:{1} (account {2})", "verbinde mit {0}:{1} (Account {2})"),
        ["Bot.Connected"] = ("DirectPlay connected, waiting for CONNECTRESPONSE", "DirectPlay verbunden, warte auf CONNECTRESPONSE"),
        ["Bot.WrongPassword"] = ("connection rejected: server password missing or wrong", "Verbindung abgelehnt: Server-Passwort fehlt oder ist falsch"),
        ["Bot.ConnectFailed"] = ("connection failed: HRESULT 0x{0:X8}", "Verbindung fehlgeschlagen: HRESULT 0x{0:X8}"),
        ["Bot.ServerDisconnected"] = ("disconnected by server (0x{0:X8})", "Verbindung vom Server getrennt (0x{0:X8})"),
        ["Bot.DecodeError"] = ("decode error {0}: {1}", "Dekodierfehler {0}: {1}"),
        ["Bot.ConnectResponse"] = ("CONNECTRESPONSE: client ID {0}", "CONNECTRESPONSE: Client-ID {0}"),
        ["Bot.SetStartRoom"] = ("SETSTARTROOM: base 0x{0:X8}, room 0x{1:X8}", "SETSTARTROOM: Basis 0x{0:X8}, Raum 0x{1:X8}"),
        ["Bot.NewsDestroy"] = ("GFDESTROYNEWSBROADCAST base 0x{0:X8} key {1}", "GFDESTROYNEWSBROADCAST Basis 0x{0:X8} Key {1}"),
        ["Bot.ShipDestroyed"] = ("own ship destroyed", "eigenes Schiff zerstört"),
        ["Bot.AccountBusy"] = ("account still logged in, retrying in 10 s", "Account noch angemeldet, neuer Versuch in 10 s"),
        ["Bot.LoginRejected"] = ("login rejected (status {0})", "Login abgelehnt (Status {0})"),
        ["Bot.LoggedIn"] = ("logged in", "eingeloggt"),
        ["Bot.CharacterInfo"] = ("CHARACTERINFO: {0} character(s){1}", "CHARACTERINFO: {0} Charakter(e){1}"),
        ["Bot.CreateFailed"] = ("character was not created (name taken or invalid?)", "Charakter wurde nicht angelegt (Name vergeben oder ungültig?)"),
        ["Bot.Creating"] = ("creating character '{0}' ({1}, {2}, {3}, {4})", "lege Charakter '{0}' an ({1}, {2}, {3}, {4})"),
        ["Bot.Selecting"] = ("selecting character '{0}' ({1})", "wähle Charakter '{0}' ({1})"),
        ["Bot.CharSelectVerified"] = ("CHARSELECTVERIFIED (server time {0:F1})", "CHARSELECTVERIFIED (Serverzeit {0:F1})"),
        ["Bot.ServerCharSelect"] = ("character selection by server (beam?)", "Charakterwahl vom Server (Beam?)"),
        ["Bot.Docked"] = ("docked at {0}", "angedockt auf {0}"),
        ["Bot.Launching"] = ("launching (REQUESTCREATESHIP)", "starte (REQUESTCREATESHIP)"),
        ["Bot.SavedInSpace"] = ("character is in space ({0}), REQUESTCREATESHIP", "Charakter ist im All ({0}), REQUESTCREATESHIP"),
        ["Bot.CreateShipRejected"] = ("REQUESTCREATESHIP rejected", "REQUESTCREATESHIP abgelehnt"),
        ["Bot.Launched"] = ("LAUNCH: ship {0}, position ({1:F0}, {2:F0}, {3:F0})", "LAUNCH: Schiff {0}, Position ({1:F0}, {2:F0}, {3:F0})"),
        ["Bot.Landed"] = ("LAND at {0}", "LAND auf {0}"),
        ["Bot.Says"] = ("says: {0}", "sagt: {0}"),
        ["Bot.SendFailed"] = ("send failed (not connected)", "Senden fehlgeschlagen (nicht verbunden)"),
        ["Bot.Respawn"] = ("respawning (character selection)", "Respawn (Charakterwahl)"),
        ["Bot.CycleLogout"] = ("session cycle: logging out, back in {0:F0} s", "Sitzungszyklus: melde mich ab, zurück in {0:F0} s"),
        ["Bot.StartFailed"] = ("start failed: {0}", "Start fehlgeschlagen: {0}"),
        ["Bot.TickError"] = ("error in tick: {0}", "Fehler im Takt: {0}"),
        ["Manager.Error"] = ("error: {0}", "Fehler: {0}"),

        // Core error messages
        ["Error.HResult"] = ("{0} failed: HRESULT 0x{1:X8}", "{0} fehlgeschlagen: HRESULT 0x{1:X8}"),
        ["Error.NoDirectPlay"] = ("CoCreateInstance(DirectPlay8Client) - is the Windows feature DirectPlay enabled?",
                                  "CoCreateInstance(DirectPlay8Client) - ist DirectPlay als Windows-Feature aktiv?"),
        ["Error.UniverseMissing"] = ("universe.ini not found", "universe.ini nicht gefunden"),
        ["Error.AdminRejected"] = ("FLHook login rejected: {0}", "FLHook-Anmeldung abgelehnt: {0}"),
        ["Error.AdminTimeout"] = ("(timeout)", "(Zeitüberschreitung)"),

        // Bot states (short, for lists)
        ["State.Offline"] = ("Waiting", "Wartet"),
        ["State.Connecting"] = ("Connecting", "Verbinde"),
        ["State.LoggingIn"] = ("Login", "Login"),
        ["State.CharacterMenu"] = ("Character menu", "Charakterwahl"),
        ["State.Selecting"] = ("Loading character", "Lade Charakter"),
        ["State.InBase"] = ("Docked", "Gedockt"),
        ["State.Launching"] = ("Launching", "Startet"),
        ["State.InSpace"] = ("In space", "Im All"),
        ["State.Disconnected"] = ("Disconnected", "Getrennt"),
        ["State.Failed"] = ("Error", "Fehler"),

        // Command line
        ["Cli.Usage"] = (
            """
            FLTestClient - headless Freelancer client for testing a local FLServer/FLHook

              FLTestClient [options]

              --server HOST[:PORT]   server (default: first local IPv4 address, port 2302)
              --password PW          session password of the server
              --flserver-cfg FILE    read the session password from FLServer.cfg (Accts\MultiPlayer\FLServer.cfg)
              --count N              number of bots (default 1)
              --start K              first bot number (default 1)
              --account PREFIX       account prefix (default fltest -> fltest-001, ...)
              --name PREFIX          character prefix (default TestBot -> TestBot01, ...)
              --launch               launch automatically after every docking
              --say TEXT             say once after login (e.g. "/help"), may be repeated
              --duration SEC         disconnect and exit after SEC seconds (default: interactive)
              --stagger SEC          delay between connections (default 0.5)
              --data FOLDER          DATA folder of the server for base/system names (universe.ini)
              --faction/--base/--package/--pilot NICK   values for new characters (newcharacter.ini)
              --circle [R[:S]]       fly a circle in space, radius R m, speed S m/s (default 600:80)
              --cycle MIN-MAX:OFF    log out after MIN..MAX s online, back after OFF s (e.g. 120-300:30)
              --chat-interval SEC    send a random chat-script line every SEC seconds
              --chat-line TEXT       a line of the chat script (text or FLHook command), may be repeated
              --lang en|de           language of the output (default en)
              --verbose              log every received packet
              --commands FILE        run new lines of this file as commands (for background use)
              --hash NICK            print the FL hash (CreateID) of a nickname

            Commands (TARGET = bot number or "all"):
              list | say TARGET TEXT | launch TARGET | reconnect TARGET | stop TARGET | quit
            """,
            """
            FLTestClient - kopfloser Freelancer-Client für Tests gegen einen lokalen FLServer/FLHook

              FLTestClient [Optionen]

              --server HOST[:PORT]   Server (Standard: erste lokale IPv4-Adresse, Port 2302)
              --password PW          Session-Passwort des Servers
              --flserver-cfg DATEI   Session-Passwort aus FLServer.cfg lesen (Accts\MultiPlayer\FLServer.cfg)
              --count N              Anzahl Bots (Standard 1)
              --start K              erste Bot-Nummer (Standard 1)
              --account PREFIX       Account-Präfix (Standard fltest -> fltest-001, ...)
              --name PREFIX          Charakter-Präfix (Standard TestBot -> TestBot01, ...)
              --launch               nach jedem Andocken automatisch starten
              --say TEXT             nach dem Einloggen einmal sagen (z.B. "/help"), mehrfach erlaubt
              --duration SEK         nach SEK Sekunden trennen und beenden (Standard: interaktiv)
              --stagger SEK          Abstand zwischen Verbindungen (Standard 0.5)
              --data ORDNER          DATA-Ordner des Servers für Basis-/Systemnamen (universe.ini)
              --faction/--base/--package/--pilot NICK   Werte für neue Charaktere (newcharacter.ini)
              --circle [R[:S]]       im All im Kreis fliegen, Radius R m, Tempo S m/s (Standard 600:80)
              --cycle MIN-MAX:OFF    nach MIN..MAX s online abmelden, nach OFF s zurück (z.B. 120-300:30)
              --chat-interval SEK    alle SEK Sekunden eine zufällige Chat-Skript-Zeile senden
              --chat-line TEXT       eine Zeile des Chat-Skripts (Text oder FLHook-Befehl), mehrfach erlaubt
              --lang en|de           Sprache der Ausgabe (Standard en)
              --verbose              jedes empfangene Paket protokollieren
              --commands DATEI       neue Zeilen dieser Datei als Befehle ausführen (für Hintergrundbetrieb)
              --hash NICK            FL-Hash (CreateID) eines Nicknames ausgeben

            Befehle (ZIEL = Bot-Nummer oder "all"):
              list | say ZIEL TEXT | launch ZIEL | reconnect ZIEL | stop ZIEL | quit
            """),
        ["Cli.Commands"] = ("Commands: list | say TARGET TEXT | launch TARGET | reconnect TARGET | stop TARGET | quit",
                            "Befehle: list | say ZIEL TEXT | launch ZIEL | reconnect ZIEL | stop ZIEL | quit"),
        ["Cli.NeedsValue"] = ("{0} needs a value", "{0} braucht einen Wert"),
        ["Cli.UnknownOption"] = ("Unknown option {0}", "Unbekannte Option {0}"),
        ["Cli.NotFlserverCfg"] = ("{0} is not an FLServer.cfg", "{0} ist keine FLServer.cfg"),
        ["Cli.Chat"] = ("chat (from 0x{0:X}): {1}", "Chat (von 0x{0:X}): {1}"),
        ["Cli.Finished"] = ("Finished: {0}/{1} bots without errors.", "Beendet: {0}/{1} Bots ohne Fehler."),
    };

    /// <summary>Returns a translated, formatted text in the active language.</summary>
    /// <param name="key">Key, e.g. <c>Bot.Connecting</c>.</param>
    /// <param name="args">Values for the placeholders.</param>
    /// <returns>The text; the key itself if it is unknown.</returns>
    public static string Get(string key, params object?[] args)
    {
        if (!Table.TryGetValue(key, out var t))
            return key;
        string format = Language == "de" && t.De.Length > 0 ? t.De : t.En;
        return args.Length == 0 ? format : string.Format(Culture, format, args);
    }

    /// <summary>Short text for a bot state.</summary>
    /// <param name="state">State.</param>
    /// <returns>For example "In space" or "Im All".</returns>
    public static string StateLabel(BotState state) => Get($"State.{state}");
}
