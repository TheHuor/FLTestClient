using FLTestClient.Gui.ViewModels;
using System.Text.RegularExpressions;

namespace FLTestClient.Gui;

/// <summary>A single relocation shown in the preview and sent to FLHook.</summary>
/// <param name="Index">Bot number.</param>
/// <param name="Name">Character name.</param>
/// <param name="Base">Target base nickname.</param>
public sealed record FleetAssignment(int Index, string Name, string Base)
{
    /// <summary>Validated FLHook command.</summary>
    public string Command => $"beam {Name} {Base}";
}

/// <summary>Immutable, validated relocation plan shared by preview and execution.</summary>
public sealed class FleetActionPlan
{
    private FleetActionPlan(FleetAssignment[] assignments, int skipped)
    {
        Assignments = Array.AsReadOnly(assignments);
        Skipped = skipped;
    }

    /// <summary>Eligible bots and target bases in execution order.</summary>
    public IReadOnlyList<FleetAssignment> Assignments { get; }
    /// <summary>Number of candidates currently outside space.</summary>
    public int Skipped { get; }

    /// <summary>Validates target nicknames and assigns in-space bots round-robin.</summary>
    /// <param name="bots">Explicit action scope.</param>
    /// <param name="targets">One base nickname per line.</param>
    /// <returns>Plan; only bots in space receive an assignment.</returns>
    /// <exception cref="ArgumentException">Missing or invalid base/name.</exception>
    public static FleetActionPlan Create(IEnumerable<BotRowViewModel> bots, string targets)
    {
        var bases = targets.Split('\n').Select(b => b.Trim()).Where(b => b.Length > 0).ToArray();
        if (bases.Length == 0 || bases.Any(b => !Regex.IsMatch(b, @"\A[A-Za-z0-9_\-]+\z")))
            throw new ArgumentException("Action.InvalidBases");
        var candidates = bots.OrderBy(b => b.Index).ToArray();
        var eligible = candidates.Where(b => b.State == BotState.InSpace).ToArray();
        if (eligible.Any(b => b.Name.Length == 0 || b.Name.Any(char.IsWhiteSpace)))
            throw new ArgumentException("Action.InvalidNames");
        return new FleetActionPlan(eligible.Select((b, i) => new FleetAssignment(b.Index, b.Name, bases[i % bases.Length])).ToArray(),
            candidates.Length - eligible.Length);
    }
}
