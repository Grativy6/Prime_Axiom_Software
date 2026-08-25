using System.Collections.ObjectModel;
using System.Globalization;

namespace PrimeAxiom.Core.Build004.Lineage;

public enum LineageEvidenceReferenceKind
{
    Source,
    CalibrationValidity,
    Uncertainty,
    Residual,
}

/// <summary>
/// An immutable reference to evidence retained outside the lineage envelope.
/// The digest binds referenced bytes; it does not authenticate their author or
/// establish the truth of their contents.
/// </summary>
public sealed class LineageEvidenceReference : IEquatable<LineageEvidenceReference>
{
    public const string Schema = "prime-axiom-lineage-evidence-reference-v1";
    public const int MaximumReferenceIdUtf8Bytes = 4096;

    public LineageEvidenceReference(
        LineageEvidenceReferenceKind kind,
        string referenceId,
        string contentSha256)
    {
        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind));
        }

        referenceId = LineageText.RequireToken(referenceId, nameof(referenceId));
        if (LineageText.GetUtf8Bytes(referenceId, nameof(referenceId)).Length > MaximumReferenceIdUtf8Bytes)
        {
            throw new ArgumentOutOfRangeException(
                nameof(referenceId),
                $"An evidence reference identity cannot exceed {MaximumReferenceIdUtf8Bytes.ToString(CultureInfo.InvariantCulture)} UTF-8 bytes.");
        }

        Kind = kind;
        ReferenceId = referenceId;
        ContentSha256 = LineageHash.RequireSha256(contentSha256, nameof(contentSha256));
    }

    public LineageEvidenceReferenceKind Kind { get; }
    public string ReferenceId { get; }
    public string ContentSha256 { get; }

    internal string Canonical => LineageText.Fields(
        Schema,
        ((int)Kind).ToString(CultureInfo.InvariantCulture),
        Kind.ToString(),
        ReferenceId,
        ContentSha256);

    public bool Equals(LineageEvidenceReference? other) =>
        other is not null &&
        Kind == other.Kind &&
        string.Equals(ReferenceId, other.ReferenceId, StringComparison.Ordinal) &&
        string.Equals(ContentSha256, other.ContentSha256, StringComparison.Ordinal);

    public override bool Equals(object? obj) => Equals(obj as LineageEvidenceReference);

    public override int GetHashCode() => HashCode.Combine(Kind, ReferenceId, ContentSha256);

    internal LineageEvidenceReferenceSnapshot ToSnapshot() => new(
        Kind,
        ReferenceId,
        ContentSha256);
}

public sealed record LineageEvidenceReferenceSnapshot(
    LineageEvidenceReferenceKind Kind,
    string ReferenceId,
    string ContentSha256);

public sealed record LineageEvidenceEnvelopeSnapshot(
    string Schema,
    string EnvelopeId,
    string RootNodeId,
    string RegistryId,
    LineageCompleteness LineageCompleteness,
    PayloadReplayability PayloadReplayability,
    IssuerAuthenticity IssuerAuthenticity,
    IReadOnlyList<LineageEvidenceReferenceSnapshot> EvidenceReferences);

public enum LineageEvidenceEnvelopeVerificationStatus
{
    ValidIntegrityOnly,
    InvalidSnapshot,
    UnsupportedSchema,
    EnvelopeHashMismatch,
    RegistryBindingMismatch,
    DagReplayFailed,
}

public sealed record LineageEvidenceEnvelopeVerificationResult(
    LineageEvidenceEnvelopeVerificationStatus Status,
    bool EnvelopeHashMatches,
    bool RegistryBindingMatches,
    DagVerificationResult? DagReplay,
    string Detail)
{
    public bool IsValid => Status == LineageEvidenceEnvelopeVerificationStatus.ValidIntegrityOnly;

    public const bool EstablishesIssuerAuthentication = false;
}

/// <summary>
/// A bounded, content-addressed binding from a derivation root and registry to
/// independent knowledge declarations and external evidence references.
/// SHA-256 provides content-integrity replay only, never issuer authentication.
/// </summary>
public sealed class LineageEvidenceEnvelope
{
    public const string Schema = "prime-axiom-lineage-evidence-envelope-v1";
    public const int MaximumEvidenceReferences = 64;
    public const string SecurityBoundary =
        "CONTENT_ADDRESS_INTEGRITY_ONLY__NO_SIGNATURE_ISSUER_AUTHENTICATION_EMPIRICAL_VALIDITY_OR_AUTHORITY";

    private readonly ReadOnlyCollection<LineageEvidenceReference> evidenceReferences;

    private LineageEvidenceEnvelope(
        string envelopeId,
        string rootNodeId,
        string registryId,
        LineageCompleteness lineageCompleteness,
        PayloadReplayability payloadReplayability,
        IssuerAuthenticity issuerAuthenticity,
        IReadOnlyList<LineageEvidenceReference> evidenceReferences)
    {
        EnvelopeId = envelopeId;
        RootNodeId = rootNodeId;
        RegistryId = registryId;
        LineageCompleteness = lineageCompleteness;
        PayloadReplayability = payloadReplayability;
        IssuerAuthenticity = issuerAuthenticity;
        this.evidenceReferences = Array.AsReadOnly(evidenceReferences.ToArray());
    }

    public string EnvelopeId { get; }
    public string RootNodeId { get; }
    public string RegistryId { get; }
    public LineageCompleteness LineageCompleteness { get; }
    public PayloadReplayability PayloadReplayability { get; }
    public IssuerAuthenticity IssuerAuthenticity { get; }
    public IReadOnlyList<LineageEvidenceReference> EvidenceReferences => evidenceReferences;

    public static LineageEvidenceEnvelope Create(
        DerivationDag dag,
        string rootNodeId,
        LineageRegistry registry,
        LineageCompleteness lineageCompleteness,
        PayloadReplayability payloadReplayability,
        IssuerAuthenticity issuerAuthenticity,
        IEnumerable<LineageEvidenceReference> evidenceReferences)
    {
        ArgumentNullException.ThrowIfNull(dag);
        ArgumentNullException.ThrowIfNull(registry);
        rootNodeId = RequireCanonicalSha256(rootNodeId, nameof(rootNodeId));
        ValidateAxis(lineageCompleteness, nameof(lineageCompleteness));
        ValidateAxis(payloadReplayability, nameof(payloadReplayability));
        ValidateAxis(issuerAuthenticity, nameof(issuerAuthenticity));
        var orderedEvidence = FreezeAndValidateReferences(evidenceReferences);

        var dagReplay = DerivationDag.VerifySnapshots(
            dag.GetReachableSnapshots(rootNodeId),
            rootNodeId,
            registry);
        if (!dagReplay.IsValid)
        {
            throw new ArgumentException(
                $"The envelope root did not replay against the declared registry: {dagReplay.Status}.",
                nameof(rootNodeId));
        }

        var envelopeId = ComputeEnvelopeId(
            rootNodeId,
            registry.RegistryId,
            lineageCompleteness,
            payloadReplayability,
            issuerAuthenticity,
            orderedEvidence.Select(reference => reference.ToSnapshot()).ToArray());
        return new LineageEvidenceEnvelope(
            envelopeId,
            rootNodeId,
            registry.RegistryId,
            lineageCompleteness,
            payloadReplayability,
            issuerAuthenticity,
            orderedEvidence);
    }

    public LineageEvidenceEnvelopeSnapshot ToSnapshot() => new(
        Schema,
        EnvelopeId,
        RootNodeId,
        RegistryId,
        LineageCompleteness,
        PayloadReplayability,
        IssuerAuthenticity,
        Array.AsReadOnly(evidenceReferences.Select(reference => reference.ToSnapshot()).ToArray()));

    public static LineageEvidenceEnvelopeVerificationResult VerifySnapshot(
        LineageEvidenceEnvelopeSnapshot snapshot,
        IEnumerable<DerivationNodeSnapshot> dagSnapshots,
        LineageRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(dagSnapshots);
        ArgumentNullException.ThrowIfNull(registry);

        if (!string.Equals(snapshot.Schema, Schema, StringComparison.Ordinal))
        {
            return Invalid(
                LineageEvidenceEnvelopeVerificationStatus.UnsupportedSchema,
                "The evidence-envelope schema is not supported.");
        }

        LineageEvidenceReference[] orderedEvidence;
        string envelopeId;
        string rootNodeId;
        string registryId;
        try
        {
            envelopeId = RequireCanonicalSha256(snapshot.EnvelopeId, nameof(snapshot.EnvelopeId));
            rootNodeId = RequireCanonicalSha256(snapshot.RootNodeId, nameof(snapshot.RootNodeId));
            registryId = RequireCanonicalSha256(snapshot.RegistryId, nameof(snapshot.RegistryId));
            ValidateAxis(snapshot.LineageCompleteness, nameof(snapshot.LineageCompleteness));
            ValidateAxis(snapshot.PayloadReplayability, nameof(snapshot.PayloadReplayability));
            ValidateAxis(snapshot.IssuerAuthenticity, nameof(snapshot.IssuerAuthenticity));
            orderedEvidence = FreezeAndValidateReferenceSnapshots(snapshot.EvidenceReferences);
        }
        catch (ArgumentException exception)
        {
            return Invalid(
                LineageEvidenceEnvelopeVerificationStatus.InvalidSnapshot,
                exception.Message);
        }

        var canonicalSnapshots = orderedEvidence.Select(reference => reference.ToSnapshot()).ToArray();
        if (!snapshot.EvidenceReferences.SequenceEqual(canonicalSnapshots))
        {
            return Invalid(
                LineageEvidenceEnvelopeVerificationStatus.InvalidSnapshot,
                "Evidence references are not in canonical kind/reference/digest order.");
        }

        var recomputedId = ComputeEnvelopeId(
            rootNodeId,
            registryId,
            snapshot.LineageCompleteness,
            snapshot.PayloadReplayability,
            snapshot.IssuerAuthenticity,
            canonicalSnapshots);
        var envelopeHashMatches = string.Equals(envelopeId, recomputedId, StringComparison.Ordinal);
        var registryBindingMatches = string.Equals(registryId, registry.RegistryId, StringComparison.Ordinal);

        DagVerificationResult dagReplay;
        try
        {
            dagReplay = DerivationDag.VerifySnapshots(dagSnapshots, rootNodeId, registry);
        }
        catch (ArgumentException exception)
        {
            dagReplay = new DagVerificationResult(DagVerificationStatus.InvalidNodeShape, exception.Message);
        }

        if (!registryBindingMatches)
        {
            return new LineageEvidenceEnvelopeVerificationResult(
                LineageEvidenceEnvelopeVerificationStatus.RegistryBindingMismatch,
                envelopeHashMatches,
                false,
                dagReplay,
                "The envelope registry identity does not match the supplied registry.");
        }

        if (!dagReplay.IsValid)
        {
            return new LineageEvidenceEnvelopeVerificationResult(
                LineageEvidenceEnvelopeVerificationStatus.DagReplayFailed,
                envelopeHashMatches,
                true,
                dagReplay,
                $"The bound derivation root failed replay: {dagReplay.Status}.");
        }

        if (!envelopeHashMatches)
        {
            return new LineageEvidenceEnvelopeVerificationResult(
                LineageEvidenceEnvelopeVerificationStatus.EnvelopeHashMismatch,
                false,
                true,
                dagReplay,
                "The envelope ID does not match the canonical root, registry, axes, and evidence references.");
        }

        return new LineageEvidenceEnvelopeVerificationResult(
            LineageEvidenceEnvelopeVerificationStatus.ValidIntegrityOnly,
            true,
            true,
            dagReplay,
            "The envelope content address, registry binding, and derivation replay are valid. This integrity result establishes no issuer authentication or empirical validity.");
    }

    private static LineageEvidenceReference[] FreezeAndValidateReferences(
        IEnumerable<LineageEvidenceReference> evidenceReferences)
    {
        ArgumentNullException.ThrowIfNull(evidenceReferences);
        var frozen = evidenceReferences.ToArray();
        if (frozen.Any(reference => reference is null))
        {
            throw new ArgumentException("Evidence references cannot contain null.", nameof(evidenceReferences));
        }

        return ValidateAndOrderReferences(frozen, nameof(evidenceReferences));
    }

    private static LineageEvidenceReference[] FreezeAndValidateReferenceSnapshots(
        IReadOnlyList<LineageEvidenceReferenceSnapshot> evidenceReferences)
    {
        ArgumentNullException.ThrowIfNull(evidenceReferences);
        var frozen = evidenceReferences.ToArray();
        if (frozen.Any(reference => reference is null))
        {
            throw new ArgumentException("Evidence-reference snapshots cannot contain null.", nameof(evidenceReferences));
        }

        var materialized = frozen.Select(reference =>
        {
            var contentSha256 = RequireCanonicalSha256(
                reference.ContentSha256,
                nameof(reference.ContentSha256));
            return new LineageEvidenceReference(reference.Kind, reference.ReferenceId, contentSha256);
        }).ToArray();
        return ValidateAndOrderReferences(materialized, nameof(evidenceReferences));
    }

    private static LineageEvidenceReference[] ValidateAndOrderReferences(
        LineageEvidenceReference[] references,
        string parameterName)
    {
        if (references.Length > MaximumEvidenceReferences)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                $"An evidence envelope cannot contain more than {MaximumEvidenceReferences.ToString(CultureInfo.InvariantCulture)} references.");
        }

        if (references
            .GroupBy(reference => (reference.Kind, reference.ReferenceId))
            .Any(group => group.Count() != 1))
        {
            throw new ArgumentException(
                "An evidence envelope cannot repeat a kind/reference identity.",
                parameterName);
        }

        return references
            .OrderBy(reference => reference.Kind)
            .ThenBy(reference => reference.ReferenceId, StringComparer.Ordinal)
            .ThenBy(reference => reference.ContentSha256, StringComparer.Ordinal)
            .ToArray();
    }

    private static string ComputeEnvelopeId(
        string rootNodeId,
        string registryId,
        LineageCompleteness lineageCompleteness,
        PayloadReplayability payloadReplayability,
        IssuerAuthenticity issuerAuthenticity,
        IReadOnlyList<LineageEvidenceReferenceSnapshot> evidenceReferences) =>
        LineageHash.Sha256(LineageText.Fields(
            Schema,
            rootNodeId,
            registryId,
            ((int)lineageCompleteness).ToString(CultureInfo.InvariantCulture),
            lineageCompleteness.ToString(),
            ((int)payloadReplayability).ToString(CultureInfo.InvariantCulture),
            payloadReplayability.ToString(),
            ((int)issuerAuthenticity).ToString(CultureInfo.InvariantCulture),
            issuerAuthenticity.ToString(),
            evidenceReferences.Count.ToString(CultureInfo.InvariantCulture),
            string.Concat(evidenceReferences.Select(reference => LineageText.Fields(
                new LineageEvidenceReference(
                    reference.Kind,
                    reference.ReferenceId,
                    reference.ContentSha256).Canonical)))));

    private static string RequireCanonicalSha256(string value, string parameterName)
    {
        var normalized = LineageHash.RequireSha256(value, parameterName);
        if (!string.Equals(value, normalized, StringComparison.Ordinal))
        {
            throw new ArgumentException("A snapshot SHA-256 field must use canonical uppercase hexadecimal.", parameterName);
        }

        return normalized;
    }

    private static void ValidateAxis<TEnum>(TEnum value, string parameterName)
        where TEnum : struct, Enum
    {
        if (!Enum.IsDefined(value))
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }
    }

    private static LineageEvidenceEnvelopeVerificationResult Invalid(
        LineageEvidenceEnvelopeVerificationStatus status,
        string detail) => new(
            status,
            false,
            false,
            null,
            detail);
}
