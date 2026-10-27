using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace FLTestClient;

/// <summary>
/// Minimal access to DirectPlay8 (<c>dpnet.dll</c>) through raw COM vtables, the same way DALib.dll
/// (<c>CDPClient</c>) does it in the original client.
/// </summary>
/// <remarks>
/// Slot numbers follow <c>dplay8.h</c> and were checked against a disassembly of Freelancer
/// (<c>ReturnBuffer</c> = +0x3c = slot 15). All structures use the 32-bit layout.
/// </remarks>
internal static unsafe class DP8
{
    /// <summary>CLSID of <c>DirectPlay8Client</c>.</summary>
    public static readonly Guid CLSID_DirectPlay8Client = new("743f1dc6-5aba-429f-8bdf-c54d03253dc2");

    /// <summary>IID of <c>IDirectPlay8Client</c>.</summary>
    public static readonly Guid IID_IDirectPlay8Client = new("5102dacd-241b-11d3-aea7-006097b01411");

    /// <summary>CLSID of <c>DirectPlay8Address</c>.</summary>
    public static readonly Guid CLSID_DirectPlay8Address = new("934a9523-a3ca-4bc5-ada0-d6d95d979421");

    /// <summary>IID of <c>IDirectPlay8Address</c>.</summary>
    public static readonly Guid IID_IDirectPlay8Address = new("83783300-4063-4c8a-9db3-82830a7feb31");

    /// <summary>CLSID of the TCP/IP service provider.</summary>
    public static readonly Guid CLSID_DP8SP_TCPIP = new("ebfe7ba0-628d-11d2-ae0f-006097b01411");

    /// <summary>Application GUID FLServer hosts with (RemoteServer.dll, <c>[Freelancer] AppGUID</c>).</summary>
    public static readonly Guid FreelancerApp = new("a690f026-26f0-4e57-aca0-ecf868e48d21");

    /// <summary>DPN_MSGID_CONNECT_COMPLETE.</summary>
    public const uint MSGID_CONNECT_COMPLETE = 0xFFFF0005;

    /// <summary>DPN_MSGID_RECEIVE.</summary>
    public const uint MSGID_RECEIVE = 0xFFFF0011;

    /// <summary>DPN_MSGID_TERMINATE_SESSION.</summary>
    public const uint MSGID_TERMINATE_SESSION = 0xFFFF0016;

    /// <summary>DPNSUCCESS_PENDING: the asynchronous operation was started.</summary>
    public const int DPNSUCCESS_PENDING = unchecked((int)0x0015800E);

    /// <summary>DPNERR_INVALIDPASSWORD: session password missing or wrong.</summary>
    public const int DPNERR_INVALIDPASSWORD = unchecked((int)0x80158410);

    /// <summary>Send flags as used by <c>CDPClient::Send</c>: DPNSEND_GUARANTEED | DPNSEND_COALESCE.</summary>
    public const uint SendFlags = 0x108;

    /// <summary>Initialises COM for the calling thread.</summary>
    /// <param name="reserved">Must be <see cref="IntPtr.Zero"/>.</param>
    /// <param name="coInit">COINIT flags, 0 = multithreaded apartment.</param>
    /// <returns>HRESULT.</returns>
    [DllImport("ole32.dll")]
    public static extern int CoInitializeEx(IntPtr reserved, uint coInit);

    /// <summary>Creates a COM object.</summary>
    /// <param name="clsid">Class ID.</param>
    /// <param name="outer">Aggregation, always <see cref="IntPtr.Zero"/> here.</param>
    /// <param name="context">CLSCTX flags.</param>
    /// <param name="iid">Requested interface.</param>
    /// <param name="instance">Created interface pointer.</param>
    /// <returns>HRESULT.</returns>
    [DllImport("ole32.dll")]
    public static extern int CoCreateInstance(in Guid clsid, IntPtr outer, uint context, in Guid iid, out IntPtr instance);

    /// <summary>Reads an entry from the vtable of a COM object.</summary>
    /// <param name="obj">Interface pointer.</param>
    /// <param name="index">Slot number (0 = QueryInterface).</param>
    /// <returns>Function pointer of the slot.</returns>
    public static void* Slot(IntPtr obj, int index) => (*(void***)obj)[index];

    /// <summary>Calls <c>IUnknown::Release</c> if the pointer is set.</summary>
    /// <param name="obj">Interface pointer or <see cref="IntPtr.Zero"/>.</param>
    public static void Release(IntPtr obj)
    {
        if (obj != IntPtr.Zero)
            ((delegate* unmanaged[Stdcall]<IntPtr, uint>)Slot(obj, 2))(obj);
    }

    /// <summary>Throws if an HRESULT reports an error.</summary>
    /// <param name="hr">Return value of the COM call.</param>
    /// <param name="what">Description for the error message.</param>
    /// <exception cref="InvalidOperationException">For a negative HRESULT.</exception>
    public static void Check(int hr, string what)
    {
        if (hr < 0)
            throw new InvalidOperationException(Texts.Get("Error.HResult", what, hr));
    }

    /// <summary>Creates a DirectPlay8 address with the TCP/IP provider.</summary>
    /// <returns>Interface pointer; the caller must free it with <see cref="Release"/>.</returns>
    public static IntPtr CreateAddress()
    {
        Check(CoCreateInstance(CLSID_DirectPlay8Address, IntPtr.Zero, 1, IID_IDirectPlay8Address, out var addr),
              "CoCreateInstance(DirectPlay8Address)");
        SetTcpIpProvider(addr);
        return addr;
    }

    /// <summary>Fills an address from a DirectPlay URL and sets the TCP/IP provider again afterwards.</summary>
    /// <param name="addr">Address from <see cref="CreateAddress"/>.</param>
    /// <param name="url">For example <c>x-directplay:/hostname=1.2.3.4;port=2302</c>.</param>
    public static void BuildFromUrl(IntPtr addr, string url)
    {
        fixed (char* p = url)
            Check(((delegate* unmanaged[Stdcall]<IntPtr, char*, int>)Slot(addr, 3))(addr, p), "BuildFromURLW");
        // BuildFromURLW replaces the provider; set it again like CDPClient::ConnectToServer does.
        SetTcpIpProvider(addr);
    }

    private static void SetTcpIpProvider(IntPtr addr)
    {
        Guid sp = CLSID_DP8SP_TCPIP;
        Check(((delegate* unmanaged[Stdcall]<IntPtr, Guid*, int>)Slot(addr, 13))(addr, &sp), "SetSP");
    }
}

/// <summary>DPN_APPLICATION_DESC (x86 layout, 0x48 bytes).</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct DPN_APPLICATION_DESC
{
    /// <summary>Size of the structure.</summary>
    public uint dwSize;
    /// <summary>DPNSESSION flags.</summary>
    public uint dwFlags;
    /// <summary>Instance GUID of the session.</summary>
    public Guid guidInstance;
    /// <summary>Application GUID.</summary>
    public Guid guidApplication;
    /// <summary>Maximum number of players.</summary>
    public uint dwMaxPlayers;
    /// <summary>Current number of players.</summary>
    public uint dwCurrentPlayers;
    /// <summary>Session name (UTF-16).</summary>
    public IntPtr pwszSessionName;
    /// <summary>Session password (UTF-16) or null.</summary>
    public IntPtr pwszPassword;
    /// <summary>Reserved.</summary>
    public IntPtr pvReservedData;
    /// <summary>Reserved.</summary>
    public uint dwReservedDataSize;
    /// <summary>Application data.</summary>
    public IntPtr pvApplicationReservedData;
    /// <summary>Size of the application data.</summary>
    public uint dwApplicationReservedDataSize;
}

/// <summary>DPN_BUFFER_DESC.</summary>
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct DPN_BUFFER_DESC
{
    /// <summary>Size of the buffer.</summary>
    public uint dwBufferSize;
    /// <summary>Pointer to the data.</summary>
    public byte* pBufferData;
}

/// <summary>DPNMSG_RECEIVE.</summary>
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct DPNMSG_RECEIVE
{
    /// <summary>Size of the structure.</summary>
    public uint dwSize;
    /// <summary>Sender.</summary>
    public uint dpnidSender;
    /// <summary>Context of the sender.</summary>
    public IntPtr pvPlayerContext;
    /// <summary>Received data.</summary>
    public byte* pReceiveData;
    /// <summary>Length of the received data.</summary>
    public uint dwReceiveDataSize;
    /// <summary>Buffer handle for ReturnBuffer.</summary>
    public IntPtr hBufferHandle;
}

/// <summary>DPNMSG_CONNECT_COMPLETE (only the fields used here).</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct DPNMSG_CONNECT_COMPLETE
{
    /// <summary>Size of the structure.</summary>
    public uint dwSize;
    /// <summary>Handle of the connect operation.</summary>
    public IntPtr hAsyncOp;
    /// <summary>User context.</summary>
    public IntPtr pvUserContext;
    /// <summary>Result of the connection attempt.</summary>
    public int hResultCode;
}

/// <summary>DPNMSG_TERMINATE_SESSION (only the fields used here).</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct DPNMSG_TERMINATE_SESSION
{
    /// <summary>Size of the structure.</summary>
    public uint dwSize;
    /// <summary>Reason the session ended.</summary>
    public int hResultCode;
}

/// <summary>Kind of a network event from <see cref="DPlayConnection"/>.</summary>
internal enum NetEventKind
{
    /// <summary>Connection established.</summary>
    Connected,
    /// <summary>Connection attempt failed, see <see cref="NetEvent.Result"/>.</summary>
    ConnectFailed,
    /// <summary>Game data received.</summary>
    Data,
    /// <summary>1-byte ping reply from the server received.</summary>
    Ping,
    /// <summary>Connection ended.</summary>
    Disconnected,
}

/// <summary>An event from the DirectPlay callback, processed on the game tick.</summary>
/// <param name="Kind">Kind of event.</param>
/// <param name="Data">Payload for <see cref="NetEventKind.Data"/> and <see cref="NetEventKind.Ping"/>.</param>
/// <param name="Result">HRESULT for connection errors or the end of the session.</param>
internal readonly record struct NetEvent(NetEventKind Kind, byte[]? Data = null, int Result = 0);

/// <summary>
/// A DirectPlay8 client connection to an FLServer.
/// </summary>
/// <remarks>
/// Callbacks run on DirectPlay threads and only fill <see cref="Events"/>; the game logic reads them on its
/// own tick, like <c>CDPClient::DispatchMsgs</c>. Create and use the object on an MTA thread.
/// </remarks>
internal sealed unsafe class DPlayConnection : IDisposable
{
    private IntPtr client;
    private GCHandle self;

    /// <summary>Queue of received events.</summary>
    public readonly ConcurrentQueue<NetEvent> Events = new();

    /// <summary>True while the connection is up.</summary>
    public bool Connected { get; private set; }

    /// <summary>Creates and initialises an <c>IDirectPlay8Client</c> object.</summary>
    /// <exception cref="InvalidOperationException">If DirectPlay is not available.</exception>
    public DPlayConnection()
    {
        DP8.Check(DP8.CoCreateInstance(DP8.CLSID_DirectPlay8Client, IntPtr.Zero, 1, DP8.IID_IDirectPlay8Client, out client),
                  Texts.Get("Error.NoDirectPlay"));
        self = GCHandle.Alloc(this);
        delegate* unmanaged[Stdcall]<IntPtr, uint, IntPtr, int> handler = &HandleMessage;
        DP8.Check(((delegate* unmanaged[Stdcall]<IntPtr, IntPtr, IntPtr, uint, int>)DP8.Slot(client, 3))(
            client, GCHandle.ToIntPtr(self), (IntPtr)handler, 0), "IDirectPlay8Client::Initialize");
    }

    /// <summary>Starts the asynchronous connection; the result arrives as an event.</summary>
    /// <param name="host">IP address or host name of the server.</param>
    /// <param name="port">UDP port, normally 2302.</param>
    /// <param name="password">Session password or empty.</param>
    public void Connect(string host, int port, string password)
    {
        IntPtr hostAddr = DP8.CreateAddress();
        IntPtr deviceAddr = DP8.CreateAddress();
        IntPtr passwordPtr = password.Length > 0 ? Marshal.StringToHGlobalUni(password) : IntPtr.Zero;
        try
        {
            DP8.BuildFromUrl(hostAddr, $"x-directplay:/hostname={host};port={port}");
            var desc = new DPN_APPLICATION_DESC
            {
                dwSize = (uint)sizeof(DPN_APPLICATION_DESC),
                // DPNSESSION_REQUIREPASSWORD if the server has a session password (CDPServer::SetPassword).
                dwFlags = password.Length > 0 ? 0x80u : 0u,
                guidApplication = DP8.FreelancerApp,
                pwszPassword = passwordPtr,
            };
            IntPtr asyncHandle;
            int hr = ((delegate* unmanaged[Stdcall]<IntPtr, DPN_APPLICATION_DESC*, IntPtr, IntPtr, IntPtr, IntPtr, IntPtr, uint, IntPtr, IntPtr*, uint, int>)
                DP8.Slot(client, 7))(client, &desc, hostAddr, deviceAddr, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 0,
                                     IntPtr.Zero, &asyncHandle, 0);
            if (hr != 0 && hr != DP8.DPNSUCCESS_PENDING)
                DP8.Check(hr, "IDirectPlay8Client::Connect");
        }
        finally
        {
            DP8.Release(hostAddr);
            DP8.Release(deviceAddr);
            if (passwordPtr != IntPtr.Zero)
                Marshal.FreeHGlobal(passwordPtr);
        }
    }

    /// <summary>Sends a message to the server, guaranteed delivery.</summary>
    /// <param name="data">Raw data, one or more FL packets.</param>
    /// <returns>False if there is no connection or DirectPlay refuses.</returns>
    public bool Send(ReadOnlySpan<byte> data)
    {
        if (!Connected || client == IntPtr.Zero)
            return false;
        fixed (byte* p = data)
        {
            var buffer = new DPN_BUFFER_DESC { dwBufferSize = (uint)data.Length, pBufferData = p };
            IntPtr asyncHandle;
            int hr = ((delegate* unmanaged[Stdcall]<IntPtr, DPN_BUFFER_DESC*, uint, uint, IntPtr, IntPtr*, uint, int>)
                DP8.Slot(client, 8))(client, &buffer, 1, 0, IntPtr.Zero, &asyncHandle, DP8.SendFlags);
            return hr == 0 || hr == DP8.DPNSUCCESS_PENDING;
        }
    }

    /// <summary>Cancels pending operations and closes the connection.</summary>
    public void Close()
    {
        if (client == IntPtr.Zero)
            return;
        ((delegate* unmanaged[Stdcall]<IntPtr, IntPtr, uint, int>)DP8.Slot(client, 6))(client, IntPtr.Zero, 0x8000);
        ((delegate* unmanaged[Stdcall]<IntPtr, uint, int>)DP8.Slot(client, 14))(client, 0);
        Connected = false;
    }

    /// <summary>Closes the connection and releases the COM object.</summary>
    public void Dispose()
    {
        Close();
        DP8.Release(client);
        client = IntPtr.Zero;
        if (self.IsAllocated)
            self.Free();
    }

    // PFNDPNMESSAGEHANDLER. Must never throw an exception into dpnet.dll.
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
    private static int HandleMessage(IntPtr context, uint messageId, IntPtr message)
    {
        if (context == IntPtr.Zero || GCHandle.FromIntPtr(context).Target is not DPlayConnection conn)
            return 0;
        try
        {
            switch (messageId)
            {
                case DP8.MSGID_CONNECT_COMPLETE:
                {
                    var msg = (DPNMSG_CONNECT_COMPLETE*)message;
                    if (msg->hResultCode == 0)
                    {
                        conn.Connected = true;
                        conn.Events.Enqueue(new NetEvent(NetEventKind.Connected));
                    }
                    else
                    {
                        conn.Events.Enqueue(new NetEvent(NetEventKind.ConnectFailed, Result: msg->hResultCode));
                    }
                    break;
                }
                case DP8.MSGID_RECEIVE:
                {
                    var msg = (DPNMSG_RECEIVE*)message;
                    var data = new ReadOnlySpan<byte>(msg->pReceiveData, (int)msg->dwReceiveDataSize).ToArray();
                    // A single byte is the ping reply (link quality), see CDPClient::HandleClientMsg.
                    conn.Events.Enqueue(new NetEvent(data.Length > 1 ? NetEventKind.Data : NetEventKind.Ping, data));
                    break;
                }
                case DP8.MSGID_TERMINATE_SESSION:
                {
                    var msg = (DPNMSG_TERMINATE_SESSION*)message;
                    conn.Connected = false;
                    conn.Events.Enqueue(new NetEvent(NetEventKind.Disconnected, Result: msg->hResultCode));
                    break;
                }
            }
        }
        catch
        {
            // Never let an exception escape into dpnet.dll.
        }
        return 0;
    }
}
