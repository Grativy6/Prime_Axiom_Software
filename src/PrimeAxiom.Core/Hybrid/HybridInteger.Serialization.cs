using System.Globalization;
using System.Numerics;
using System.Text;
using System.Text.Json;
using PrimeAxiom.Core.Substrate;

namespace PrimeAxiom.Core.Hybrid;

public sealed record HybridSerializationResult(string Data, HybridReceipt Receipt);

public sealed partial class HybridInteger
{
    private const string SerializationSchema = "prime-axiom-hybrid-integer-v1";
    private const int MaximumSerializedBytes = 1_048_576;

    /// <summary>
    /// Deterministic numeric serialization. Provenance is intentionally omitted:
    /// it is evidence about production, not part of the integer's identity.
    /// </summary>
    public HybridSerializationResult Serialize()
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = false }))
        {
            writer.WriteStartObject();
            writer.WriteString("schema", SerializationSchema);
            writer.WritePropertyName("bank");
            writer.WriteStartArray();
            foreach (var prime in Bank)
            {
                writer.WriteNumberValue(prime);
            }

            writer.WriteEndArray();
            writer.WriteNumber("exponentWidth", ExponentWidth);
            writer.WriteNumber("sign", Sign);
            writer.WriteString("cofactor", Cofactor.ToString(CultureInfo.InvariantCulture));
            writer.WritePropertyName("exponents");
            writer.WriteStartArray();
            foreach (var exponent in _exponents)
            {
                writer.WriteStringValue(exponent.ToUnsigned().ToString(CultureInfo.InvariantCulture));
            }

            writer.WriteEndArray();
            writer.WritePropertyName("knowledge");
            writer.WriteStartArray();
            foreach (var state in _knowledge)
            {
                writer.WriteStringValue(state.ToString());
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        var data = Encoding.UTF8.GetString(stream.ToArray());
        if (Encoding.UTF8.GetByteCount(data) > MaximumSerializedBytes)
        {
            return new HybridSerializationResult(
                string.Empty,
                Receipt(
                    "SERIALIZE",
                    false,
                    HybridFailure.InvalidSerialization,
                    HybridDomain.Boundary,
                    HybridCostLedger.Zero.Add(
                        CostPhase.Egress,
                        new HybridCostVector(
                            GateCost.Zero,
                            LaneReads: LaneCount,
                            MetadataReads: LaneCount + 3L,
                            SerializedBytes: Encoding.UTF8.GetByteCount(data))),
                    Validity,
                    null,
                    "Serialization exceeds the explicit decoder allocation cap; no partial frame is returned"));
        }

        var cost = HybridCostLedger.Zero.Add(
            CostPhase.Egress,
            new HybridCostVector(
                GateCost.Zero,
                LaneReads: LaneCount,
                MetadataReads: LaneCount + 3L,
                SerializedBytes: Encoding.UTF8.GetByteCount(data)));
        return new HybridSerializationResult(
            data,
            Receipt(
                "SERIALIZE",
                true,
                HybridFailure.None,
                HybridDomain.Boundary,
                cost,
                Validity,
                Validity,
                "Canonical JSON field order and invariant-culture integer strings; provenance excluded"));
    }

    public static HybridResult<HybridInteger> Deserialize(string data)
    {
        if (data is null)
        {
            return InvalidSerialization("Input is null.", 0);
        }

        var byteCount = Encoding.UTF8.GetByteCount(data);
        if (byteCount > MaximumSerializedBytes)
        {
            return InvalidSerialization("Input exceeds the decoder allocation cap.", byteCount);
        }

        try
        {
            using var document = JsonDocument.Parse(
                data,
                new JsonDocumentOptions
                {
                    AllowTrailingCommas = false,
                    CommentHandling = JsonCommentHandling.Disallow,
                    MaxDepth = 8,
                });
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return InvalidSerialization("Top-level JSON value must be an object.", byteCount);
            }

            var expected = new HashSet<string>(StringComparer.Ordinal)
            {
                "schema", "bank", "exponentWidth", "sign", "cofactor", "exponents", "knowledge",
            };
            var properties = root.EnumerateObject().ToArray();
            if (properties.Length != expected.Count ||
                properties.Select(property => property.Name).Distinct(StringComparer.Ordinal).Count() != expected.Count ||
                properties.Any(property => !expected.Contains(property.Name)))
            {
                return InvalidSerialization("Properties must be exactly the versioned schema fields, with no duplicates.", byteCount);
            }

            if (root.GetProperty("schema").ValueKind != JsonValueKind.String ||
                root.GetProperty("schema").GetString() != SerializationSchema)
            {
                return InvalidSerialization("Unknown serialization schema.", byteCount);
            }

            var bankElement = root.GetProperty("bank");
            var exponentElement = root.GetProperty("exponents");
            var knowledgeElement = root.GetProperty("knowledge");
            if (bankElement.ValueKind != JsonValueKind.Array ||
                exponentElement.ValueKind != JsonValueKind.Array ||
                knowledgeElement.ValueKind != JsonValueKind.Array)
            {
                return InvalidSerialization("Bank, exponents, and knowledge must be arrays.", byteCount);
            }

            var primeElements = bankElement.EnumerateArray().ToArray();
            if (primeElements.Length > ValuationBank.MaximumLanes)
            {
                return InvalidSerialization("Bank exceeds the bounded lane count.", byteCount);
            }

            var primes = new int[primeElements.Length];
            for (var index = 0; index < primeElements.Length; index++)
            {
                if (!TryGetCanonicalJsonInt32(primeElements[index], out primes[index]))
                {
                    return InvalidSerialization("Bank contains a noncanonical Int32 label.", byteCount);
                }
            }
            ValuationBank bank;
            try
            {
                bank = primes.Length == 0
                    ? ValuationBank.Empty
                    : new ValuationBank(primes, BankStrategy.Configured, "decoded");
            }
            catch (ArgumentException exception)
            {
                return InvalidSerialization($"Invalid bank: {exception.Message}", byteCount);
            }

            var widthElement = root.GetProperty("exponentWidth");
            var signElement = root.GetProperty("sign");
            if (!TryGetCanonicalJsonInt32(widthElement, out var width) ||
                width <= 0 || width > MaximumExponentWidth ||
                !TryGetCanonicalJsonInt32(signElement, out var sign))
            {
                return InvalidSerialization("Exponent width or sign is not a bounded Int32 value.", byteCount);
            }

            var cofactorElement = root.GetProperty("cofactor");
            if (cofactorElement.ValueKind != JsonValueKind.String ||
                !TryParseCanonicalNonnegative(cofactorElement.GetString(), out var cofactor))
            {
                return InvalidSerialization("Cofactor must be a minimally formatted nonnegative decimal string.", byteCount);
            }

            var exponentStrings = exponentElement.EnumerateArray().ToArray();
            var knowledgeStrings = knowledgeElement.EnumerateArray().ToArray();
            if (exponentStrings.Length != bank.Count || knowledgeStrings.Length != bank.Count)
            {
                return InvalidSerialization("Lane arrays must match the bank length.", byteCount);
            }

            var exponents = new BigInteger[bank.Count];
            var knowledge = new ValuationKnowledge[bank.Count];
            for (var lane = 0; lane < bank.Count; lane++)
            {
                if (exponentStrings[lane].ValueKind != JsonValueKind.String ||
                    !TryParseCanonicalNonnegative(exponentStrings[lane].GetString(), out exponents[lane]))
                {
                    return InvalidSerialization($"Exponent lane {lane} is not a canonical nonnegative decimal string.", byteCount);
                }

                if (knowledgeStrings[lane].ValueKind != JsonValueKind.String ||
                    !Enum.TryParse<ValuationKnowledge>(knowledgeStrings[lane].GetString(), ignoreCase: false, out knowledge[lane]) ||
                    !Enum.IsDefined(knowledge[lane]))
                {
                    return InvalidSerialization($"Knowledge lane {lane} has an unknown state.", byteCount);
                }
            }

            var decoded = FromStructured(sign, cofactor, exponents, bank, width, knowledge);
            var decodeCost = new HybridCostVector(
                GateCost.Zero,
                LaneReads: bank.Count,
                LaneWrites: bank.Count,
                MetadataReads: bank.Count + 3L,
                MetadataWrites: bank.Count + 3L,
                SerializedBytes: byteCount);
            if (!decoded.Receipt.Succeeded)
            {
                return new HybridResult<HybridInteger>(
                    null,
                    decoded.Receipt with
                    {
                        Operation = "DESERIALIZE",
                        Failure = HybridFailure.InvalidSerialization,
                        Cost = decoded.Receipt.Cost + HybridCostLedger.Zero.Add(CostPhase.Ingress, decodeCost),
                        Scope = "Decoded fields failed the executable representation contract",
                    });
            }

            return new HybridResult<HybridInteger>(
                decoded.Value,
                decoded.Receipt with
                {
                    Operation = "DESERIALIZE",
                    Cost = decoded.Receipt.Cost + HybridCostLedger.Zero.Add(CostPhase.Ingress, decodeCost),
                    Scope = "Strict schema decode followed by full component validation",
                });
        }
        catch (JsonException exception)
        {
            return InvalidSerialization($"Invalid JSON: {exception.Message}", byteCount);
        }
        catch (InvalidOperationException exception)
        {
            return InvalidSerialization($"Invalid field type: {exception.Message}", byteCount);
        }
    }

    public static HybridResult<HybridInteger> FromClaimedMagnitude(
        BigInteger claimedMagnitude,
        int sign,
        BigInteger cofactor,
        IEnumerable<BigInteger> exponents,
        ValuationBank bank,
        int exponentWidth,
        IEnumerable<ValuationKnowledge>? knowledge = null)
    {
        var components = FromStructured(sign, cofactor, exponents, bank, exponentWidth, knowledge);
        if (!components.Receipt.Succeeded)
        {
            return new HybridResult<HybridInteger>(
                null,
                components.Receipt with
                {
                    Operation = "CLAIMED_MAGNITUDE_INGRESS",
                    Scope = "Component validation failed before magnitude comparison",
                });
        }

        var reconstructed = components.Value!.Reconstruct();
        var comparison = new HybridCostVector(
            GateCost.Zero,
            CofactorComparisons: 1,
            BinaryOperandBits: checked(BitLength(reconstructed.Value) + BitLength(claimedMagnitude)));
        var verificationWork = reconstructed.Receipt.Cost.Total + comparison;
        var verificationLedger = components.Receipt.Cost.Add(CostPhase.Ingress, verificationWork);
        if (reconstructed.Value != claimedMagnitude)
        {
            return Failed<HybridInteger>(
                "CLAIMED_MAGNITUDE_INGRESS",
                HybridFailure.ClaimedMagnitudeMismatch,
                HybridDomain.Boundary,
                verificationLedger,
                null,
                "Validated components do not equal the externally claimed exact magnitude");
        }

        return new HybridResult<HybridInteger>(
            components.Value,
            Receipt(
                "CLAIMED_MAGNITUDE_INGRESS",
                true,
                HybridFailure.None,
                HybridDomain.Boundary,
                verificationLedger,
                null,
                components.Value.Validity,
                "Component contract and exact equality to the claimed magnitude were both checked"));
    }

    private static bool TryParseCanonicalNonnegative(string? text, out BigInteger value)
    {
        value = BigInteger.Zero;
        if (string.IsNullOrEmpty(text) || (text.Length > 1 && text[0] == '0') || text.Any(character => character is < '0' or > '9'))
        {
            return false;
        }

        return BigInteger.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value) && value >= BigInteger.Zero;
    }

    private static bool TryGetCanonicalJsonInt32(JsonElement element, out int value)
    {
        value = 0;
        return element.ValueKind == JsonValueKind.Number &&
               element.TryGetInt32(out value) &&
               element.GetRawText() == value.ToString(CultureInfo.InvariantCulture);
    }

    private static HybridResult<HybridInteger> InvalidSerialization(string detail, long byteCount) =>
        Failed<HybridInteger>(
            "DESERIALIZE",
            HybridFailure.InvalidSerialization,
            HybridDomain.Boundary,
            HybridCostLedger.Zero.Add(
                CostPhase.Ingress,
                new HybridCostVector(GateCost.Zero, SerializedBytes: byteCount)),
            null,
            "Malformed input never becomes an executable value",
            detail);
}
