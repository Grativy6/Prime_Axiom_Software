using System.Collections.ObjectModel;
using System.Globalization;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;

namespace PrimeAxiom.Core.Build004.Probes;

public sealed record ProbeBomLine
{
    public ProbeBomLine(
        string supplierId,
        string componentId,
        string lotId,
        BigInteger quantity,
        string localProvenanceLabel)
    {
        if (string.IsNullOrWhiteSpace(supplierId))
        {
            throw new ArgumentException("Supplier identity is required.", nameof(supplierId));
        }

        if (string.IsNullOrWhiteSpace(componentId))
        {
            throw new ArgumentException("Component identity is required.", nameof(componentId));
        }

        if (string.IsNullOrWhiteSpace(lotId))
        {
            throw new ArgumentException("Lot identity is required.", nameof(lotId));
        }

        ArgumentOutOfRangeException.ThrowIfNegative(quantity);

        if (string.IsNullOrWhiteSpace(localProvenanceLabel))
        {
            throw new ArgumentException("A local provenance label is required.", nameof(localProvenanceLabel));
        }

        SupplierId = supplierId;
        ComponentId = componentId;
        LotId = lotId;
        Quantity = quantity;
        LocalProvenanceLabel = localProvenanceLabel;
    }

    public string SupplierId { get; }

    public string ComponentId { get; }

    public string LotId { get; }

    public BigInteger Quantity { get; }

    public string LocalProvenanceLabel { get; }

    internal string ToCanonicalString() => string.Join(
        '|',
        "schema=PAS-BUILD004-BOM-LINE-2",
        $"supplier.utf16={EncodeText(SupplierId)}",
        $"component.utf16={EncodeText(ComponentId)}",
        $"lot.utf16={EncodeText(LotId)}",
        $"quantity={Quantity.ToString(CultureInfo.InvariantCulture)}",
        $"provenance.utf16={EncodeText(LocalProvenanceLabel)}");

    private static string EncodeText(string value)
    {
        var encoded = new StringBuilder(value.Length * 4);
        foreach (var codeUnit in value)
        {
            encoded.Append(((ushort)codeUnit).ToString("X4", CultureInfo.InvariantCulture));
        }

        return encoded.ToString();
    }
}

/// <summary>
/// Deliberately tiny BOM demonstration. Its lineage label is local and is not
/// presented as a substitute for the Build 004 content-addressed DAG.
/// </summary>
public sealed class ProbeBomQuantityReceipt
{
    public const string IntegrationBoundary =
        "TOPOLOGY_PRESERVING_RECEIPT_REQUIRED__PERSISTENT_TYPED_DAG_TESTED";

    private readonly ReadOnlyCollection<ProbeBomLine> _lines;
    private readonly ReadOnlyCollection<string> _componentSupport;

    private ProbeBomQuantityReceipt(string receiptId, IReadOnlyList<ProbeBomLine> lines)
    {
        if (string.IsNullOrWhiteSpace(receiptId))
        {
            throw new ArgumentException("BOM receipt identity is required.", nameof(receiptId));
        }

        ReceiptId = receiptId;
        var ordered = lines
            .OrderBy(line => line.SupplierId, StringComparer.Ordinal)
            .ThenBy(line => line.ComponentId, StringComparer.Ordinal)
            .ThenBy(line => line.LotId, StringComparer.Ordinal)
            .ThenBy(line => line.LocalProvenanceLabel, StringComparer.Ordinal)
            .ThenBy(line => line.Quantity)
            .ToArray();
        _lines = Array.AsReadOnly(ordered);
        _componentSupport = Array.AsReadOnly(
            ordered.Select(line => line.ComponentId)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray());
        ComputedQuantity = ordered.Aggregate(
            BigInteger.Zero,
            (current, line) => current + line.Quantity);
        LineageDigestSha256 = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(ToCanonicalLineageString())));
    }

    public string ReceiptId { get; }

    public BigInteger ComputedQuantity { get; }

    public IReadOnlyList<ProbeBomLine> Lines => _lines;

    public IReadOnlyList<string> ComponentSupport => _componentSupport;

    public string LineageDigestSha256 { get; }

    public static ProbeBomQuantityReceipt Create(
        string receiptId,
        IEnumerable<ProbeBomLine> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);
        var copy = lines.ToArray();
        if (copy.Length == 0)
        {
            throw new ArgumentException("A BOM receipt requires at least one line.", nameof(lines));
        }

        if (copy.Any(line => line is null))
        {
            throw new ArgumentException("BOM lines cannot contain null.", nameof(lines));
        }

        return new ProbeBomQuantityReceipt(receiptId, copy);
    }

    public IReadOnlyList<string> SharedComponentKeys(ProbeBomQuantityReceipt other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return Array.AsReadOnly(
            _componentSupport.Intersect(other._componentSupport, StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray());
    }

    public bool HasSameComputedValueButDifferentLineage(ProbeBomQuantityReceipt other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return ComputedQuantity == other.ComputedQuantity &&
               LineageDigestSha256 != other.LineageDigestSha256;
    }

    private string ToCanonicalLineageString() => string.Join(
        '|',
        "schema=PAS-BUILD004-BOM-PROBE-2",
        $"line-count={_lines.Count.ToString(CultureInfo.InvariantCulture)}",
        string.Join(';', _lines.Select(line => line.ToCanonicalString())));
}
