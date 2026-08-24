namespace PrimeAxiom.Core.Substrate;

public readonly record struct Signal(BitState State, int Depth = 0);

public readonly record struct GateCost(long NandEvaluations, int CriticalPathDepth)
{
    public static GateCost Zero => new(0, 0);

    public static GateCost Sequential(GateCost first, GateCost second) =>
        new(first.NandEvaluations + second.NandEvaluations,
            first.CriticalPathDepth + second.CriticalPathDepth);

    public static GateCost Parallel(IEnumerable<GateCost> costs)
    {
        var materialized = costs.ToArray();
        return materialized.Length == 0
            ? Zero
            : new GateCost(
                materialized.Sum(cost => cost.NandEvaluations),
                materialized.Max(cost => cost.CriticalPathDepth));
    }
}

/// <summary>
/// Counts every evaluated NAND and propagates a unit-delay critical depth.
/// Wiring, fan-out, loading, hazards, and physical energy are intentionally not
/// modeled; this is a logical network model, not a transistor or relay model.
/// </summary>
public sealed class GateNetwork
{
    private long _nandEvaluations;
    private int _maximumDepth;

    public GateCost Cost => new(_nandEvaluations, _maximumDepth);

    public Signal Input(BitState value, int arrivalDepth = 0)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(arrivalDepth);
        _maximumDepth = Math.Max(_maximumDepth, arrivalDepth);
        return new Signal(value, arrivalDepth);
    }

    public Signal Nand(Signal left, Signal right)
    {
        var depth = Math.Max(left.Depth, right.Depth) + 1;
        _nandEvaluations++;
        _maximumDepth = Math.Max(_maximumDepth, depth);
        return new Signal(
            BitStateExtensions.FromBoolean(!(left.State.ToBoolean() && right.State.ToBoolean())),
            depth);
    }

    public Signal Not(Signal input) => Nand(input, input);

    public Signal And(Signal left, Signal right)
    {
        var notAnd = Nand(left, right);
        return Nand(notAnd, notAnd);
    }

    public Signal Or(Signal left, Signal right)
    {
        var notLeft = Nand(left, left);
        var notRight = Nand(right, right);
        return Nand(notLeft, notRight);
    }

    public Signal Xor(Signal left, Signal right)
    {
        var shared = Nand(left, right);
        var leftArm = Nand(left, shared);
        var rightArm = Nand(right, shared);
        return Nand(leftArm, rightArm);
    }

    public Signal Xnor(Signal left, Signal right) => Not(Xor(left, right));

    /// <summary>Returns <paramref name="whenOn"/> when select is On.</summary>
    public Signal Mux(Signal select, Signal whenOn, Signal whenOff)
    {
        var notSelect = Nand(select, select);
        var onArm = Nand(select, whenOn);
        var offArm = Nand(notSelect, whenOff);
        return Nand(onArm, offArm);
    }
}
