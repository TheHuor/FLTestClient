using System.Diagnostics;
using System.Numerics;
using System.Text;

namespace FLTestClient;

/// <summary>State of a <see cref="Bot"/>.</summary>
public enum BotState
{
    /// <summary>Not started yet.</summary>
    Offline,
    /// <summary>DirectPlay connection in progress.</summary>
    Connecting,
    /// <summary>Connected, login in progress.</summary>
    LoggingIn,
    /// <summary>Logged in, character list requested.</summary>
    CharacterMenu,
    /// <summary>Character selected, waiting for CHARSELECTVERIFIED.</summary>
    Selecting,
    /// <summary>Docked at a base.</summary>
    InBase,
    /// <summary>Launch into space in progress.</summary>
    Launching,
    /// <summary>In space.</summary>
    InSpace,
    /// <summary>Disconnected cleanly.</summary>
    Disconnected,
    /// <summary>Connection rejected or dropped by the server.</summary>
    Failed,
}

/// <summary>A character from the character list (FLPACKET_SERVER_CHARACTERINFO).</summary>
/// <param name="Id">CHARACTER_ID, the character's file name (e.g. <c>09-234edf23.fl</c>).</param>
/// <param name="Name">Character name.</param>
/// <param name="Ship">Ship archetype (hash).</param>
/// <param name="Money">Money.</param>
/// <param name="System">System ID (hash) where the character was saved.</param>
/// <param name="Base">Base ID (hash); 0 = saved in space.</param>
/// <param name="Location">Room ID (hash) or 0.</param>
/// <param name="Rank">Rank.</param>
public sealed record CharacterEntry(string Id, string Name, uint Ship, uint Money, uint System, uint Base, uint Location, uint Rank);

/// <summary>A received chat message.</summary>
/// <param name="From">Sender CHAT_ID (0x10000 = server/console, otherwise a client ID).</param>
/// <param name="To">Receiver CHAT_ID.</param>
/// <param name="Text">Plain text extracted from the RDL.</param>
public sealed record ChatMessage(uint From, uint To, string Text);

/// <summary>Immutable state of a bot at one point in time, safe to read from any thread.</summary>
/// <param name="Index">Bot number.</param>
/// <param name="Name">Character name.</param>
/// <param name="Account">Account name.</param>
/// <param name="State">State.</param>
/// <param name="ClientId">Client ID on the server.</param>
/// <param name="SystemId">System ID (hash) or 0 if unknown.</param>
/// <param name="BaseId">Base ID (hash) or 0.</param>
/// <param name="ShipId">Object ID of the ship in space or 0.</param>
/// <param name="Cash">Money.</param>
/// <param name="PingMs">Last measured ping in ms or -1.</param>
/// <param name="UnknownPackets">Number of packets that could not be decoded.</param>
/// <param name="Position">Position in space.</param>
public sealed record BotSnapshot(int Index, string Name, string Account, BotState State, uint ClientId, uint SystemId,
    uint BaseId, uint ShipId, uint Cash, int PingMs, int UnknownPackets, Vector3 Position);

/// <summary>How a bot moves while it is in space.</summary>
public enum MovementPattern
{
    /// <summary>Stays at the launch point.</summary>
    Stationary,
    /// <summary>Flies a horizontal circle that starts at the launch point.</summary>
    Circle,
}

/// <summary>Settings shared by all bots of a session.</summary>
/// <remarks>
/// The behaviour settings (movement, session cycle, chat script) are read on every tick and may be changed
/// while the bots are running; simple value assignments are safe across threads here.
/// </remarks>
public sealed class BotOptions
{
    /// <summary>Server address. FLServer does not bind to 127.0.0.1, so use the LAN address.</summary>
    public string Host { get; set; } = "";

    /// <summary>UDP port of the server.</summary>
    public int Port { get; set; } = 2302;

    /// <summary>Session password of the server or empty.</summary>
    public string Password { get; set; } = "";

    /// <summary>Account prefix; bot n uses the account <c>{prefix}-{n:D3}</c>.</summary>
    public string AccountPrefix { get; set; } = "fltest";

    /// <summary>Character prefix; bot n is called <c>{prefix}{n:D2}</c>.</summary>
    public string NamePrefix { get; set; } = "TestBot";

    /// <summary>Faction for new characters (<c>[Faction]</c> in newcharacter.ini).</summary>
    public string Faction { get; set; } = "new_player";

    /// <summary>Start base for new characters.</summary>
    public string Base { get; set; } = "li01_01_base";

    /// <summary>Ship package for new characters (<c>[Package]</c>).</summary>
    public string Package { get; set; } = "ge_fighter";

    /// <summary>Pilot for new characters (<c>[Pilot]</c>).</summary>
    public string Pilot { get; set; } = "trent";

    /// <summary>Launch automatically 3 s after every docking.</summary>
    public bool AutoLaunch { get; set; }

    /// <summary>Interval of the position updates in space, in seconds.</summary>
    public double UpdateInterval { get; set; } = 0.5;

    /// <summary>Log every received packet.</summary>
    public bool Verbose { get; set; }

    /// <summary>Optional name lookup so the bot knows the system of a base after landing.</summary>
    public UniverseNames? Universe { get; set; }

    /// <summary>Movement in space.</summary>
    public MovementPattern Movement { get; set; } = MovementPattern.Stationary;

    /// <summary>Radius of the circle flight in metres.</summary>
    public double CircleRadius { get; set; } = 600;

    /// <summary>Speed of the circle flight in metres per second (normal FL cruise is 300).</summary>
    public double CircleSpeed { get; set; } = 80;

    /// <summary>Log out and in again periodically (tests saving, disconnect delay and login load).</summary>
    public bool SessionCycle { get; set; }

    /// <summary>Shortest time online per cycle, in seconds.</summary>
    public double OnlineMinSeconds { get; set; } = 120;

    /// <summary>Longest time online per cycle, in seconds.</summary>
    public double OnlineMaxSeconds { get; set; } = 300;

    /// <summary>Time offline between two sessions, in seconds.</summary>
    public double OfflineSeconds { get; set; } = 30;

    /// <summary>Interval of the chat script in seconds; 0 switches it off.</summary>
    public double ChatIntervalSeconds { get; set; }

    /// <summary>Lines of the chat script; a random one is sent per interval (text or FLHook command).</summary>
    public IReadOnlyList<string> ChatLines { get; set; } = Array.Empty<string>();
}

/// <summary>
/// A simulated player without graphics that speaks the real network protocol.
/// </summary>
/// <remarks>
/// <para>The flow follows the original client (login_mgr, menu_universe_base, space_state):
/// connect, CONNECTRESPONSE, LOGIN, LOGINRESPONSE, REQUESTCHARINFO, CHARACTERINFO, optionally CREATENEWCHAR,
/// SELECTCHARACTER, CHARSELECTVERIFIED, ENTERBASE; to launch EXITBASE, REQUESTCREATESHIP,
/// REQUESTCREATESHIPRESP, REQUESTLAUNCH, LAUNCH, LAUNCHCOMPLETE.</para>
/// <para>The class is not thread-safe: all methods must run on the same thread, normally the network thread
/// of <see cref="BotManager"/>. Other threads use <see cref="Snapshot"/>.</para>
/// </remarks>
public sealed class Bot : IDisposable
{
    private readonly BotOptions opt;
    private DPlayConnection? net;
    private Quaternion orientation = Quaternion.Identity;
    private bool createAttempted;
    private bool stopping;
    private CharacterEntry? selected;
    private bool launchWanted;
    private readonly Stopwatch clock = Stopwatch.StartNew();
    private double lastPing, lastUpdate, spaceStart;
    private double launchAt = -1;
    private double reconnectAt = -1;
    private int loginRetries;
    private bool pingOutstanding;
    private readonly Random random;
    private double cycleLogoutAt = -1;
    private double respawnAt = -1;
    private double nextChatAt = -1;
    private Vector3 circleCenter, circleU, circleV, departureDirection;
    private double circleRadius, circleAngle, lastMoveTime, departureLeft;

    /// <summary>Bot number; determines account and character name.</summary>
    public int Index { get; }

    /// <summary>Account name, e.g. <c>fltest-001</c>.</summary>
    public string Account { get; }

    /// <summary>Character name, e.g. <c>TestBot01</c>.</summary>
    public string CharName { get; }

    /// <summary>Current state.</summary>
    public BotState State { get; private set; } = BotState.Offline;

    /// <summary>Client ID from CONNECTRESPONSE.</summary>
    public uint ClientId { get; private set; }

    /// <summary>Base ID (hash) of the current or last base.</summary>
    public uint BaseId { get; private set; }

    /// <summary>System ID (hash) or 0 if unknown.</summary>
    public uint SystemId { get; private set; }

    /// <summary>Room ID (hash) on the base or 0.</summary>
    public uint RoomId { get; private set; }

    /// <summary>Object ID of the own ship in space.</summary>
    public uint ShipId { get; private set; }

    /// <summary>Money from the character list or SETCASH.</summary>
    public uint Cash { get; private set; }

    /// <summary>Last ping round trip in milliseconds or -1.</summary>
    public int PingMs { get; private set; } = -1;

    /// <summary>Position in space (start point from LAUNCH; the bot does not fly).</summary>
    public Vector3 Position { get; private set; }

    /// <summary>Character list of the account from the last CHARACTERINFO.</summary>
    public List<CharacterEntry> Characters { get; } = new();

    /// <summary>Number of packets whose format the bot does not know.</summary>
    public int UnknownPackets { get; private set; }

    /// <summary>Log line of this bot (already translated, without timestamp).</summary>
    public event Action<Bot, LogLevel, string>? Logged;

    /// <summary>Received chat message.</summary>
    public event Action<Bot, ChatMessage>? ChatReceived;

    /// <summary>Creates a bot; it connects only when <see cref="Start"/> is called.</summary>
    /// <param name="index">Bot number.</param>
    /// <param name="options">Shared settings.</param>
    public Bot(int index, BotOptions options)
    {
        Index = index;
        opt = options;
        Account = $"{options.AccountPrefix}-{index:D3}";
        CharName = $"{options.NamePrefix}{index:D2}";
        // Different seed per bot so that cycles and chat lines do not run in lockstep.
        random = new Random(unchecked(index * 7919 + Environment.TickCount));
    }

    private double Now => clock.Elapsed.TotalSeconds;

    /// <summary>Raises <see cref="Logged"/> with a translated text from <see cref="Texts"/>.</summary>
    /// <param name="level">Severity.</param>
    /// <param name="key">Text key, e.g. <c>Bot.Connecting</c>.</param>
    /// <param name="args">Values for the placeholders.</param>
    public void Log(LogLevel level, string key, params object?[] args) => Logged?.Invoke(this, level, Texts.Get(key, args));

    /// <summary>Creates a snapshot of the current state.</summary>
    /// <returns>Immutable copy for other threads.</returns>
    public BotSnapshot Snapshot() =>
        new(Index, CharName, Account, State, ClientId, SystemId, BaseId, ShipId, Cash, PingMs, UnknownPackets, Position);

    /// <summary>(Re)connects. An existing connection is discarded.</summary>
    public void Start()
    {
        net?.Dispose();
        net = new DPlayConnection();
        stopping = false;
        State = BotState.Connecting;
        createAttempted = false;
        Log(LogLevel.Info, "Bot.Connecting", opt.Host, opt.Port, Account);
        net.Connect(opt.Host, opt.Port, opt.Password);
    }

    /// <summary>Disconnects cleanly; the server logs the player out normally.</summary>
    public void Stop()
    {
        stopping = true;
        reconnectAt = -1;
        net?.Close();
        if (State != BotState.Failed)
            State = BotState.Disconnected;
    }

    /// <summary>Releases the DirectPlay connection.</summary>
    public void Dispose() => net?.Dispose();

    /// <summary>
    /// One tick: process network events, send the ping, run a scheduled launch and report the position in
    /// space. Should be called about 20 times per second.
    /// </summary>
    public void Tick()
    {
        if (net == null)
            return;
        if (reconnectAt >= 0 && Now >= reconnectAt)
        {
            reconnectAt = -1;
            Start();
            return;
        }
        while (net.Events.TryDequeue(out var ev))
            HandleNetEvent(ev);

        double now = Now;
        if (net.Connected && !pingOutstanding && now - lastPing >= 1.0)
        {
            // CDPClient::DispatchMsgs: 1-byte ping (value 1) once per second.
            lastPing = now;
            pingOutstanding = net.Send(new byte[] { 1 });
        }

        if (respawnAt >= 0 && now >= respawnAt)
        {
            // Respawn: request the character list again; the normal selection flow docks the bot at the base
            // the server put the dead character on.
            respawnAt = -1;
            Log(LogLevel.Event, "Bot.Respawn");
            State = BotState.CharacterMenu;
            Send(new PacketWriter(Ch.Client, ClientOp.RequestCharInfo));
        }

        if (State == BotState.InBase && launchAt >= 0 && now >= launchAt)
        {
            launchAt = -1;
            Launch();
        }

        if (State == BotState.InSpace && now - lastUpdate >= opt.UpdateInterval)
        {
            lastUpdate = now;
            Move(now);
            SendObjUpdate(now - spaceStart);
        }

        bool active = State is BotState.InBase or BotState.InSpace;
        TickSessionCycle(now, active);
        TickChatScript(now, active);
    }

    // Session cycle: after a random time online, log out cleanly and come back after OfflineSeconds.
    private void TickSessionCycle(double now, bool active)
    {
        if (!opt.SessionCycle)
        {
            cycleLogoutAt = -1;
            return;
        }
        if (cycleLogoutAt < 0)
        {
            if (active)
            {
                double min = Math.Max(5, opt.OnlineMinSeconds), max = Math.Max(min, opt.OnlineMaxSeconds);
                cycleLogoutAt = now + min + random.NextDouble() * (max - min);
            }
            return;
        }
        if (now < cycleLogoutAt)
            return;

        double offline = Math.Max(5, opt.OfflineSeconds);
        Log(LogLevel.Event, "Bot.CycleLogout", offline);
        stopping = true;
        net?.Close();
        State = BotState.Disconnected;
        cycleLogoutAt = -1;
        nextChatAt = -1;
        reconnectAt = now + offline;
    }

    // Chat script: a random line every ChatIntervalSeconds (scattered by +-50 %).
    private void TickChatScript(double now, bool active)
    {
        var lines = opt.ChatLines;
        if (!active || opt.ChatIntervalSeconds <= 0 || lines.Count == 0)
        {
            nextChatAt = -1;
            return;
        }
        if (nextChatAt < 0)
        {
            nextChatAt = now + opt.ChatIntervalSeconds * (0.5 + random.NextDouble());
            return;
        }
        if (now < nextChatAt)
            return;
        Say(lines[random.Next(lines.Count)]);
        nextChatAt = now + opt.ChatIntervalSeconds * (0.5 + random.NextDouble());
    }

    // Circle flight like a player leaving a base: first a straight leg of DepartureDistance along the launch
    // direction (away from the dock, which avoids crashing into the station or planet), then a horizontal
    // circle that starts at the end of the leg and lies further out. Y is up in FL; ships look along -Z.
    private const double DepartureDistance = 1000;

    private void Move(double now)
    {
        double dt = Math.Clamp(now - lastMoveTime, 0, 2);
        lastMoveTime = now;
        if (opt.Movement != MovementPattern.Circle || circleRadius <= 0)
            return;
        double step = Math.Max(0, opt.CircleSpeed) * dt;

        if (departureLeft > 0)
        {
            double d = Math.Min(step, departureLeft);
            departureLeft -= d;
            Position += departureDirection * (float)d;
            orientation = Facing(departureDirection);
            return;
        }

        circleAngle += step / circleRadius;
        var radial = circleU * (float)Math.Cos(circleAngle) + circleV * (float)Math.Sin(circleAngle);
        Position = circleCenter + radial * (float)circleRadius;
        orientation = Facing(-circleU * (float)Math.Sin(circleAngle) + circleV * (float)Math.Cos(circleAngle));
    }

    // Prepares the departure leg and the circle from the launch position and orientation.
    private void PlanCircle()
    {
        circleRadius = Math.Max(50, opt.CircleRadius);
        var forward = Vector3.Transform(-Vector3.UnitZ, orientation);
        forward.Y = 0;
        forward = forward.LengthSquared() < 1e-4f ? -Vector3.UnitZ : Vector3.Normalize(forward);
        departureDirection = forward;
        departureLeft = DepartureDistance;
        // Circle through the end of the leg, centre one radius further out: angle 0 is the leg's end point.
        var legEnd = Position + forward * (float)DepartureDistance;
        circleCenter = legEnd + forward * (float)circleRadius;
        circleU = -forward;
        circleV = new Vector3(forward.Z, 0, -forward.X);
        circleAngle = 0;
    }

    private static Quaternion Facing(Vector3 direction) =>
        Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.Atan2(-direction.X, -direction.Z));

    private void HandleNetEvent(NetEvent ev)
    {
        switch (ev.Kind)
        {
            case NetEventKind.Connected:
                Log(LogLevel.Info, "Bot.Connected");
                State = BotState.LoggingIn;
                break;
            case NetEventKind.ConnectFailed:
                // connect_handshake.cpp: the client treats 0x80158410 as "password required/wrong".
                if (ev.Result == DP8.DPNERR_INVALIDPASSWORD)
                    Log(LogLevel.Error, "Bot.WrongPassword");
                else
                    Log(LogLevel.Error, "Bot.ConnectFailed", ev.Result);
                State = BotState.Failed;
                break;
            case NetEventKind.Disconnected:
                if (stopping)
                {
                    State = BotState.Disconnected;
                    break;
                }
                Log(LogLevel.Error, "Bot.ServerDisconnected", ev.Result);
                State = BotState.Failed;
                break;
            case NetEventKind.Ping:
                pingOutstanding = false;
                PingMs = (int)((Now - lastPing) * 1000);
                break;
            case NetEventKind.Data:
                Dispatch(Codec.MaybeInflate(ev.Data!));
                break;
        }
    }

    // RpcDispatchMsgs: a message may hold several packets. An unknown packet has no known length,
    // so the rest of the message is dropped.
    private void Dispatch(byte[] data)
    {
        var r = new PacketReader(data);
        while (r.Remaining >= 2)
        {
            int opcode = r.U8();
            int channel = r.U8();
            int start = r.Pos;
            bool known;
            try
            {
                known = HandlePacket(channel, opcode, r);
            }
            catch (EndOfStreamException e)
            {
                Log(LogLevel.Error, "Bot.DecodeError", PacketNames.Name(channel, opcode), e.Message);
                return;
            }
            if (opt.Verbose)
                Logged?.Invoke(this, LogLevel.Info, $"<- {PacketNames.Name(channel, opcode)} ({(known ? r.Pos - start : data.Length - start)} B)");
            if (!known)
            {
                UnknownPackets++;
                if (opt.Verbose && (channel < 1 || channel > 3))
                    Logged?.Invoke(this, LogLevel.Info, $"   hex: {Convert.ToHexString(data, start - 2, Math.Min(64, data.Length - start + 2))}");
                return;
            }
        }
    }

    private bool HandlePacket(int channel, int opcode, PacketReader r)
    {
        if (channel == Ch.Server)
        {
            switch (opcode)
            {
                case ServerOp.ConnectResponse:
                    ClientId = r.U32();
                    Log(LogLevel.Info, "Bot.ConnectResponse", ClientId);
                    SendLogin();
                    return true;
                case ServerOp.LoginResponse:
                    OnLoginResponse(r.U8());
                    return true;
                case ServerOp.CharacterInfo:
                    OnCharacterInfo(r);
                    return true;
                case ServerOp.CharSelectVerified:
                    r.U32(); // rep instance
                    OnCharSelectVerified(r.F64());
                    return true;
                case ServerOp.SetStartRoom:
                    BaseId = r.U32();
                    RoomId = r.U32();
                    Log(LogLevel.Info, "Bot.SetStartRoom", BaseId, RoomId);
                    EnterBase(BaseId);
                    return true;
                case ServerOp.RequestCreateShipResp:
                {
                    byte ok = r.U8();
                    uint ship = r.U32();
                    OnCreateShipResp(ok != 0, ship);
                    return true;
                }
                case ServerOp.Launch:
                {
                    uint ship = r.U32();
                    uint baseId = r.U32();
                    r.U32(); // state
                    Position = r.Vec();
                    orientation = r.Quat();
                    OnLaunch(ship, baseId);
                    return true;
                }
                case ServerOp.Land:
                {
                    // FServerLandInfo: ship, dock, base (RemoteClient SendServerLand). Also sent for NPCs in the
                    // same system, so only the own ship counts.
                    uint ship = r.U32();
                    r.U32();
                    uint baseId = r.U32();
                    if (ship == ShipId)
                        OnLand(baseId);
                    return true;
                }
                case ServerOp.GfDestroyNewsBroadcast:
                {
                    // The retail client reads through null for an unknown base ID (FUN_0043a750).
                    uint baseId = r.U32();
                    uint key = r.U32();
                    Log(LogLevel.Info, "Bot.NewsDestroy", baseId, key);
                    return true;
                }
                case ServerOp.CreateShip:
                    // Only the object ID is decoded (for verbose logging); the variable rest stays unknown,
                    // so the remainder of the message is dropped like for any unknown packet.
                    if (opt.Verbose && r.Remaining >= 4)
                        Logged?.Invoke(this, LogLevel.Info, $"   createship object {r.U32()}");
                    return false;
                case ServerOp.SetCash:
                    Cash = r.U32();
                    return true;
                case ServerOp.DestroyObject:
                {
                    uint obj = r.U32();
                    r.U8();
                    if (obj == ShipId && State == BotState.InSpace)
                    {
                        // Like a player after death: respawn at the last base a few seconds later.
                        Log(LogLevel.Error, "Bot.ShipDestroyed");
                        State = BotState.Launching;
                        ShipId = 0;
                        respawnAt = Now + 5.0;
                    }
                    return true;
                }
            }
        }
        else if (channel == Ch.Common)
        {
            switch (opcode)
            {
                case CommonOp.UpdateObject:
                {
                    // Position updates of other ships; only decoded for verbose logging.
                    r.U8();
                    uint obj = r.U32();
                    var pos = r.Vec();
                    r.Skip(4 + 1 + 4);
                    if (opt.Verbose)
                        Logged?.Invoke(this, LogLevel.Info, $"   object {obj} at ({pos.X:F0}, {pos.Y:F0}, {pos.Z:F0})");
                    return true;
                }
                case CommonOp.ChatMsg:
                {
                    int size = (int)r.U32();
                    byte[] rdl = r.Bytes(size);
                    uint target = r.U32();
                    uint from = r.U32();
                    string text = Rdl.ExtractText(rdl);
                    if (text.Trim().Length > 0)
                        ChatReceived?.Invoke(this, new ChatMessage(from, target, text));
                    return true;
                }
            }
        }
        return false;
    }

    // ---------------------------------------------------------------- login and character

    private void SendLogin()
    {
        // FRpcClientLoginPacket: create flag, u8 account length, u8 password length, then UTF-16.
        // The MP client sends bCreateAccount = 1; FLServer then creates the account when needed.
        string account = Account.ToLowerInvariant();
        var w = new PacketWriter(Ch.Client, ClientOp.Login)
            .U8(1).U8((byte)account.Length).U8(0)
            .Bytes(Encoding.Unicode.GetBytes(account));
        Send(w);
    }

    private void OnLoginResponse(byte status)
    {
        if (status == 1 && loginRetries < 6)
        {
            // Account still logged in (stale session after a hard abort, until the server drops it).
            loginRetries++;
            Log(LogLevel.Info, "Bot.AccountBusy");
            net?.Close();
            reconnectAt = Now + 10.0;
            return;
        }
        // CLoginMgr::Login only treats status 3 as success.
        if (status != 3)
        {
            Log(LogLevel.Error, "Bot.LoginRejected", status);
            State = BotState.Failed;
            return;
        }
        Log(LogLevel.Info, "Bot.LoggedIn");
        State = BotState.CharacterMenu;
        Send(new PacketWriter(Ch.Client, ClientOp.RequestCharInfo));
    }

    private void OnCharacterInfo(PacketReader r)
    {
        Characters.Clear();
        int count = r.U8();
        for (int i = 0; i < count; i++)
        {
            string id = r.Ascii(r.U16());
            r.U8();
            r.U8();
            string name = r.Wide(r.U16());
            r.Wide(r.U16());
            // 15 dwords in wire order (rpc_server_channel_receive: +0x224, +0x22c, +0x228, ship, money, system,
            // base, location, voice, rank, ?, hull, kills, mission failures, mission successes).
            var f = new uint[15];
            for (int k = 0; k < f.Length; k++)
                f[k] = r.U32();
            SkipIdBlock(r);
            SkipIdBlock(r);
            int equip = r.U8();
            for (int e = 0; e < equip; e++)
            {
                r.Skip(4 + 4 + 4 + 2 + 1 + 1);
                r.Skip(r.U16());
            }
            uint groups = r.U32();
            r.Skip((int)groups * 6);
            Characters.Add(new CharacterEntry(id, name, f[3], f[4], f[5], f[6], f[7], f[9]));
        }
        r.U32(); // save failure count
        Log(LogLevel.Info, "Bot.CharacterInfo", count, count > 0 ? ": " + string.Join(", ", Characters.Select(c => c.Name)) : "");

        // After CREATENEWCHAR the list may arrive twice (answer plus our own request).
        if (State != BotState.CharacterMenu)
            return;
        var mine = Characters.FirstOrDefault(c => string.Equals(c.Name, CharName, StringComparison.OrdinalIgnoreCase))
                   ?? Characters.FirstOrDefault();
        if (mine != null)
        {
            SelectCharacter(mine);
            return;
        }
        if (createAttempted)
        {
            Log(LogLevel.Error, "Bot.CreateFailed");
            State = BotState.Failed;
            return;
        }
        CreateCharacter();
    }

    private static void SkipIdBlock(PacketReader r)
    {
        if (r.U8() == 0)
            return;
        r.Skip(16);
        r.Skip((int)r.U32() * 4);
    }

    private void CreateCharacter()
    {
        createAttempted = true;
        Log(LogLevel.Event, "Bot.Creating", CharName, opt.Faction, opt.Base, opt.Package, opt.Pilot);
        // SCreateCharacterInfo: faction, base, package, pilot (in this order on the wire).
        var w = new PacketWriter(Ch.Client, ClientOp.CreateNewChar)
            .WideString(CharName)
            .U32(FLHash.CreateID(opt.Faction))
            .U32(FLHash.CreateID(opt.Base))
            .U32(FLHash.CreateID(opt.Package))
            .U32(FLHash.CreateID(opt.Pilot));
        Send(w);
        // CLoginMgr::CreateNewCharacter requests the list again if the answer was empty.
        Send(new PacketWriter(Ch.Client, ClientOp.RequestCharInfo));
    }

    private void SelectCharacter(CharacterEntry c)
    {
        State = BotState.Selecting;
        selected = c;
        Cash = c.Money;
        SystemId = c.System;
        Log(LogLevel.Info, "Bot.Selecting", c.Name, c.Id);
        var w = new PacketWriter(Ch.Client, ClientOp.SelectCharacter).U16((ushort)c.Id.Length)
            .Bytes(Encoding.ASCII.GetBytes(c.Id));
        Send(w);
    }

    private void OnCharSelectVerified(double serverTime)
    {
        Log(LogLevel.Info, "Bot.CharSelectVerified", serverTime);
        if (State != BotState.Selecting && BaseId != 0)
        {
            // Character selection triggered by the server, e.g. an FLHook beam into another system
            // (HkBeam then emulates an F1 character load). The client enters the base again.
            Log(LogLevel.Event, "Bot.ServerCharSelect");
            EnterBase(BaseId);
        }
        else if (selected != null && State == BotState.Selecting)
        {
            // In MP there is no SETSTARTROOM: the client enters the base from CHARACTERINFO.
            RoomId = selected.Location;
            if (selected.Base != 0)
                EnterBase(selected.Base);
            else
                SpawnInSpace();
        }
    }

    // ---------------------------------------------------------------- base and space

    private string NameOf(uint id) => opt.Universe?.Name(id) ?? $"0x{id:X8}";

    private void EnterBase(uint baseId)
    {
        BaseId = baseId;
        uint system = opt.Universe?.SystemOfBase(baseId) ?? 0;
        if (system != 0)
            SystemId = system;
        State = BotState.InBase;
        Send(new PacketWriter(Ch.Client, ClientOp.EnterBase).U32(baseId));
        if (RoomId != 0)
            Send(new PacketWriter(Ch.Client, ClientOp.EnterLocation).U32(RoomId));
        Log(LogLevel.Event, "Bot.Docked", NameOf(baseId));
        // Wait briefly like a player in the base menu; with AutoLaunch launch again after every docking.
        launchAt = launchWanted || opt.AutoLaunch ? Now + 3.0 : -1;
    }

    /// <summary>
    /// Launches from the current base into space. If the bot is not docked right now, the launch is
    /// remembered and runs the next time it enters a base.
    /// </summary>
    public void Launch()
    {
        if (State != BotState.InBase)
        {
            launchWanted = true;
            return;
        }
        launchWanted = false;
        State = BotState.Launching;
        if (RoomId != 0)
            Send(new PacketWriter(Ch.Client, ClientOp.ExitLocation).U32(RoomId));
        Send(new PacketWriter(Ch.Client, ClientOp.ExitBase).U32(BaseId));
        Send(new PacketWriter(Ch.Client, ClientOp.RequestCreateShip));
        Log(LogLevel.Info, "Bot.Launching");
    }

    // Character was saved in space (base 0): the client goes straight to the space state without a base.
    // The server would answer ENTERBASE(0) with a kick.
    private void SpawnInSpace()
    {
        launchWanted = false;
        BaseId = 0;
        State = BotState.Launching;
        Send(new PacketWriter(Ch.Client, ClientOp.RequestCreateShip));
        Log(LogLevel.Info, "Bot.SavedInSpace", NameOf(SystemId));
    }

    private void OnCreateShipResp(bool ok, uint ship)
    {
        if (!ok)
        {
            Log(LogLevel.Error, "Bot.CreateShipRejected");
            EnterBase(BaseId);
            return;
        }
        ShipId = ship;
        Send(new PacketWriter(Ch.Client, ClientOp.RequestLaunch).U32(ship));
    }

    private void OnLaunch(uint ship, uint baseId)
    {
        ShipId = ship;
        Log(LogLevel.Success, "Bot.Launched", ship, Position.X, Position.Y, Position.Z);
        Send(new PacketWriter(Ch.Client, ClientOp.LaunchComplete).U32(baseId).U32(ship));
        State = BotState.InSpace;
        spaceStart = Now;
        lastUpdate = 0;
        lastMoveTime = spaceStart;
        RoomId = 0;
        PlanCircle();
    }

    private void OnLand(uint baseId)
    {
        Log(LogLevel.Event, "Bot.Landed", NameOf(baseId));
        EnterBase(baseId);
    }

    private void SendObjUpdate(double elapsed)
    {
        // FRpcUpdateObjectPacket: u8 state, ship, position, quaternion x/y/z/w * 127, throttle * 127,
        // f32 time since the ship was created (client_objdb FRemotePhysBlock::ElapsedTime).
        var q = orientation;
        var w = new PacketWriter(Ch.Common, CommonOp.UpdateObject)
            .U8(0).U32(ShipId).Vec(Position)
            .U8(Quantize(q.X)).U8(Quantize(q.Y)).U8(Quantize(q.Z)).U8(Quantize(q.W))
            .U8(0)
            .F32((float)elapsed);
        Send(w);
    }

    private static byte Quantize(float v) => unchecked((byte)(sbyte)(int)(v * 127.0));

    // ---------------------------------------------------------------- chat

    /// <summary>
    /// Sends a chat message. FLHook commands such as <c>/help</c> also go through the chat.
    /// </summary>
    /// <param name="text">Text.</param>
    /// <param name="target">CHAT_ID of the receiver; 0x10000 = universe.</param>
    public void Say(string text, uint target = 0x10000)
    {
        byte[] rdl = Rdl.Text(text);
        var w = new PacketWriter(Ch.Common, CommonOp.ChatMsg).U32((uint)rdl.Length).Bytes(rdl).U32(target).U32(0);
        Send(w);
        Log(LogLevel.Event, "Bot.Says", text);
    }

    private void Send(PacketWriter w)
    {
        if (net == null || !net.Send(w.ToArray()))
            Log(LogLevel.Error, "Bot.SendFailed");
    }

    /// <summary>One-line summary for console output.</summary>
    /// <returns>Name, state, system, ship, money and ping.</returns>
    public override string ToString() =>
        $"{CharName,-10} {Texts.StateLabel(State),-16} Client {ClientId,3}  {(SystemId == 0 ? "" : NameOf(SystemId)),-12} " +
        $"Ship {ShipId,6}  Cash {Cash,9}  Ping {PingMs,4} ms  ?{UnknownPackets}";
}
