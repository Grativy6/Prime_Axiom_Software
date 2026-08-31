using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PrimeAxiom.Cli;

internal static class Build005Protocol
{
    public const string ProtocolId = "PAH-BUILD005-DEMAND-VALUATION-0001";
    public const string FrozenPlanSha256 = "8B76649A4D4E7E60B756BCFB5FDA7954385A10A9E6DDD520C97123B845CE9031";
    public const string BaselineCommit = "1fff29e2f1e454921aa51cb4a91bd5b41821ebcc";
    public const string FreezeCommit = "3ffb86b";
    public const ulong MasterSeed = 0x5041_4857_4230_3035UL;
    public const string PartialStatus = "PARTIAL — FINAL DECISION NOT EARNED";
    public const string ClaimCeiling =
        "Bounded exact semantic, host-software, and declared NAND/DFF evidence under PAH-BUILD005-DEMAND-VALUATION-0001 only; no universal arithmetic, novelty, FPGA/ASIC PPA, physical-energy, fabricated-hardware, or PAL-conformance claim.";

    public static readonly int[] ExhaustiveWidths = [8];
    public static readonly int[] DecisionWidths = [16, 32];
    public static readonly int[] CacheSizes = [0, 1, 2, 4];
    public static readonly int[] SpeculationBudgets = [1, 4];
    public static readonly int[] PrimeCatalog = [2, 3, 5, 7, 11, 13, 17, 19, 23, 29, 31];
    public static readonly int[] CompositeControls = [4, 6, 9, 10, 15, 21, 25, 27, 33, 35];

    public static JsonSerializerOptions JsonOptions { get; } = CreateJsonOptions();

    public static ulong DeriveSeed(int width, string family, int replicate = 0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(family);
        var name = string.Create(
            CultureInfo.InvariantCulture,
            $"{ProtocolId}/{width}/{family}/{replicate}");
        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(name));
        return BinaryPrimitives.ReadUInt64LittleEndian(digest) ^ MasterSeed;
    }

    public static void VerifyFrozenPlan(string repositoryRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRoot);
        var path = Path.Combine(repositoryRoot, "research", "build005_experiment_plan.md");
        var actual = FileSha256(path);
        if (!string.Equals(actual, FrozenPlanSha256, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Frozen Build 005 plan hash mismatch. Expected {FrozenPlanSha256}; observed {actual}.");
        }
    }

    public static void WriteJson<T>(string path, T value)
    {
        var json = JsonSerializer.Serialize(value, JsonOptions);
        WriteLfText(path, json + "\n");
    }

    public static void WriteLfText(string path, string content)
    {
        var parent = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(parent))
        {
            Directory.CreateDirectory(parent);
        }

        var normalized = content
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');
        File.WriteAllText(path, normalized, new UTF8Encoding(false));
    }

    public static string FileSha256(string path) =>
        Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));

    public static string BytesSha256(ReadOnlySpan<byte> bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes));

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}

internal sealed class Build005SplitMix64
{
    private ulong _state;

    public Build005SplitMix64(ulong seed)
    {
        _state = seed;
    }

    public ulong NextUInt64()
    {
        _state = unchecked(_state + 0x9E37_79B9_7F4A_7C15UL);
        var value = _state;
        value = unchecked((value ^ (value >> 30)) * 0xBF58_476D_1CE4_E5B9UL);
        value = unchecked((value ^ (value >> 27)) * 0x94D0_49BB_1331_11EBUL);
        return value ^ (value >> 31);
    }

    public ulong NextBelow(ulong exclusiveUpperBound)
    {
        ArgumentOutOfRangeException.ThrowIfZero(exclusiveUpperBound);
        var threshold = unchecked(0UL - exclusiveUpperBound) % exclusiveUpperBound;
        while (true)
        {
            var candidate = NextUInt64();
            if (candidate >= threshold)
            {
                return candidate % exclusiveUpperBound;
            }
        }
    }
}
