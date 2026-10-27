namespace FLTestClient;

/// <summary>Channels of the FL RPC protocol (RegisterChannel1/2/3Packets in RemoteServer.dll).</summary>
internal static class Ch
{
    /// <summary>COMMON: both directions (movement, chat, weapons).</summary>
    public const int Common = 1;
    /// <summary>SERVER: server to client.</summary>
    public const int Server = 2;
    /// <summary>CLIENT: client to server.</summary>
    public const int Client = 3;
}

/// <summary>Opcodes of the CLIENT channel (FLPACKET_CLIENT_*).</summary>
internal static class ClientOp
{
    /// <summary>LOGIN: create flag, account, password.</summary>
    public const int Login = 0x01;
    /// <summary>REQUESTLAUNCH: ship ID.</summary>
    public const int RequestLaunch = 0x04;
    /// <summary>REQUESTCHARINFO: no payload.</summary>
    public const int RequestCharInfo = 0x05;
    /// <summary>SELECTCHARACTER: CHARACTER_ID (file name).</summary>
    public const int SelectCharacter = 0x06;
    /// <summary>ENTERBASE: base ID.</summary>
    public const int EnterBase = 0x07;
    /// <summary>EXITBASE: base ID.</summary>
    public const int ExitBase = 0x0d;
    /// <summary>ENTERLOCATION: room ID.</summary>
    public const int EnterLocation = 0x0e;
    /// <summary>EXITLOCATION: room ID.</summary>
    public const int ExitLocation = 0x0f;
    /// <summary>REQUESTCREATESHIP: no payload.</summary>
    public const int RequestCreateShip = 0x10;
    /// <summary>CREATENEWCHAR: name, faction, base, package, pilot.</summary>
    public const int CreateNewChar = 0x39;
    /// <summary>LAUNCHCOMPLETE: base, ship.</summary>
    public const int LaunchComplete = 0x3e;
}

/// <summary>Opcodes of the SERVER channel (FLPACKET_SERVER_*) that the bot handles.</summary>
internal static class ServerOp
{
    /// <summary>CONNECTRESPONSE: client ID.</summary>
    public const int ConnectResponse = 0x01;
    /// <summary>LOGINRESPONSE: status (3 = OK, 1 = account already logged in).</summary>
    public const int LoginResponse = 0x02;
    /// <summary>CHARACTERINFO: character list.</summary>
    public const int CharacterInfo = 0x03;
    /// <summary>CREATESHIP: object ID first, then a long variable part.</summary>
    public const int CreateShip = 0x04;
    /// <summary>DESTROYOBJECT: object ID, byte.</summary>
    public const int DestroyObject = 0x06;
    /// <summary>LAUNCH: ship, base, state, position, orientation.</summary>
    public const int Launch = 0x07;
    /// <summary>CHARSELECTVERIFIED: rep instance, server time.</summary>
    public const int CharSelectVerified = 0x08;
    /// <summary>LAND: ship, dock, base.</summary>
    public const int Land = 0x0b;
    /// <summary>SETSTARTROOM: base, room (single player only).</summary>
    public const int SetStartRoom = 0x0d;
    /// <summary>GFDESTROYNEWSBROADCAST: base, key.</summary>
    public const int GfDestroyNewsBroadcast = 0x14;
    /// <summary>REQUESTCREATESHIPRESP: success, ship ID.</summary>
    public const int RequestCreateShipResp = 0x27;
    /// <summary>SETCASH: money.</summary>
    public const int SetCash = 0x30;
}

/// <summary>Opcodes of the COMMON channel that the bot uses.</summary>
internal static class CommonOp
{
    /// <summary>UPDATEOBJECT: position and orientation of a ship.</summary>
    public const int UpdateObject = 0x01;
    /// <summary>CHATMSG: RDL text, target, sender.</summary>
    public const int ChatMsg = 0x05;
}

/// <summary>Readable packet names for log output.</summary>
public static class PacketNames
{
    private static readonly Dictionary<int, string> Names = new()
    {
        [0x0101] = "COMMON_UPDATEOBJECT", [0x0102] = "COMMON_FIREWEAPON", [0x0104] = "COMMON_SETTARGET",
        [0x0105] = "COMMON_CHATMSG", [0x0108] = "COMMON_ACTIVATEEQUIP", [0x010e] = "COMMON_ACTIVATECRUISE",
        [0x010f] = "COMMON_GOTRADELANE", [0x0110] = "COMMON_STOPTRADELANE", [0x0111] = "COMMON_SET_WEAPON_GROUP",
        [0x0112] = "COMMON_PLAYER_TRADE", [0x0113] = "COMMON_SET_VISITED_STATE", [0x0114] = "COMMON_JETTISONCARGO",
        [0x0115] = "COMMON_ACTIVATETHRUSTERS", [0x0116] = "COMMON_REQUEST_BEST_PATH",
        [0x0117] = "COMMON_REQUEST_GROUP_POSITIONS", [0x0118] = "COMMON_REQUEST_PLAYER_STATS",
        [0x0119] = "COMMON_SET_MISSION_LOG", [0x011a] = "COMMON_REQUEST_RANK_LEVEL", [0x011b] = "COMMON_POP_UP_DIALOG",
        [0x011c] = "COMMON_SET_INTERFACE_STATE", [0x011d] = "COMMON_TRACTOROBJECTS",
        [0x0201] = "SERVER_CONNECTRESPONSE", [0x0202] = "SERVER_LOGINRESPONSE", [0x0203] = "SERVER_CHARACTERINFO",
        [0x0204] = "SERVER_CREATESHIP", [0x0205] = "SERVER_DAMAGEOBJECT", [0x0206] = "SERVER_DESTROYOBJECT",
        [0x0207] = "SERVER_LAUNCH", [0x0208] = "SERVER_CHARSELECTVERIFIED", [0x020a] = "SERVER_ACTIVATEOBJECT",
        [0x020b] = "SERVER_LAND", [0x020d] = "SERVER_SETSTARTROOM", [0x020e] = "SERVER_GFCOMPLETENEWSBROADCASTLIST",
        [0x020f] = "SERVER_GFCOMPLETECHARLIST", [0x0210] = "SERVER_GFCOMPLETEMISSIONCOMPUTERLIST",
        [0x0211] = "SERVER_GFCOMPLETESCRIPTBEHAVIORLIST", [0x0213] = "SERVER_GFCOMPLETEAMBIENTSCRIPTLIST",
        [0x0214] = "SERVER_GFDESTROYNEWSBROADCAST", [0x021e] = "SERVER_GFUPDATENEWSBROADCAST",
        [0x0221] = "SERVER_SYSTEM_SWITCH_OUT", [0x0222] = "SERVER_SYSTEM_SWITCH_IN", [0x0223] = "SERVER_SETSHIPARCH",
        [0x0224] = "SERVER_SETEQUIPMENT", [0x0225] = "SERVER_SETCARGO", [0x0226] = "SERVER_GFUPDATECHAR",
        [0x0227] = "SERVER_REQUESTCREATESHIPRESP", [0x0228] = "SERVER_CREATELOOT", [0x0229] = "SERVER_SETREPUTATION",
        [0x022a] = "SERVER_ADJUSTATTITUDE", [0x022b] = "SERVER_SETGROUPFEELINGS", [0x022c] = "SERVER_CREATEMINE",
        [0x022d] = "SERVER_CREATECOUNTER", [0x022e] = "SERVER_SETADDITEM", [0x022f] = "SERVER_SETREMOVEITEM",
        [0x0230] = "SERVER_SETCASH", [0x0237] = "SERVER_CREATEGUIDED", [0x0242] = "SERVER_CREATESOLAR",
        [0x0248] = "SERVER_SETCOLLISIONGROUPS", [0x0249] = "SERVER_SETHULLSTATUS", [0x024f] = "SERVER_SENDCOMM",
        [0x0252] = "SERVER_PLAYERLIST", [0x0253] = "SERVER_FORMATION_UPDATE", [0x0254] = "SERVER_MISCOBJUPDATE",
        [0x0255] = "SERVER_OBJECTCARGOUPDATE", [0x0256] = "SERVER_SENDNNMESSAGE", [0x0257] = "SERVER_SET_MUSIC",
    };

    /// <summary>Returns the packet name, or <c>chN/0xNN</c> if it is unknown.</summary>
    /// <param name="channel">Channel.</param>
    /// <param name="opcode">Opcode.</param>
    /// <returns>Name for output.</returns>
    public static string Name(int channel, int opcode) =>
        Names.TryGetValue((channel << 8) | opcode, out var n) ? n : $"ch{channel}/0x{opcode:x2}";
}
