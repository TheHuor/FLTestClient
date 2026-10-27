namespace FLTestClient;

/// <summary>
/// Resolves FL hashes of bases and systems to nicknames, read from <c>DATA\UNIVERSE\universe.ini</c>.
/// </summary>
public sealed class UniverseNames
{
    private readonly Dictionary<uint, string> names = new();
    private readonly Dictionary<uint, uint> systemOfBase = new();
    private readonly List<string> baseNicknames = new();

    /// <summary>All base nicknames in file order.</summary>
    public IReadOnlyList<string> Bases => baseNicknames;

    /// <summary>Reads <c>universe.ini</c> from a DATA folder.</summary>
    /// <param name="dataDirectory">Path of the DATA folder, e.g. <c>C:\Freelancer\EXE_SERVER\DATA</c>.</param>
    /// <returns>The loaded names.</returns>
    /// <exception cref="FileNotFoundException">If <c>UNIVERSE\universe.ini</c> is missing.</exception>
    public static UniverseNames Load(string dataDirectory)
    {
        string file = Path.Combine(dataDirectory, "UNIVERSE", "universe.ini");
        if (!File.Exists(file))
            throw new FileNotFoundException(Texts.Get("Error.UniverseMissing"), file);

        var result = new UniverseNames();
        string section = "";
        string? nickname = null;
        string? system = null;

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
        }
        Flush();
        return result;
    }

    /// <summary>Returns the nickname for a hash.</summary>
    /// <param name="id">Hash of a base or system.</param>
    /// <returns>Nickname, or null if unknown or 0.</returns>
    public string? Name(uint id) => id != 0 && names.TryGetValue(id, out var n) ? n : null;

    /// <summary>Returns the system of a base.</summary>
    /// <param name="baseId">Hash of the base.</param>
    /// <returns>Hash of the system or 0.</returns>
    public uint SystemOfBase(uint baseId) => systemOfBase.TryGetValue(baseId, out var s) ? s : 0;
}
