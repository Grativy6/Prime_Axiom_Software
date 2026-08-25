using System.Security.Cryptography;
using System.Globalization;
using System.Numerics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PrimeAxiom.Cli;

internal static class Build004Protocol
{
    public const string ProtocolId = "PAS-BUILD004-EXACT-LINEAGE-0001";
    public const string FrozenPlanSha256 = "2482698A57E857F07DBDEB7103B09EC36317661A0413ABBC2B20FAB7F44B53D1";
    public const string BaselineCommit = "31dd150540bac79de3ee5925b44afdb7abaf327a";
    public const ulong MasterSeed = 0x0000000050415334UL;
    public const string FrameworkStatus = "BOUNDED_EXACT_LINEAGE_TOOLKIT_VALIDATED";
    public const string PartialStatus = "PARTIAL — FINAL DECISION NOT EARNED";
    public const string HardwareStatus = "NOT_MEASURED";
    public const string SecurityStatus = "NOT_CRYPTOGRAPHIC";
    public const string PrivacyStatus = "NO_PRIVACY";
    public const string CanonicalJsonContract =
        "COMPACT_UTF8_JSON_V1__CAMEL_CASE__ENUM_STRINGS__ALL_NUMBERS_AS_CANONICAL_STRINGS__BIGINTEGER_DECIMAL__NO_BOM";
    public const string ClaimCeiling =
        "Bounded exact-software and abstract-structure evidence under PAS-BUILD004-EXACT-LINEAGE-0001 only; no source-authenticity, empirical-validity, privacy, cryptographic-security, PAL-conformance, universal-performance, or hardware-PPA claim.";

    public static JsonSerializerOptions JsonOptions { get; } = CreateJsonOptions(writeIndented: true);

    private static JsonSerializerOptions CanonicalJsonOptions { get; } = CreateJsonOptions(writeIndented: false);

    public static void WriteJson<T>(string path, T value)
    {
        var json = JsonSerializer.Serialize(value, JsonOptions).Replace("\r\n", "\n", StringComparison.Ordinal);
        File.WriteAllText(path, json + "\n", new UTF8Encoding(false));
    }

    public static string FileSha256(string path) =>
        Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));

    public static string BytesSha256(ReadOnlySpan<byte> bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes));

    public static int CanonicalJsonUtf8ByteLength<T>(T value) =>
        JsonSerializer.SerializeToUtf8Bytes(value, CanonicalJsonOptions).Length;

    private static JsonSerializerOptions CreateJsonOptions(bool writeIndented)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = writeIndented,
            NumberHandling = JsonNumberHandling.WriteAsString,
        };
        options.Converters.Add(new JsonStringEnumConverter());
        options.Converters.Add(new BigIntegerDecimalStringConverter());
        return options;
    }

    private sealed class BigIntegerDecimalStringConverter : JsonConverter<BigInteger>
    {
        public override BigInteger Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.String ||
                !BigInteger.TryParse(
                    reader.GetString(),
                    NumberStyles.AllowLeadingSign,
                    CultureInfo.InvariantCulture,
                    out var value))
            {
                throw new JsonException("A BigInteger must be a canonical decimal string.");
            }

            return value;
        }

        public override void Write(
            Utf8JsonWriter writer,
            BigInteger value,
            JsonSerializerOptions options) =>
            writer.WriteStringValue(value.ToString(CultureInfo.InvariantCulture));

        public override BigInteger ReadAsPropertyName(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options)
        {
            if (!BigInteger.TryParse(
                    reader.GetString(),
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out var value) ||
                value.Sign < 0)
            {
                throw new JsonException("A BigInteger dictionary key must be a nonnegative decimal string.");
            }

            return value;
        }

        public override void WriteAsPropertyName(
            Utf8JsonWriter writer,
            BigInteger value,
            JsonSerializerOptions options) =>
            writer.WritePropertyName(value.ToString(CultureInfo.InvariantCulture));
    }
}
