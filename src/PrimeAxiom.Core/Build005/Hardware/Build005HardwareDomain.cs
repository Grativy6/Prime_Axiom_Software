using PrimeAxiom.Core.Hardware;
using PrimeAxiom.Core.Substrate;

namespace PrimeAxiom.Core.Build005.Hardware;

/// <summary>
/// Frozen widths and field sizes for the Build 005 declared hardware artifact.
/// Prime-catalogue validation remains configuration evidence; these builders
/// consume a four-bit catalogue index rather than performing primality tests.
/// </summary>
public static class Build005HardwareDomain
{
    public const int SlotWidth = 2;

    public const int GenerationWidth = 8;

    public const int PrimeIndexWidth = 4;

    public const int CacheIndexWidth = 2;

    public static IReadOnlyList<int> SupportedWidths { get; } = [8, 16, 32];

    public static IReadOnlyList<int> SupportedCacheCapacities { get; } = [0, 1, 2, 4];

    public static IReadOnlyList<int> PrimeCatalogue { get; } =
        [2, 3, 5, 7, 11, 13, 17, 19, 23, 29, 31];

    public static int ExponentWidth(int width)
    {
        ValidateWidth(width);
        return RequiredBits(width);
    }

    public static int FrontierLineBits(int width)
    {
        ValidateWidth(width);
        return checked(
            1 +
            SlotWidth +
            GenerationWidth +
            PrimeIndexWidth +
            ExponentWidth(width) +
            width +
            1 +
            1);
    }

    public static bool IsSupportedWidth(int width) => SupportedWidths.Contains(width);

    public static bool IsSupportedCacheCapacity(int capacity) =>
        SupportedCacheCapacities.Contains(capacity);

    public static void ValidateWidth(int width)
    {
        if (!IsSupportedWidth(width))
        {
            throw new ArgumentOutOfRangeException(
                nameof(width),
                width,
                "Build 005 declared hardware widths are frozen to 8, 16, and 32 bits.");
        }
    }

    public static void ValidateCacheCapacity(int capacity)
    {
        if (!IsSupportedCacheCapacity(capacity))
        {
            throw new ArgumentOutOfRangeException(
                nameof(capacity),
                capacity,
                "Build 005 declared cache capacities are frozen to 0, 1, 2, and 4 lines.");
        }
    }

    internal static int RequiredBits(int maximumInclusive)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maximumInclusive, 1);

        var bits = 0;
        var value = maximumInclusive;
        while (value != 0)
        {
            bits++;
            value >>= 1;
        }

        return bits;
    }
}

public sealed record DeclaredComponentCost(
    string Component,
    NandStaticMetrics Metrics);

/// <summary>
/// An exact additive inventory of separately declared netlists. Depth, fanout,
/// wire topology, and ports are intentionally not combined because the three
/// components are not one routed or integrated graph.
/// </summary>
public sealed record CompositionalServiceCost(
    int Width,
    int CacheCapacity,
    int Nand2StaticAdditive,
    int DffStaticAdditive,
    int StateBitsAdditive,
    int CacheLineBits,
    IReadOnlyList<DeclaredComponentCost> Components)
{
    public const string EvidenceClass = "STRUCTURAL_DECLARED_COMPOSITIONAL";

    public const bool IsIntegratedNetlist = false;
}

internal static class Build005HardwareBits
{
    public static NandSignal[] Inputs(
        NandNetlistBuilder builder,
        string name,
        int width,
        string region) =>
        Enumerable.Range(0, width)
            .Select(index => builder.Input($"{name}[{index}]", $"{region}/bit:{index}"))
            .ToArray();

    public static NandSignal[] States(
        NandNetlistBuilder builder,
        string name,
        int width,
        string region) =>
        Enumerable.Range(0, width)
            .Select(index => builder.State($"{name}[{index}]", BitState.Off, $"{region}/bit:{index}"))
            .ToArray();

    public static NandSignal[] ConstantWord(
        NandNetlistBuilder builder,
        string name,
        int width,
        uint value,
        string region) =>
        Enumerable.Range(0, width)
            .Select(index => builder.Constant(
                $"{name}[{index}]",
                ((value >> index) & 1U) == 0 ? BitState.Off : BitState.On,
                $"{region}/bit:{index}"))
            .ToArray();

    public static NandSignal[] ZeroWord(
        NandNetlistBuilder builder,
        string name,
        int width,
        string region)
    {
        var zero = builder.Constant($"{name}.zero", BitState.Off, region);
        return Enumerable.Repeat(zero, width).ToArray();
    }

    public static void Outputs(
        NandNetlistBuilder builder,
        string name,
        IReadOnlyList<NandSignal> signals,
        string region)
    {
        for (var index = 0; index < signals.Count; index++)
        {
            builder.Output($"{name}[{index}]", signals[index], $"{region}/bit:{index}");
        }
    }

    public static NandSignal[] MuxWord(
        NandNetlistBuilder builder,
        NandSignal select,
        IReadOnlyList<NandSignal> whenOn,
        IReadOnlyList<NandSignal> whenOff,
        string prefix,
        string region)
    {
        if (whenOn.Count == 0 || whenOn.Count != whenOff.Count)
        {
            throw new ArgumentException("Mux words must have equal positive widths.");
        }

        var result = new NandSignal[whenOn.Count];
        for (var index = 0; index < result.Length; index++)
        {
            result[index] = NandLogic.Mux(
                builder,
                select,
                whenOn[index],
                whenOff[index],
                $"{prefix}.bit[{index}]",
                $"{region}/bit:{index}");
        }

        return result;
    }

    public static NandSignal EqualWord(
        NandNetlistBuilder builder,
        IReadOnlyList<NandSignal> left,
        IReadOnlyList<NandSignal> right,
        string prefix,
        string region)
    {
        if (left.Count == 0 || left.Count != right.Count)
        {
            throw new ArgumentException("Equality words must have equal positive widths.");
        }

        var equal = builder.Constant($"{prefix}.initial", BitState.On, $"{region}/initial");
        for (var index = 0; index < left.Count; index++)
        {
            var same = NandLogic.Xnor(
                builder,
                left[index],
                right[index],
                $"{prefix}.same[{index}]",
                $"{region}/bit:{index}");
            equal = NandLogic.And(
                builder,
                equal,
                same,
                $"{prefix}.accumulate[{index}]",
                $"{region}/bit:{index}");
        }

        return equal;
    }

    public static NandSignal ReduceOr(
        NandNetlistBuilder builder,
        IReadOnlyList<NandSignal> signals,
        string prefix,
        string region)
    {
        if (signals.Count == 0)
        {
            throw new ArgumentException("An OR reduction requires at least one signal.", nameof(signals));
        }

        var result = signals[0];
        for (var index = 1; index < signals.Count; index++)
        {
            result = NandLogic.Or(
                builder,
                result,
                signals[index],
                $"{prefix}.or[{index}]",
                $"{region}/step:{index}");
        }

        return result;
    }

    public static NandSignal IsZero(
        NandNetlistBuilder builder,
        IReadOnlyList<NandSignal> word,
        string prefix,
        string region)
    {
        var any = ReduceOr(builder, word, $"{prefix}.any", region);
        return NandLogic.Not(builder, any, $"{prefix}.not", region);
    }

    public static Dictionary<string, BitState> InputWord(
        string name,
        int width,
        uint value)
    {
        var result = new Dictionary<string, BitState>(StringComparer.Ordinal);
        WriteWord(result, name, width, value);
        return result;
    }

    public static void WriteWord(
        IDictionary<string, BitState> destination,
        string name,
        int width,
        uint value)
    {
        for (var index = 0; index < width; index++)
        {
            destination[$"{name}[{index}]"] =
                ((value >> index) & 1U) == 0 ? BitState.Off : BitState.On;
        }
    }

    public static uint ReadWord(
        IReadOnlyDictionary<string, BitState> source,
        string name,
        int width)
    {
        uint result = 0;
        for (var index = 0; index < width; index++)
        {
            if (source[$"{name}[{index}]"] == BitState.On)
            {
                result |= 1U << index;
            }
        }

        return result;
    }

    public static bool ReadFlag(IReadOnlyDictionary<string, BitState> source, string name) =>
        source[name] == BitState.On;
}
