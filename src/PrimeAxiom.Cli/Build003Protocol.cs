using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PrimeAxiom.Cli;

internal static class Build003Protocol
{
    public const string ProtocolId = "PAS-BUILD003-PRIME-RECEIPT-0001";
    public const string FrozenPlanSha256 = "8893D15E539F3750981F63AE6B6EE26FBC537D356AE7C5ACDA24F881ED766EE5";
    public const string BaselineCommit = "a83b660443df489c2ff218887953926a33a84c84";
    public const ulong MasterSeed = 0x5041534230303033UL;
    public const string FrameworkStatus = "BOUNDED_TOOL_PATH_VALIDATED";
    public const string MultiplicationConclusion = "LOCAL_EXPONENT_MERGE_AFTER_EXPLICIT_ACQUISITION";
    public const string AdditionConclusion = "MAGNITUDE_ADD_THEN_FRESH_FACTOR_DISCOVERY";
    public const string ClaimCeiling =
        "Bounded functional and deterministic-path evidence only; no hardware advantage, factoring-performance, private reasoning, model understanding, or general LLM accuracy claim.";

    public static JsonSerializerOptions JsonOptions { get; } = CreateJsonOptions();

    public static void WriteJson<T>(string path, T value)
    {
        var json = JsonSerializer.Serialize(value, JsonOptions).Replace("\r\n", "\n", StringComparison.Ordinal);
        File.WriteAllText(path, json + "\n", new UTF8Encoding(false));
    }

    public static string FileSha256(string path) =>
        Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
            NumberHandling = JsonNumberHandling.WriteAsString,
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
