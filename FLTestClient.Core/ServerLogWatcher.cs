using System.Text;

namespace FLTestClient;

/// <summary>Kind of a line in the FLHook log, used for highlighting.</summary>
public enum LogLineKind
{
    /// <summary>Any other line.</summary>
    Normal,
    /// <summary>"Time lag detected" or "Time delta invalid" (frame longer than 1 s).</summary>
    TimeLag,
    /// <summary>Kick or disconnect.</summary>
    Kick,
    /// <summary>"Possible cheating detected" (dsacesrv).</summary>
    Cheat,
    /// <summary>Error, exception or crash suppression.</summary>
    Error,
}

/// <summary>
/// Follows a growing log file (e.g. <c>flhook_logs\FLHook.log</c>) like <c>tail -f</c>.
/// </summary>
/// <remarks>Only lines appended after the start are reported. FLHook writes Windows-1252.</remarks>
public sealed class ServerLogWatcher : IDisposable
{
    private readonly string path;
    private readonly Timer timer;
    private long offset;
    private string partial = "";

    /// <summary>A new complete line. Raised on a thread pool thread.</summary>
    public event Action<string, LogLineKind>? LineAdded;

    /// <summary>Starts following the file from its current end.</summary>
    /// <param name="path">Path of the log file.</param>
    /// <param name="interval">Polling interval.</param>
    public ServerLogWatcher(string path, TimeSpan interval)
    {
        this.path = path;
        offset = File.Exists(path) ? new FileInfo(path).Length : 0;
        timer = new Timer(_ => Poll(), null, interval, interval);
    }

    /// <summary>Classifies a log line.</summary>
    /// <param name="line">Line from the FLHook log.</param>
    /// <returns>Kind of the line.</returns>
    public static LogLineKind Classify(string line)
    {
        if (line.Contains("Time lag", StringComparison.OrdinalIgnoreCase) ||
            line.Contains("Time delta invalid", StringComparison.OrdinalIgnoreCase))
            return LogLineKind.TimeLag;
        if (line.Contains("cheating", StringComparison.OrdinalIgnoreCase))
            return LogLineKind.Cheat;
        if (line.Contains("ERROR", StringComparison.Ordinal) || line.Contains("Exception", StringComparison.OrdinalIgnoreCase) ||
            line.Contains("Crash", StringComparison.OrdinalIgnoreCase))
            return LogLineKind.Error;
        if (line.Contains("Kick", StringComparison.OrdinalIgnoreCase) || line.Contains("DisConnect", StringComparison.Ordinal))
            return LogLineKind.Kick;
        return LogLineKind.Normal;
    }

    private void Poll()
    {
        // Timer callbacks can overlap if one run takes longer than the interval.
        if (!Monitor.TryEnter(timer))
            return;
        try
        {
            ReadNewLines();
        }
        finally
        {
            Monitor.Exit(timer);
        }
    }

    private void ReadNewLines()
    {
        try
        {
            if (!File.Exists(path))
                return;
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            if (fs.Length < offset)
                offset = 0; // file was recreated (server restart)
            if (fs.Length == offset)
                return;
            fs.Seek(offset, SeekOrigin.Begin);
            var bytes = new byte[fs.Length - offset];
            int read = fs.Read(bytes, 0, bytes.Length);
            offset += read;
            string text = partial + Encoding.Latin1.GetString(bytes, 0, read);
            int end = text.LastIndexOf('\n');
            if (end < 0)
            {
                partial = text;
                return;
            }
            partial = text[(end + 1)..];
            foreach (string line in text[..end].Split('\n'))
            {
                string l = line.TrimEnd('\r');
                if (l.Length > 0)
                    LineAdded?.Invoke(l, Classify(l));
            }
        }
        catch (IOException)
        {
            // File briefly locked; try again on the next interval.
        }
    }

    /// <summary>Stops following the file.</summary>
    public void Dispose() => timer.Dispose();
}
