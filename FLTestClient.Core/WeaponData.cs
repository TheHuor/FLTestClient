using System.Globalization;

namespace FLTestClient;

/// <summary>A gun archetype (<c>[Gun]</c> in the equipment files) with the values a bot needs to fire it.</summary>
/// <param name="Nickname">Nickname of the gun.</param>
/// <param name="MunitionId">Nickname hash of its <c>projectile_archetype</c>.</param>
/// <param name="Munition">Nickname of its <c>projectile_archetype</c>.</param>
/// <param name="RefireDelay">Seconds between two shots (<c>refire_delay</c>).</param>
/// <param name="PowerUsage">Energy per shot (<c>power_usage</c>).</param>
/// <param name="MuzzleVelocity">Projectile speed in metres per second (<c>muzzle_velocity</c>).</param>
/// <param name="Range">Reach in metres: muzzle velocity times the munition's <c>lifetime</c>.</param>
public sealed record GunInfo(string Nickname, uint MunitionId, string Munition, double RefireDelay, double PowerUsage,
    double MuzzleVelocity, double Range);

/// <summary>A power plant (<c>[Power]</c>).</summary>
/// <param name="Capacity">Maximum energy (<c>capacity</c>).</param>
/// <param name="ChargeRate">Energy regained per second (<c>charge_rate</c>).</param>
public sealed record PowerInfo(double Capacity, double ChargeRate);

/// <summary>
/// Guns and power plants read from the server's <c>DATA\EQUIPMENT</c> files, so a bot can fire its own guns the
/// way a real client does (FIREWEAPON with the right refire delay and energy).
/// </summary>
/// <remarks>Guided munitions (with <c>motor</c> or <c>seeker</c>) are left out: their hits are reported by the
/// client of the ship that is hit, not by the shooter.</remarks>
public sealed class WeaponData
{
    private readonly Dictionary<uint, GunInfo> guns = new();
    private readonly Dictionary<uint, PowerInfo> powerPlants = new();

    /// <summary>Number of guns found.</summary>
    public int GunCount => guns.Count;

    /// <summary>Reads all <c>*.ini</c> files of <c>DATA\EQUIPMENT</c>.</summary>
    /// <param name="dataDirectory">Path of the DATA folder.</param>
    /// <returns>The loaded data; empty if the folder has no EQUIPMENT subfolder.</returns>
    public static WeaponData Load(string dataDirectory)
    {
        var result = new WeaponData();
        string folder = Path.Combine(dataDirectory, "EQUIPMENT");
        if (!Directory.Exists(folder))
            return result;

        var rawGuns = new List<Dictionary<string, string>>();
        var munitions = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        foreach (string file in Directory.EnumerateFiles(folder, "*.ini"))
        {
            foreach (var (section, values) in ReadSections(file))
            {
                if (!values.TryGetValue("nickname", out string? nickname))
                    continue;
                switch (section)
                {
                    case "gun":
                        rawGuns.Add(values);
                        break;
                    case "munition":
                        munitions[nickname] = values;
                        break;
                    case "power":
                        result.powerPlants[FLHash.CreateID(nickname)] =
                            new PowerInfo(Number(values, "capacity"), Number(values, "charge_rate"));
                        break;
                }
            }
        }

        foreach (var gun in rawGuns)
        {
            if (!gun.TryGetValue("projectile_archetype", out string? munitionName)
                || !munitions.TryGetValue(munitionName, out var munition)
                || munition.ContainsKey("motor") || munition.ContainsKey("seeker"))
                continue;
            double velocity = Number(gun, "muzzle_velocity");
            string nickname = gun["nickname"];
            result.guns[FLHash.CreateID(nickname)] = new GunInfo(nickname, FLHash.CreateID(munitionName), munitionName,
                Number(gun, "refire_delay"), Number(gun, "power_usage"), velocity, velocity * Number(munition, "lifetime"));
        }
        return result;
    }

    /// <summary>Looks up a gun archetype.</summary>
    /// <param name="archetypeId">Nickname hash of the gun.</param>
    /// <returns>The gun or null if it is unknown or fires guided munitions.</returns>
    public GunInfo? Gun(uint archetypeId) => guns.TryGetValue(archetypeId, out var gun) ? gun : null;

    /// <summary>Looks up a power plant archetype.</summary>
    /// <param name="archetypeId">Nickname hash of the power plant.</param>
    /// <returns>The power plant or null.</returns>
    public PowerInfo? Power(uint archetypeId) => powerPlants.TryGetValue(archetypeId, out var power) ? power : null;

    private static double Number(Dictionary<string, string> values, string key) =>
        values.TryGetValue(key, out string? text)
        && double.TryParse(text.Split(',')[0].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double value)
            ? value
            : 0;

    // Sections of an INI file as (lower-case name, first value per lower-case key).
    private static IEnumerable<(string Section, Dictionary<string, string> Values)> ReadSections(string file)
    {
        string? section = null;
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (string raw in File.ReadLines(file))
        {
            string line = raw.Split(';')[0].Trim();
            if (line.StartsWith('['))
            {
                if (section != null)
                    yield return (section, values);
                section = line.Trim('[', ']').Trim().ToLowerInvariant();
                values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                continue;
            }
            int eq = line.IndexOf('=');
            if (section == null || eq < 0)
                continue;
            string key = line[..eq].Trim();
            if (!values.ContainsKey(key))
                values[key] = line[(eq + 1)..].Trim();
        }
        if (section != null)
            yield return (section, values);
    }
}
