using System.Numerics;
using System.Text;
using PrimeAxiom.Core.Build004.Lineage;
using PrimeAxiom.Core.Build004.Probes;

namespace PrimeAxiom.Tests;

public sealed class Build004BoundaryProbeTests
{
    private static readonly DateTimeOffset FrozenInstant =
        new(2026, 8, 25, 12, 0, 0, TimeSpan.Zero);
    private static readonly string[] ExpectedDerivationParentIds = ["derive-a", "derive-b"];
    private static readonly string[] ExpectedEvidenceIds = ["evidence-a", "evidence-b"];
    private static readonly string[] LeftAccumulatorMembers = ["a", "b"];
    private static readonly string[] RightAccumulatorMembers = ["b", "c"];
    private static readonly string[] ReorderedAccumulatorMembers = ["b", "a", "a"];
    private static readonly string[] RecycledEpochMembers = ["a"];

    [Fact]
    public void ExactRatiosAndSignedPrimeCoordinatesRoundTripWithoutUsingUnitAxes()
    {
        var ratio = new ProbeExactRatio(-45, 28);
        var coordinates = ProbeSignedPrimeCoordinates.FromRatio(ratio);

        Assert.Equal(-1, coordinates.Sign);
        Assert.Equal(-2, coordinates.Exponents[new BigInteger(2)]);
        Assert.Equal(2, coordinates.Exponents[new BigInteger(3)]);
        Assert.Equal(1, coordinates.Exponents[new BigInteger(5)]);
        Assert.Equal(-1, coordinates.Exponents[new BigInteger(7)]);
        Assert.Equal(ratio, coordinates.ToRatio());

        var inverse = coordinates.Invert();
        Assert.Equal(new ProbeExactRatio(-28, 45), inverse.ToRatio());
        Assert.Equal(ProbeSignedPrimeCoordinates.FromRatio(ProbeExactRatio.One), coordinates.Compose(inverse));

        var length = ProbeUnitDimensionVector.Create(("L", 1));
        var inverseTime = ProbeUnitDimensionVector.Create(("T", -1));
        var velocity = length.Multiply(inverseTime);

        Assert.Equal(1, velocity.Axes["L"]);
        Assert.Equal(-1, velocity.Axes["T"]);
        Assert.DoesNotContain("2", velocity.Axes.Keys);
        Assert.DoesNotContain(
            ProbeUnitDimensionVector.Contract.Name,
            coordinates.ToCanonicalString(),
            StringComparison.Ordinal);
    }

    [Fact]
    public void NumericAndDimensionProjectionsDeclareTheirIndependentContracts()
    {
        var numeric = ProbeSignedPrimeCoordinates.FromRatio(new ProbeExactRatio(45, 28));
        var dimension = ProbeUnitDimensionVector.Create(("L", 1), ("T", -1));

        Assert.False(string.IsNullOrWhiteSpace(ProbeSignedPrimeCoordinates.Contract.Preserves));
        Assert.False(string.IsNullOrWhiteSpace(ProbeSignedPrimeCoordinates.Contract.Discards));
        Assert.False(string.IsNullOrWhiteSpace(ProbeSignedPrimeCoordinates.Contract.ReplayabilitySemantics));
        Assert.Equal(ProbeSignedPrimeCoordinates.DefaultBasisId, numeric.BasisId);
        Assert.Equal(LineageCompleteness.Exact, numeric.Completeness);
        Assert.Equal(PayloadReplayability.ReplayableExact, numeric.PayloadReplayability);

        Assert.False(string.IsNullOrWhiteSpace(ProbeUnitDimensionVector.Contract.Preserves));
        Assert.False(string.IsNullOrWhiteSpace(ProbeUnitDimensionVector.Contract.Discards));
        Assert.False(string.IsNullOrWhiteSpace(ProbeUnitDimensionVector.Contract.ReplayabilitySemantics));
        Assert.Equal(ProbeUnitDimensionVector.DefaultBasisId, dimension.BasisId);
        Assert.Equal(LineageCompleteness.Exact, dimension.Completeness);
        Assert.Equal(PayloadReplayability.MissingDependency, dimension.PayloadReplayability);
    }

    [Fact]
    public void NumericAndDimensionCompositionRejectMismatchedBasisIdentities()
    {
        var numericLeft = ProbeSignedPrimeCoordinates.FromRatio(
            new ProbeExactRatio(3, 2),
            basisId: "NUMERIC-BASIS-A");
        var numericRight = ProbeSignedPrimeCoordinates.FromRatio(
            new ProbeExactRatio(5, 3),
            basisId: "NUMERIC-BASIS-B");
        Assert.Throws<InvalidOperationException>(() => numericLeft.Compose(numericRight));

        var dimensionLeft = ProbeUnitDimensionVector.CreateDeclared(
            "DIMENSION-BASIS-A",
            LineageCompleteness.Exact,
            PayloadReplayability.MissingDependency,
            ("L", 1));
        var dimensionRight = ProbeUnitDimensionVector.CreateDeclared(
            "DIMENSION-BASIS-B",
            LineageCompleteness.Exact,
            PayloadReplayability.MissingDependency,
            ("T", -1));
        Assert.Throws<InvalidOperationException>(() => dimensionLeft.Multiply(dimensionRight));
        Assert.Throws<InvalidOperationException>(() => dimensionLeft.Divide(dimensionRight));
    }

    [Fact]
    public void ProjectionKnowledgeDeclarationsPropagateWithoutPromotion()
    {
        var exactNumeric = ProbeSignedPrimeCoordinates.FromRatio(
            new ProbeExactRatio(3, 2),
            completeness: LineageCompleteness.Exact,
            payloadReplayability: PayloadReplayability.ReplayableExact);
        var boundedNumeric = ProbeSignedPrimeCoordinates.FromRatio(
            new ProbeExactRatio(5, 3),
            completeness: LineageCompleteness.KnownLowerBound,
            payloadReplayability: PayloadReplayability.DigestOnly);
        var numericComposition = exactNumeric.Compose(boundedNumeric);

        Assert.Equal(LineageCompleteness.KnownLowerBound, numericComposition.Completeness);
        Assert.Equal(PayloadReplayability.DigestOnly, numericComposition.PayloadReplayability);
        Assert.Throws<InvalidOperationException>(() => numericComposition.Invert());

        var exactDimension = ProbeUnitDimensionVector.CreateDeclared(
            ProbeUnitDimensionVector.DefaultBasisId,
            LineageCompleteness.Exact,
            PayloadReplayability.ReplayableExact,
            ("L", 1));
        var boundedDimension = ProbeUnitDimensionVector.CreateDeclared(
            ProbeUnitDimensionVector.DefaultBasisId,
            LineageCompleteness.KnownLowerBound,
            PayloadReplayability.DigestOnly,
            ("T", -1));
        var dimensionComposition = exactDimension.Multiply(boundedDimension);

        Assert.Equal(LineageCompleteness.KnownLowerBound, dimensionComposition.Completeness);
        Assert.Equal(PayloadReplayability.DigestOnly, dimensionComposition.PayloadReplayability);
        Assert.Throws<InvalidOperationException>(() => dimensionComposition.Invert());

        var exactInverse = exactDimension.Invert();
        Assert.Equal(LineageCompleteness.Exact, exactInverse.Completeness);
        Assert.Equal(PayloadReplayability.ReplayableExact, exactInverse.PayloadReplayability);
        Assert.Equal(-1, exactInverse.Axes["L"]);
    }

    [Theory]
    [InlineData(LineageCompleteness.KnownLowerBound, PayloadReplayability.ReplayableExact)]
    [InlineData(LineageCompleteness.Conflict, PayloadReplayability.ReplayableExact)]
    [InlineData(LineageCompleteness.Exact, PayloadReplayability.DigestOnly)]
    [InlineData(LineageCompleteness.Exact, PayloadReplayability.MissingDependency)]
    public void NumericProjectionRefusesExactReconstructionWhenKnowledgeDoesNotEarnIt(
        LineageCompleteness completeness,
        PayloadReplayability replayability)
    {
        var coordinates = ProbeSignedPrimeCoordinates.FromRatio(
            new ProbeExactRatio(15, 8),
            completeness: completeness,
            payloadReplayability: replayability);

        Assert.Throws<InvalidOperationException>(() => coordinates.ToRatio());
    }

    [Fact]
    public void ExactRatioScaleCompositionKeepsNumericDimensionDerivationAndEvidenceAxesSeparate()
    {
        var first = ProbeMeasurementTransformReceipt.ExactRatioScale(
            "scale-a",
            new ProbeExactRatio(3, 2),
            ProbeUnitDimensionVector.Create(("L", 1)),
            "derive-a",
            new[] { ValidEvidence("evidence-a") },
            FrozenInstant);
        var second = ProbeMeasurementTransformReceipt.ExactRatioScale(
            "scale-b",
            new ProbeExactRatio(5, 3),
            ProbeUnitDimensionVector.Create(("T", -1)),
            "derive-b",
            new[] { ValidEvidence("evidence-b") },
            FrozenInstant);

        var composed = ProbeMeasurementTransformReceipt.ComposeExact(
            "scale-c",
            "derive-c",
            first,
            second,
            FrozenInstant);

        Assert.Equal(ProbeBoundaryDisposition.ExactRepresentationLocal, composed.Disposition);
        Assert.Equal(new ProbeExactRatio(5, 2), composed.NominalCoefficient);
        Assert.Equal(composed.NominalCoefficient, composed.NumericFactors!.ToRatio());
        Assert.Equal(1, composed.Dimension.Axes["L"]);
        Assert.Equal(-1, composed.Dimension.Axes["T"]);
        Assert.Equal(LineageCompleteness.Exact, composed.Dimension.Completeness);
        Assert.Equal(PayloadReplayability.MissingDependency, composed.Dimension.PayloadReplayability);
        Assert.Equal(ExpectedDerivationParentIds, composed.ParentDerivationReceiptIds);
        Assert.Equal(ExpectedEvidenceIds, composed.Evidence.Select(item => item.EvidenceId));
        Assert.Equal(new ProbeExactRatio(10, 1), composed.ApplyExact(new ProbeExactRatio(4, 1)));
        Assert.Equal(ProbeEvidenceAuthentication.IntegrityOnly, composed.Evidence[0].Authentication);

        var evidenceList = Assert.IsAssignableFrom<IList<ProbeCalibrationEvidenceEnvelope>>(composed.Evidence);
        Assert.Throws<NotSupportedException>(() => evidenceList.Add(ValidEvidence("mutation")));
    }

    [Fact]
    public void ExactCompositionReassessesEvidenceBeforeAnyCoordinateMerge()
    {
        var first = ProbeMeasurementTransformReceipt.ExactRatioScale(
            "expiring-a",
            new ProbeExactRatio(3, 2),
            ProbeUnitDimensionVector.Create(("L", 1)),
            "derive-expiring-a",
            new[] { ValidEvidence("expiring-evidence-a") },
            FrozenInstant);
        var second = ProbeMeasurementTransformReceipt.ExactRatioScale(
            "expiring-b",
            new ProbeExactRatio(5, 4),
            ProbeUnitDimensionVector.Create(("T", -1)),
            "derive-expiring-b",
            new[] { ValidEvidence("expiring-evidence-b") },
            FrozenInstant);

        var expiredComposition = ProbeMeasurementTransformReceipt.ComposeExact(
            "expired-composition",
            "derive-expired-composition",
            first,
            second,
            FrozenInstant.AddDays(2));

        Assert.Equal(ProbeBoundaryDisposition.Unresolved, expiredComposition.Disposition);
        Assert.Contains("EXPIRED_VALIDITY", expiredComposition.CrossingReason, StringComparison.Ordinal);
        Assert.Equal(new ProbeExactRatio(15, 8), expiredComposition.NominalCoefficient);
        Assert.Null(expiredComposition.NumericFactors);
        Assert.Equal(LineageCompleteness.Conflict, expiredComposition.Dimension.Completeness);
        Assert.Empty(expiredComposition.Dimension.Axes);
        Assert.Throws<InvalidOperationException>(() => expiredComposition.ApplyExact(ProbeExactRatio.One));
    }

    [Fact]
    public void RatioScaleRejectsNegativeCoefficients()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ProbeMeasurementTransformReceipt.ExactRatioScale(
                "negative-scale",
                new ProbeExactRatio(-3, 2),
                ProbeUnitDimensionVector.Dimensionless,
                "derive-negative-scale",
                new[] { ValidEvidence("negative-scale-evidence") },
                FrozenInstant));
    }

    [Theory]
    [InlineData(LineageCompleteness.KnownLowerBound)]
    [InlineData(LineageCompleteness.Conflict)]
    public void RatioScaleRejectsNonExactDimensionProjections(LineageCompleteness completeness)
    {
        var dimension = ProbeUnitDimensionVector.CreateDeclared(
            ProbeUnitDimensionVector.DefaultBasisId,
            completeness,
            PayloadReplayability.ReplayableExact,
            ("L", 1));

        Assert.Throws<ArgumentException>(() =>
            ProbeMeasurementTransformReceipt.ExactRatioScale(
                "non-exact-dimension",
                new ProbeExactRatio(3, 2),
                dimension,
                "derive-non-exact-dimension",
                new[] { ValidEvidence("non-exact-dimension-evidence") },
                FrozenInstant));
    }

    [Fact]
    public void RatioScaleKeepsDimensionPayloadReplayabilityIndependent()
    {
        var leftDimension = ProbeUnitDimensionVector.CreateDeclared(
            ProbeUnitDimensionVector.DefaultBasisId,
            LineageCompleteness.Exact,
            PayloadReplayability.ReplayableExact,
            ("L", 1));
        var rightDimension = ProbeUnitDimensionVector.CreateDeclared(
            ProbeUnitDimensionVector.DefaultBasisId,
            LineageCompleteness.Exact,
            PayloadReplayability.DigestOnly,
            ("T", -1));
        var left = ProbeMeasurementTransformReceipt.ExactRatioScale(
            "independent-replay-left",
            new ProbeExactRatio(3, 2),
            leftDimension,
            "derive-independent-replay-left",
            new[] { ValidEvidence("independent-replay-left-evidence") },
            FrozenInstant);
        var right = ProbeMeasurementTransformReceipt.ExactRatioScale(
            "independent-replay-right",
            new ProbeExactRatio(5, 3),
            rightDimension,
            "derive-independent-replay-right",
            new[] { ValidEvidence("independent-replay-right-evidence") },
            FrozenInstant);

        var composed = ProbeMeasurementTransformReceipt.ComposeExact(
            "independent-replay-composed",
            "derive-independent-replay-composed",
            left,
            right,
            FrozenInstant);

        Assert.Equal(ProbeBoundaryDisposition.ExactRepresentationLocal, composed.Disposition);
        Assert.Equal(LineageCompleteness.Exact, composed.Dimension.Completeness);
        Assert.Equal(PayloadReplayability.DigestOnly, composed.Dimension.PayloadReplayability);
    }

    [Fact]
    public void ProjectionCanonicalEncodingSeparatesDelimiterBearingAxesAndBasisIdentities()
    {
        var delimiterAxis = ProbeUnitDimensionVector.Create(("a^1;b", 1));
        var twoAxes = ProbeUnitDimensionVector.Create(("a", 1), ("b", 1));

        Assert.NotEqual(delimiterAxis, twoAxes);
        Assert.NotEqual(delimiterAxis.ToCanonicalString(), twoAxes.ToCanonicalString());

        var delimiterBasis = ProbeSignedPrimeCoordinates.FromRatio(
            new ProbeExactRatio(3, 2),
            basisId: "basis;payload=DigestOnly:2^1");
        var plainBasis = ProbeSignedPrimeCoordinates.FromRatio(
            new ProbeExactRatio(3, 2),
            basisId: "basis");

        Assert.NotEqual(delimiterBasis, plainBasis);
        Assert.NotEqual(delimiterBasis.ToCanonicalString(), plainBasis.ToCanonicalString());
        Assert.StartsWith(
            $"{ProbeSignedPrimeCoordinates.Contract.Name.Length}:{ProbeSignedPrimeCoordinates.Contract.Name}",
            delimiterBasis.ToCanonicalString(),
            StringComparison.Ordinal);
    }

    [Fact]
    public void RoundedCorrelatedAndExpiredRatioScaleEvidenceCannotMasqueradeAsExactLocalWork()
    {
        var rounded = ProbeMeasurementTransformReceipt.ExactRatioScale(
            "rounded",
            new ProbeExactRatio(123, 100),
            ProbeUnitDimensionVector.Dimensionless,
            "derive-rounded",
            new[]
            {
                ValidEvidence(
                    "rounded-evidence",
                    coefficientStatus: ProbeCoefficientStatus.Rounded),
            },
            FrozenInstant);
        var correlated = ProbeMeasurementTransformReceipt.ExactRatioScale(
            "correlated",
            new ProbeExactRatio(7, 5),
            ProbeUnitDimensionVector.Dimensionless,
            "derive-correlated",
            new[]
            {
                ValidEvidence(
                    "correlated-evidence",
                    uncertaintyKind: ProbeUncertaintyKind.Correlated),
            },
            FrozenInstant);
        var expired = ProbeMeasurementTransformReceipt.ExactRatioScale(
            "expired",
            new ProbeExactRatio(9, 8),
            ProbeUnitDimensionVector.Dimensionless,
            "derive-expired",
            new[]
            {
                new ProbeCalibrationEvidenceEnvelope(
                    "expired-evidence",
                    "calibration/certificate/old",
                    FrozenInstant.AddDays(-10),
                    FrozenInstant.AddDays(-1),
                    ProbeCoefficientStatus.ExactCalibratedRatio,
                    ProbeUncertaintyKind.Independent,
                    "bounded independent relative standard uncertainty",
                    ProbeEvidenceAuthentication.AuthenticatedExternally,
                    "renewal required"),
            },
            FrozenInstant);

        Assert.Equal(ProbeBoundaryDisposition.ExplicitTransformCrossing, rounded.Disposition);
        Assert.Contains("ROUNDED_COEFFICIENT", rounded.CrossingReason, StringComparison.Ordinal);
        Assert.Equal(ProbeBoundaryDisposition.ExplicitTransformCrossing, correlated.Disposition);
        Assert.Contains("CORRELATED_UNCERTAINTY", correlated.CrossingReason, StringComparison.Ordinal);
        Assert.Equal(ProbeBoundaryDisposition.Unresolved, expired.Disposition);
        Assert.Equal("EXPIRED_VALIDITY", expired.CrossingReason);
        Assert.Null(expired.NumericFactors);
        Assert.Equal(LineageCompleteness.Conflict, expired.Dimension.Completeness);
        Assert.Empty(expired.Dimension.Axes);

        Assert.Throws<InvalidOperationException>(() => rounded.ApplyExact(ProbeExactRatio.One));
        Assert.Throws<InvalidOperationException>(() =>
            ProbeMeasurementTransformReceipt.ComposeExact(
                "invalid-compose",
                "invalid-derive",
                rounded,
                correlated,
                FrozenInstant));
    }

    [Fact]
    public void UndefinedCalibrationEnumsAreRejectedAtIngress()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ProbeCalibrationEvidenceEnvelope(
                "bad-coefficient-status",
                "fixture/source",
                FrozenInstant.AddDays(-1),
                FrozenInstant.AddDays(1),
                (ProbeCoefficientStatus)999,
                ProbeUncertaintyKind.NoneDeclared,
                "",
                ProbeEvidenceAuthentication.IntegrityOnly,
                ""));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ProbeCalibrationEvidenceEnvelope(
                "bad-uncertainty-kind",
                "fixture/source",
                FrozenInstant.AddDays(-1),
                FrozenInstant.AddDays(1),
                ProbeCoefficientStatus.ExactDefined,
                (ProbeUncertaintyKind)999,
                "",
                ProbeEvidenceAuthentication.IntegrityOnly,
                ""));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ProbeCalibrationEvidenceEnvelope(
                "bad-authentication",
                "fixture/source",
                FrozenInstant.AddDays(-1),
                FrozenInstant.AddDays(1),
                ProbeCoefficientStatus.ExactDefined,
                ProbeUncertaintyKind.NoneDeclared,
                "",
                (ProbeEvidenceAuthentication)999,
                ""));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ProbeMeasurementTransformReceipt.ExplicitCrossing(
                "bad-transform-kind",
                (ProbeMeasurementTransformKind)999,
                ProbeUnitDimensionVector.Dimensionless,
                "derive-bad-transform-kind",
                new[] { ValidEvidence("bad-transform-kind-evidence") },
                FrozenInstant,
                "unsupported"));
    }

    [Theory]
    [InlineData(ProbeMeasurementTransformKind.Affine, "CELSIUS_OFFSET_REQUIRES_AFFINE_TRANSFORM")]
    [InlineData(ProbeMeasurementTransformKind.Logarithmic, "DECIBEL_REQUIRES_LOG_TRANSFORM")]
    [InlineData(ProbeMeasurementTransformKind.Nonlinear, "POLYNOMIAL_CALIBRATION_REQUIRES_EVALUATION")]
    public void NonMultiplicativeMeasurementOperationsRemainExplicitCrossings(
        ProbeMeasurementTransformKind kind,
        string reason)
    {
        var receipt = ProbeMeasurementTransformReceipt.ExplicitCrossing(
            $"crossing-{kind}",
            kind,
            ProbeUnitDimensionVector.Create(("Theta", 1)),
            $"derive-{kind}",
            new[] { ValidEvidence($"evidence-{kind}") },
            FrozenInstant,
            reason);

        Assert.Equal(ProbeBoundaryDisposition.ExplicitTransformCrossing, receipt.Disposition);
        Assert.Equal(reason, receipt.CrossingReason);
        Assert.Null(receipt.NominalCoefficient);
        Assert.Null(receipt.NumericFactors);
        Assert.Throws<InvalidOperationException>(() => receipt.ApplyExact(ProbeExactRatio.One));
    }

    [Fact]
    public void JustIntervalsComposeInvertAndExposeOctaveProjectionLoss()
    {
        var fifth = ProbeJustIntervalReceipt.FromRatio("fifth", "lineage-fifth", new ProbeExactRatio(3, 2));
        var third = ProbeJustIntervalReceipt.FromRatio("third", "lineage-third", new ProbeExactRatio(5, 4));
        var chordStep = fifth.Compose(third, "fifth-plus-third", "lineage-composed");

        Assert.Equal(new ProbeExactRatio(15, 8), chordStep.Ratio);
        Assert.Equal(-3, chordStep.Coordinates.Exponents[new BigInteger(2)]);
        Assert.Equal(1, chordStep.Coordinates.Exponents[new BigInteger(3)]);
        Assert.Equal(1, chordStep.Coordinates.Exponents[new BigInteger(5)]);
        Assert.Equal(new ProbeExactRatio(8, 15), chordStep.Invert("inverse", "lineage-inverse").Ratio);

        var ratioThree = ProbeJustIntervalReceipt.FromRatio("three", "lineage-three", new ProbeExactRatio(3, 1));
        var projectedThree = ratioThree.ProjectToOctave();
        var projectedFifth = fifth.ProjectToOctave();

        Assert.Equal(new ProbeExactRatio(3, 2), projectedThree.PitchClassRatio);
        Assert.Equal(projectedFifth.PitchClassRatio, projectedThree.PitchClassRatio);
        Assert.Equal(-1, projectedThree.AppliedPowerOfTwo);
        Assert.Equal(0, projectedFifth.AppliedPowerOfTwo);
        Assert.NotEqual(ratioThree.Ratio, fifth.Ratio);
    }

    [Fact]
    public void DeterministicWaveRenderingSeparatesExactIntervalFromApproximatePcmReadout()
    {
        var first = ProbeJustIntervalReceipt.FromRatio(
            "major-third-a",
            "supplier-lineage-a",
            new ProbeExactRatio(5, 4));
        var second = ProbeJustIntervalReceipt.FromRatio(
            "major-third-b",
            "supplier-lineage-b",
            new ProbeExactRatio(5, 4));
        var baseFrequency = new ProbeExactRatio(440, 1);
        var policy = new ProbeAudioApproximationPolicy(
            sampleRate: 8_000,
            sampleCount: 800,
            phaseRadians: 0,
            peakAmplitude: 0.25,
            linearAttackSamples: 8,
            linearReleaseSamples: 8);

        var firstRender = ProbePcmWaveRenderer.RenderSine("render-a", first, baseFrequency, policy);
        var secondRender = ProbePcmWaveRenderer.RenderSine("render-b", second, baseFrequency, policy);

        Assert.Equal(new ProbeExactRatio(550, 1), firstRender.NominalFrequencyHertz);
        Assert.Equal(550d, firstRender.RenderedFrequencyHertz);
        Assert.Equal(new ProbeExactRatio(1, 10), policy.ExactDurationSeconds);
        Assert.Equal(44 + 800 * 2, firstRender.WavByteLength);
        Assert.Equal(firstRender.WavSha256, secondRender.WavSha256);
        Assert.NotEqual(firstRender.SourceDerivationReceiptId, secondRender.SourceDerivationReceiptId);
        Assert.Equal(0, firstRender.ClippedSampleCount);
        Assert.Contains("PCM16_LE_MONO", policy.ToCanonicalString(), StringComparison.Ordinal);

        var bytes = firstRender.GetWavBytes();
        Assert.Equal("RIFF", Encoding.ASCII.GetString(bytes, 0, 4));
        Assert.Equal("WAVE", Encoding.ASCII.GetString(bytes, 8, 4));
        bytes[0] = 0;
        Assert.Equal((byte)'R', firstRender.GetWavBytes()[0]);

        var aboveNyquist = ProbeJustIntervalReceipt.FromRatio(
            "above-nyquist",
            "lineage-high",
            new ProbeExactRatio(10, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ProbePcmWaveRenderer.RenderSine("rejected", aboveNyquist, new ProbeExactRatio(500, 1), policy));
    }

    [Fact]
    public void StructuralAccumulatorLeaksMembershipAndProvidesNoCryptographicProperties()
    {
        var registry = Registry("registry-a", 4);
        var left = ProbeTransparentStructuralAccumulator.Create(registry, LeftAccumulatorMembers);
        var right = ProbeTransparentStructuralAccumulator.Create(registry, RightAccumulatorMembers);
        var reordered = ProbeTransparentStructuralAccumulator.Create(registry, ReorderedAccumulatorMembers);

        Assert.Equal(new BigInteger(6), left.StructuralProduct);
        Assert.Equal(left.StructuralProduct, reordered.StructuralProduct);
        Assert.Equal(left.IntegrityDigestSha256, reordered.IntegrityDigestSha256);
        Assert.Equal(LeftAccumulatorMembers, left.PubliclyDecodableSupport);

        var present = left.TestMembership("a");
        var absent = left.TestMembership("c");
        Assert.True(present.IsMember);
        Assert.False(absent.IsMember);
        Assert.True(present.MembershipIsPubliclyLeaked);
        Assert.Equal("PUBLIC_EXACT_DIVISIBILITY", present.Method);
        Assert.Equal(ProbeSecurityPropertyState.NotProvided, present.CryptographicMembershipProof);

        Assert.Equal(new BigInteger(30), left.Union(right).StructuralProduct);
        Assert.Equal(new BigInteger(3), left.Intersect(right).StructuralProduct);
        Assert.Equal("NOT_CRYPTOGRAPHIC", ProbeTransparentStructuralAccumulator.SecurityBoundary.CryptographicClassification);
        Assert.Equal("NO_PRIVACY", ProbeTransparentStructuralAccumulator.SecurityBoundary.PrivacyClassification);
        Assert.Equal(
            ProbeSecurityPropertyState.NotProvided,
            ProbeTransparentStructuralAccumulator.SecurityBoundary.AuthenticatedCommitment);
        Assert.Equal(
            ProbeSecurityPropertyState.NotProvided,
            ProbeTransparentStructuralAccumulator.SecurityBoundary.ZeroKnowledgeProof);
        Assert.Contains("REVEALS", ProbeTransparentStructuralAccumulator.LeakageStatement, StringComparison.Ordinal);

        var recycledEpoch = ProbeTransparentStructuralAccumulator.Create(
            Registry("registry-a", 5),
            RecycledEpochMembers);
        Assert.Throws<InvalidOperationException>(() => left.Union(recycledEpoch));
    }

    [Fact]
    public void StructuralRegistryCanonicalEncodingSeparatesDelimiterBearingElementIdentities()
    {
        var oneElement = new ProbeStructuralPrimeRegistry(
            "registry|epoch=1",
            1,
            new Dictionary<string, BigInteger>(StringComparer.Ordinal)
            {
                ["a=2;b"] = 3,
            });
        var twoElements = new ProbeStructuralPrimeRegistry(
            "registry|epoch=1",
            1,
            new Dictionary<string, BigInteger>(StringComparer.Ordinal)
            {
                ["a"] = 2,
                ["b"] = 3,
            });

        Assert.NotEqual(oneElement.ToCanonicalString(), twoElements.ToCanonicalString());
        Assert.NotEqual(oneElement.BindingSha256, twoElements.BindingSha256);
    }

    [Fact]
    public void BomProbeShowsSameValueDifferentLineageAndSharedPartOverlap()
    {
        var firstLines = new[]
        {
            new ProbeBomLine("supplier-a", "bolt", "lot-1", 4, "receipt-a1"),
            new ProbeBomLine("supplier-a", "washer", "lot-2", 6, "receipt-a2"),
        };
        var first = ProbeBomQuantityReceipt.Create("bom-a", firstLines);
        var firstReordered = ProbeBomQuantityReceipt.Create("bom-a-reordered", firstLines.Reverse());
        var second = ProbeBomQuantityReceipt.Create(
            "bom-b",
            new[]
            {
                new ProbeBomLine("supplier-b", "bolt", "lot-9", 5, "receipt-b1"),
                new ProbeBomLine("supplier-b", "nut", "lot-10", 5, "receipt-b2"),
            });

        Assert.Equal(new BigInteger(10), first.ComputedQuantity);
        Assert.Equal(first.ComputedQuantity, second.ComputedQuantity);
        Assert.Equal(first.LineageDigestSha256, firstReordered.LineageDigestSha256);
        Assert.NotEqual(first.LineageDigestSha256, second.LineageDigestSha256);
        Assert.True(first.HasSameComputedValueButDifferentLineage(second));
        Assert.Collection(first.SharedComponentKeys(second), item => Assert.Equal("bolt", item));
        Assert.Contains("TOPOLOGY_PRESERVING_RECEIPT_REQUIRED", ProbeBomQuantityReceipt.IntegrationBoundary, StringComparison.Ordinal);
    }

    [Fact]
    public void BomCanonicalEncodingSeparatesDelimiterBearingLineIdentities()
    {
        var delimiterInSupplier = ProbeBomQuantityReceipt.Create(
            "bom-delimiter-supplier",
            new[]
            {
                new ProbeBomLine("supplier:x", "bolt", "lot|1", 7, "receipt;a"),
            });
        var delimiterInComponent = ProbeBomQuantityReceipt.Create(
            "bom-delimiter-component",
            new[]
            {
                new ProbeBomLine("supplier", "x:bolt", "lot|1", 7, "receipt;a"),
            });

        Assert.Equal(delimiterInSupplier.ComputedQuantity, delimiterInComponent.ComputedQuantity);
        Assert.NotEqual(delimiterInSupplier.LineageDigestSha256, delimiterInComponent.LineageDigestSha256);
        Assert.Equal(
            "TOPOLOGY_PRESERVING_RECEIPT_REQUIRED__PERSISTENT_TYPED_DAG_TESTED",
            ProbeBomQuantityReceipt.IntegrationBoundary);
    }

    private static ProbeCalibrationEvidenceEnvelope ValidEvidence(
        string evidenceId,
        ProbeCoefficientStatus coefficientStatus = ProbeCoefficientStatus.ExactCalibratedRatio,
        ProbeUncertaintyKind uncertaintyKind = ProbeUncertaintyKind.Independent) =>
        new(
            evidenceId,
            $"calibration/certificate/{evidenceId}",
            FrozenInstant.AddDays(-1),
            FrozenInstant.AddDays(1),
            coefficientStatus,
            uncertaintyKind,
            "declared bounded uncertainty",
            ProbeEvidenceAuthentication.IntegrityOnly,
            "external authentication not inferred");

    private static ProbeStructuralPrimeRegistry Registry(string registryId, int epoch) =>
        new(
            registryId,
            epoch,
            new Dictionary<string, BigInteger>(StringComparer.Ordinal)
            {
                ["a"] = 2,
                ["b"] = 3,
                ["c"] = 5,
                ["d"] = 7,
            });
}
