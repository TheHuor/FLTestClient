using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using FLTestClient;

// --lang first, so that --help and error messages already use the right language.
for (int i = 0; i + 1 < args.Length; i++)
{
    if (args[i] == "--lang")
        Texts.Language = args[i + 1];
}

var options = new BotOptions();
int count = 1, start = 1;
double? duration = null;
double stagger = 0.5;
var sayOnLogin = new List<string>();
string? commandFile = null;
long commandFileOffset = 0;

for (int i = 0; i < args.Length; i++)
{
    string Next() => i + 1 < args.Length ? args[++i] : throw new ArgumentException(Texts.Get("Cli.NeedsValue", args[i]));
    switch (args[i])
    {
        case "--server":
        {
            string v = Next();
            int colon = v.LastIndexOf(':');
            if (colon > 0)
            {
                options.Host = v[..colon];
                options.Port = int.Parse(v[(colon + 1)..], CultureInfo.InvariantCulture);
            }
            else
            {
                options.Host = v;
            }
            break;
        }
        case "--password": options.Password = Next(); break;
        case "--flserver-cfg": options.Password = ReadServerPassword(Next()); break;
        case "--count": count = int.Parse(Next(), CultureInfo.InvariantCulture); break;
        case "--start": start = int.Parse(Next(), CultureInfo.InvariantCulture); break;
        case "--account": options.AccountPrefix = Next(); break;
        case "--name": options.NamePrefix = Next(); break;
        case "--launch": options.AutoLaunch = true; break;
        case "--say": sayOnLogin.Add(Next()); break;
        case "--duration": duration = double.Parse(Next(), CultureInfo.InvariantCulture); break;
        case "--stagger": stagger = double.Parse(Next(), CultureInfo.InvariantCulture); break;
        case "--data": options.Universe = UniverseNames.Load(Next()); break;
        case "--faction": options.Faction = Next(); break;
        case "--base": options.Base = Next(); break;
        case "--package": options.Package = Next(); break;
        case "--pilot": options.Pilot = Next(); break;
        case "--lang": Next(); break; // already handled above
        case "--circle":
        {
            options.Movement = MovementPattern.Circle;
            // Optional value RADIUS[:SPEED]; without it the defaults are used.
            if (i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal))
            {
                var p = Next().Split(':');
                options.CircleRadius = double.Parse(p[0], CultureInfo.InvariantCulture);
                if (p.Length > 1)
                    options.CircleSpeed = double.Parse(p[1], CultureInfo.InvariantCulture);
            }
            break;
        }
        case "--cycle":
        {
            // MIN-MAX:OFFLINE in seconds, e.g. 120-300:30
            var p = Next().Split(':');
            var range = p[0].Split('-');
            options.SessionCycle = true;
            options.OnlineMinSeconds = double.Parse(range[0], CultureInfo.InvariantCulture);
            options.OnlineMaxSeconds = double.Parse(range.Length > 1 ? range[1] : range[0], CultureInfo.InvariantCulture);
            if (p.Length > 1)
                options.OfflineSeconds = double.Parse(p[1], CultureInfo.InvariantCulture);
            break;
        }
        case "--chat-interval": options.ChatIntervalSeconds = double.Parse(Next(), CultureInfo.InvariantCulture); break;
        case "--chat-line": options.ChatLines = options.ChatLines.Append(Next()).ToArray(); break;
        case "--verbose": options.Verbose = true; break;
        case "--commands": commandFile = Next(); break;
        case "--hash":
        {
            string nick = Next();
            Console.WriteLine($"{nick} = 0x{FLHash.CreateID(nick):X8}");
            return 0;
        }
        case "-h":
        case "--help":
            Console.WriteLine(Texts.Get("Cli.Usage"));
            return 0;
        default:
            Console.Error.WriteLine($"{Texts.Get("Cli.UnknownOption", args[i])}\n\n{Texts.Get("Cli.Usage")}");
            return 2;
    }
}

if (options.Host == "")
{
    // FLServer only binds to real interfaces, not to 127.0.0.1.
    options.Host = Dns.GetHostAddresses(Dns.GetHostName())
        .FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(a))?.ToString()
        ?? "127.0.0.1";
}

if (commandFile != null && File.Exists(commandFile))
    commandFileOffset = new FileInfo(commandFile).Length;

using var manager = new BotManager(options) { StaggerSeconds = stagger };
manager.Logged += (bot, _, text) => Console.WriteLine($"{DateTime.Now:HH:mm:ss.fff} [{bot?.CharName ?? "Manager"}] {text}");
manager.ChatReceived += (bot, msg) =>
    Console.WriteLine($"{DateTime.Now:HH:mm:ss.fff} [{bot.CharName}] {Texts.Get("Cli.Chat", msg.From, msg.Text)}");
manager.Post(m => m.AddBots(start, count));

var greeted = new HashSet<int>();
var commands = new ConcurrentQueue<string>();
bool quit = false;

Console.CancelKeyPress += (_, e) => { e.Cancel = true; quit = true; };
if (duration == null)
{
    var input = new Thread(() =>
    {
        string? line;
        while ((line = Console.ReadLine()) != null)
            commands.Enqueue(line);
        commands.Enqueue("quit");
    }) { IsBackground = true };
    input.Start();
    Console.WriteLine(Texts.Get("Cli.Commands"));
}

var started = DateTime.UtcNow;
while (!quit)
{
    if (sayOnLogin.Count > 0)
    {
        foreach (var s in manager.Snapshots)
        {
            if (s.State is BotState.InBase or BotState.InSpace && greeted.Add(s.Index))
                manager.Post(m => { foreach (var text in sayOnLogin) m.Find(s.Index)?.Say(text); });
        }
    }

    if (commandFile != null)
        ReadCommandFile();
    while (commands.TryDequeue(out var cmd))
        quit |= RunCommand(cmd.Trim());

    if (duration != null && (DateTime.UtcNow - started).TotalSeconds >= duration)
        quit = true;
    Thread.Sleep(100);
}

var final = manager.Snapshots;
int failed = final.Count(s => s.State == BotState.Failed);
Console.WriteLine(Texts.Get("Cli.Finished", final.Count - failed, final.Count));
return failed == 0 ? 0 : 1;

// FLServer.cfg (ServerConfigDlg): int version, wchar name[0x21], wchar description[0x81], wchar password[0x11], ...
static string ReadServerPassword(string path)
{
    byte[] cfg = File.ReadAllBytes(path);
    const int offset = 4 + 0x21 * 2 + 0x81 * 2;
    if (cfg.Length < offset + 0x22)
        throw new ArgumentException(Texts.Get("Cli.NotFlserverCfg", path));
    return System.Text.Encoding.Unicode.GetString(cfg, offset, 0x22).Split('\0')[0];
}

// Only lines appended after the start; an incomplete last line waits for its line break.
void ReadCommandFile()
{
    try
    {
        using var fs = new FileStream(commandFile!, FileMode.OpenOrCreate, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        if (fs.Length < commandFileOffset)
            commandFileOffset = 0;
        fs.Seek(commandFileOffset, SeekOrigin.Begin);
        var bytes = new byte[fs.Length - commandFileOffset];
        int read = fs.Read(bytes, 0, bytes.Length);
        int end = Array.LastIndexOf(bytes, (byte)'\n', read - 1);
        if (read == 0 || end < 0)
            return;
        commandFileOffset += end + 1;
        foreach (var line in System.Text.Encoding.UTF8.GetString(bytes, 0, end).Split('\n'))
            commands.Enqueue(line.TrimEnd('\r'));
    }
    catch (IOException)
    {
    }
}

bool RunCommand(string cmd)
{
    if (cmd.Length == 0)
        return false;
    var parts = cmd.Split(' ', 3, StringSplitOptions.RemoveEmptyEntries);
    void ForTargets(string which, Action<BotManager, Bot> action) => manager.Post(m =>
    {
        foreach (var bot in m.Bots.Where(b => which == "all" || b.Index.ToString(CultureInfo.InvariantCulture) == which).ToList())
            action(m, bot);
    });

    switch (parts[0])
    {
        case "quit":
        case "exit":
            return true;
        case "list":
            manager.Post(m => { foreach (var bot in m.Bots) Console.WriteLine(bot); });
            break;
        case "say" when parts.Length == 3:
            ForTargets(parts[1], (_, bot) => bot.Say(parts[2]));
            break;
        case "launch" when parts.Length >= 2:
            ForTargets(parts[1], (_, bot) => bot.Launch());
            break;
        case "reconnect" when parts.Length >= 2:
            ForTargets(parts[1], (_, bot) => { bot.Stop(); bot.Start(); });
            break;
        case "stop" when parts.Length >= 2:
            ForTargets(parts[1], (m, bot) => m.Remove(bot));
            break;
        default:
            Console.WriteLine(Texts.Get("Cli.Commands"));
            break;
    }
    return false;
}
