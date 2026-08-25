using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace PrimeAxiom.Cli;

internal static class Build002Protocol
{
    public const string Id = "PAH-BUILD002-CONF0001";
    public const string BaselineCommit = "dfd2e7a409aaa114f054a0b40e4b282c68dc0d52";
    public const string FrozenPlanSha256 = "24C770290A97A1C467DBCC7B4C97CA9EE875EFC21ADE837CA9D96049ACD76745";
    public const ulong MasterSeed = 0x5041_4857_4230_3032UL;

    public static readonly int[] Widths = [4, 6, 8];
    public static readonly int[] PrimeCatalog = [2, 3, 5, 7];

    public static JsonSerializerOptions JsonOptions { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    public static ulong DeriveSeed(int width, string experiment, int traceIndex)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(experiment);
        var name = $"{Id}/{width.ToString(CultureInfo.InvariantCulture)}/{experiment}/{traceIndex.ToString(CultureInfo.InvariantCulture)}";
        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(name));
        return BinaryPrimitives.ReadUInt64LittleEndian(digest);
    }

    public static void VerifyFrozenPlan(string repositoryRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRoot);
        var path = Path.Combine(repositoryRoot, "research", "build002_experiment_plan.md");
        var actual = HashFile(path);
        if (!string.Equals(actual, FrozenPlanSha256, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Frozen Build 002 plan hash mismatch. Expected {FrozenPlanSha256}; observed {actual}.");
        }
    }

    public static string HashFile(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    public static void WriteJson(string path, object value)
    {
        var json = JsonSerializer.Serialize(value, JsonOptions);
        WriteLfText(path, json + "\n");
    }

    public static void WriteCsv(string path, IEnumerable<IReadOnlyList<string>> rows)
    {
        var builder = new StringBuilder();
        foreach (var row in rows)
        {
            builder.AppendJoin(',', row.Select(EscapeCsv));
            builder.Append('\n');
        }

        WriteLfText(path, builder.ToString());
    }

    public static void WriteLfText(string path, string content)
    {
        var parent = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(parent))
        {
            Directory.CreateDirectory(parent);
        }

        var normalized = content.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        File.WriteAllText(path, normalized, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static string EscapeCsv(string value)
    {
        if (value.IndexOfAny([',', '"', '\r', '\n']) < 0)
        {
            return value;
        }

        return '"' + value.Replace("\"", "\"\"", StringComparison.Ordinal) + '"';
    }
}

internal sealed class SplitMix64
{
    private ulong _state;

    public SplitMix64(ulong seed)
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
