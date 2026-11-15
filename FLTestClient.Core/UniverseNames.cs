namespace FLTestClient;

/// <summary>
/// Resolves FL hashes of bases and systems to nicknames, read from <c>DATA\UNIVERSE\universe.ini</c>, and
/// knows where the jump gates and jump holes of every system lead (<c>goto</c> in the system files).
/// </summary>
public sealed class UniverseNames
{
    private readonly Dictionary<uint, string> names = new();
    private readonly Dictionary<uint, uint> systemOfBase = new();
    private readonly Dictionary<uint, uint> jumpDestination = new();
    private readonly List<string> baseNicknames = new();

    /// <summary>All base nicknames in file order.</summary>
    public IReadOnlyList<string> Bases => baseNicknames;

    /// <summary>Reads <c>universe.ini</c> and the system files it names from a DATA folder.</summary>
    /// <param name="dataDirectory">Path of the DATA folder, e.g. <c>C:\Freelancer\EXE_SERVER\DATA</c>.</param>
    /// <returns>The loaded names.</returns>
    /// <exception cref="FileNotFoundException">If <c>UNIVERSE\universe.ini</c> is missing.</exception>
    public static UniverseNames Load(string dataDirectory)
    {
        string file = Path.Combine(dataDirectory, "UNIVERSE", "universe.ini");
        if (!File.Exists(file))
            throw new FileNotFoundException(Texts.Get("Error.UniverseMissing"), file);

        var result = new UniverseNames();
        var systemFiles = new List<string>();
        string section = "";
        string? nickname = null;
        string? system = null;
        string? systemFile = null;

        void Flush()
        {
            if (nickname == null)
                return;
            uint id = FLHash.CreateID(nickname);
            result.names[id] = nickname;
            if (section == "base")
            {
                result.baseNicknames.Add(nickname);
                if (system != null)
                    result.systemOfBase[id] = FLHash.CreateID(system);
            }
            else if (systemFile != null)
            {
                // [system] file = Systems\Li01\Li01.ini, relative to the UNIVERSE folder.
                systemFiles.Add(Path.Combine(dataDirectory, "UNIVERSE", systemFile));
            }
        }

        foreach (string raw in File.ReadLines(file))
        {
            string line = raw.Split(';')[0].Trim();
            if (line.StartsWith('['))
            {
                Flush();
                section = line.Trim('[', ']').ToLowerInvariant();
                nickname = null;
                system = null;
                systemFile = null;
                continue;
            }
            int eq = line.IndexOf('=');
            if (eq < 0 || (section != "base" && section != "system"))
                continue;
            string key = line[..eq].Trim().ToLowerInvariant();
            string value = line[(eq + 1)..].Trim();
            if (key == "nickname")
                nickname = value;
            else if (key == "system")
                system = value;
            else if (key == "file")
                systemFile = value;
        }
        Flush();

        foreach (string systemPath in systemFiles)
            result.LoadJumpObjects(systemPath);
        return result;
    }

    // [Object] blocks with "goto = system, arrival object, tunnel" are jump gates and jump holes; their object
    // ID in space is the hash of the nickname. A missing or unreadable system file is skipped.
    private void LoadJumpObjects(string file)
    {
        if (!File.Exists(file))
            return;
        bool inObject = false;
        string? nickname = null;
        string? destination = null;

        void Flush()
        {
            if (inObject && nickname != null && destination != null)
            {
                uint id = FLHash.CreateID(nickname);
                names[id] = nickname;
                jumpDestination[id] = FLHash.CreateID(destination);
            }
            nickname = null;
            destination = null;
        }

        try
        {
            foreach (string raw in File.ReadLines(file))
            {
                string line = raw.Split(';')[0].Trim();
                if (line.StartsWith('['))
                {
                    Flush();
                    inObject = line.Trim('[', ']').Equals("object", StringComparison.OrdinalIgnoreCase);
                    continue;
                }
                int eq = line.IndexOf('=');
                if (!inObject || eq < 0)
                    continue;
                string key = line[..eq].Trim().ToLowerInvariant();
                string value = line[(eq + 1)..].Trim();
                if (key == "nickname")
                    nickname = value;
                else if (key == "goto")
                    destination = value.Split(',')[0].Trim();
            }
            Flush();
        }
        catch (IOException)
        {
            // The names of bases and systems are still useful without the jump objects of this system.
        }
    }

    /// <summary>Returns the nickname for a hash.</summary>
    /// <param name="id">Hash of a base, a system or a jump object.</param>
    /// <returns>Nickname, or null if unknown or 0.</returns>
    public string? Name(uint id) => id != 0 && names.TryGetValue(id, out var n) ? n : null;

    /// <summary>Returns the system of a base.</summary>
    /// <param name="baseId">Hash of the base.</param>
    /// <returns>Hash of the system or 0.</returns>
    public uint SystemOfBase(uint baseId) => systemOfBase.TryGetValue(baseId, out var s) ? s : 0;

    /// <summary>Returns the system a jump gate or jump hole leads to (first value of its <c>goto</c>).</summary>
    /// <param name="objectId">Object ID of the jump gate or jump hole (hash of its nickname).</param>
    /// <returns>Hash of the destination system, or 0 if the object is unknown.</returns>
    public uint JumpDestination(uint objectId) => jumpDestination.TryGetValue(objectId, out var s) ? s : 0;
}
