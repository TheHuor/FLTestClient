using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

namespace FLTestClient;

/// <summary>
/// Looks for the UDP port an FLServer really listens on. DirectPlay moves a server to the next free port
/// when 2302 is taken at start (e.g. by a running Freelancer client), and a connection to the configured
/// port then fails without a useful error.
/// </summary>
/// <remarks>
/// The search sends a DirectPlay 8 enumeration query (<c>00 02</c>, payload, type 1, application GUID) to
/// every port of the range; only Freelancer servers answer (<c>00 03</c>, same payload). Nothing logs in.
/// </remarks>
public static class ServerPortFinder
{
    /// <summary>First port of the default search range, the DirectPlay default.</summary>
    public const int FirstPort = 2302;

    /// <summary>Last port of the default search range.</summary>
    public const int LastPort = 2311;

    private const int DefaultTimeoutMs = 700;
    private const int CacheSeconds = 10;
    private const ushort Payload = 0x4C46;

    private static readonly ConcurrentDictionary<string, (long Started, Task<IReadOnlyList<int>> Search)> Recent = new();

    /// <summary>
    /// Starts a search in the default range on a pool thread. Bots that fail together share one search:
    /// a result for the same host is reused for a few seconds.
    /// </summary>
    /// <param name="host">IP address or host name of the server.</param>
    /// <returns>Task with the answering ports in ascending order; empty if none answers.</returns>
    public static Task<IReadOnlyList<int>> FindAsync(string host)
    {
        long now = Stopwatch.GetTimestamp();
        var entry = Recent.AddOrUpdate(
            host,
            _ => (now, Task.Run(() => Find(host, FirstPort, LastPort, DefaultTimeoutMs))),
            (_, known) => Stopwatch.GetElapsedTime(known.Started, now).TotalSeconds < CacheSeconds
                ? known
                : (now, Task.Run(() => Find(host, FirstPort, LastPort, DefaultTimeoutMs))));
        return entry.Search;
    }

    /// <summary>Queries every port of a range and waits for the answers.</summary>
    /// <param name="host">IP address or host name of the server.</param>
    /// <param name="firstPort">First port to query.</param>
    /// <param name="lastPort">Last port to query.</param>
    /// <param name="timeoutMs">Time to wait for answers in milliseconds.</param>
    /// <returns>The answering ports in ascending order; empty if none answers or the host is unknown.</returns>
    public static IReadOnlyList<int> Find(string host, int firstPort, int lastPort, int timeoutMs)
    {
        var found = new SortedSet<int>();
        try
        {
            IPAddress? address = Resolve(host);
            if (address == null)
                return Array.Empty<int>();

            using var socket = new UdpClient(AddressFamily.InterNetwork);
            byte[] query = BuildQuery();
            for (int port = firstPort; port <= lastPort; port++)
                socket.Send(query, query.Length, new IPEndPoint(address, port));

            var watch = Stopwatch.StartNew();
            while (found.Count <= lastPort - firstPort)
            {
                int left = timeoutMs - (int)watch.ElapsedMilliseconds;
                if (left <= 0)
                    break;
                socket.Client.ReceiveTimeout = left;
                var sender = new IPEndPoint(IPAddress.Any, 0);
                byte[] reply;
                try
                {
                    reply = socket.Receive(ref sender);
                }
                catch (SocketException error) when (error.SocketErrorCode == SocketError.ConnectionReset)
                {
                    // ICMP "port unreachable" of a closed port; the other answers are still to come.
                    continue;
                }
                if (IsReply(reply) && sender.Port >= firstPort && sender.Port <= lastPort)
                    found.Add(sender.Port);
            }
        }
        catch (SocketException)
        {
            // Timeout or no route: report what answered so far.
        }
        return found.ToArray();
    }

    /// <summary>Builds the enumeration query for Freelancer servers.</summary>
    /// <returns>Datagram to send to the server port.</returns>
    internal static byte[] BuildQuery()
    {
        const byte withApplicationGuid = 1;
        var query = new byte[5 + 16];
        query[1] = 0x02;
        query[2] = unchecked((byte)Payload);
        query[3] = (byte)(Payload >> 8);
        query[4] = withApplicationGuid;
        DP8.FreelancerApp.TryWriteBytes(query.AsSpan(5));
        return query;
    }

    /// <summary>Tells whether a datagram is the answer to <see cref="BuildQuery"/>.</summary>
    /// <param name="reply">Received datagram.</param>
    /// <returns>True for an enumeration response that carries the payload of the query.</returns>
    internal static bool IsReply(byte[] reply) =>
        reply.Length >= 4 && reply[0] == 0x00 && reply[1] == 0x03
        && reply[2] == unchecked((byte)Payload) && reply[3] == (byte)(Payload >> 8);

    private static IPAddress? Resolve(string host)
    {
        if (IPAddress.TryParse(host, out var literal))
            return literal.AddressFamily == AddressFamily.InterNetwork ? literal : null;
        return Dns.GetHostAddresses(host).FirstOrDefault(candidate => candidate.AddressFamily == AddressFamily.InterNetwork);
    }
}
