using System.Net.Sockets;
using System.Text;

namespace FLTestClient;

/// <summary>
/// Simple client for the FLHook admin interface (<c>[Socket] Port</c> in flhook.ini, ASCII).
/// </summary>
/// <remarks>
/// FLHook ends every answer with a line <c>OK</c> or <c>ERR ...</c>. The client is not thread-safe;
/// use one instance per thread.
/// </remarks>
public sealed class FLHookAdminClient : IDisposable
{
    private readonly TcpClient tcp;
    private readonly NetworkStream stream;
    private readonly StringBuilder pending = new();

    /// <summary>Time to wait for a complete answer.</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>Connects to FLHook and logs in.</summary>
    /// <param name="host">Machine running FLServer, usually <c>127.0.0.1</c>.</param>
    /// <param name="port">Admin port, default 9000.</param>
    /// <param name="password">Password from <c>PassN</c> in flhook.ini.</param>
    /// <exception cref="InvalidOperationException">If FLHook rejects the password.</exception>
    public FLHookAdminClient(string host, int port, string password)
    {
        tcp = new TcpClient(host, port);
        stream = tcp.GetStream();
        ReadAvailable(TimeSpan.FromMilliseconds(500)); // discard the greeting
        pending.Clear();
        string answer = Execute($"pass {password}");
        if (!answer.TrimEnd().EndsWith("OK", StringComparison.Ordinal))
            throw new InvalidOperationException(Texts.Get("Error.AdminRejected", answer.Trim()));
    }

    /// <summary>Reads the first admin password (<c>Pass0</c>) from a flhook.ini.</summary>
    /// <param name="flhookIni">Path of the server's flhook.ini.</param>
    /// <returns>The password, or null if none is configured.</returns>
    public static string? ReadPasswordFromIni(string flhookIni)
    {
        foreach (string line in File.ReadLines(flhookIni, Encoding.Latin1))
        {
            string t = line.Trim();
            if (t.StartsWith("Pass0", StringComparison.OrdinalIgnoreCase) && t.Contains('='))
                return t[(t.IndexOf('=') + 1)..].Trim();
        }
        return null;
    }

    /// <summary>Runs an admin command and waits for <c>OK</c> or <c>ERR</c>.</summary>
    /// <param name="command">For example <c>getplayers</c>, <c>beam TestBot01 Li02_01_Base</c>, <c>rehash</c>.</param>
    /// <returns>The complete answer including the final line.</returns>
    public string Execute(string command)
    {
        byte[] bytes = Encoding.ASCII.GetBytes(command + "\r\n");
        stream.Write(bytes, 0, bytes.Length);
        var deadline = DateTime.UtcNow + Timeout;
        while (DateTime.UtcNow < deadline)
        {
            ReadAvailable(TimeSpan.FromMilliseconds(100));
            string text = pending.ToString();
            if (IsComplete(text))
            {
                pending.Clear();
                return text;
            }
        }
        string partial = pending.ToString();
        pending.Clear();
        return partial + Texts.Get("Error.AdminTimeout");
    }

    private static bool IsComplete(string text)
    {
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (lines.Length == 0 || !text.EndsWith('\n'))
            return false;
        string last = lines[^1];
        return last == "OK" || last.StartsWith("ERR", StringComparison.Ordinal);
    }

    private void ReadAvailable(TimeSpan wait)
    {
        var buffer = new byte[8192];
        var until = DateTime.UtcNow + wait;
        do
        {
            while (stream.DataAvailable)
            {
                int n = stream.Read(buffer, 0, buffer.Length);
                pending.Append(Encoding.ASCII.GetString(buffer, 0, n));
            }
            if (DateTime.UtcNow >= until)
                break;
            Thread.Sleep(20);
        }
        while (!stream.DataAvailable && DateTime.UtcNow < until);
    }

    /// <summary>Closes the connection.</summary>
    public void Dispose()
    {
        stream.Dispose();
        tcp.Dispose();
    }
}
