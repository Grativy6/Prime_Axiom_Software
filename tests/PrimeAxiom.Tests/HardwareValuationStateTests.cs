using System.Numerics;
using PrimeAxiom.Core.Hardware;

namespace PrimeAxiom.Tests;

public sealed class HardwareValuationStateTests
{
    private static readonly int[] Widths = [4, 6, 8];
    private static readonly int[] S4 = [2, 3, 5, 7];
    private static readonly int[] Caps4 = [3, 2, 1, 1];
    private static readonly int[] Caps6 = [5, 3, 2, 2];
    private static readonly int[] Caps8 = [7, 5, 3, 2];
    private static readonly int[] NonCanonicalZero = [1, 0, 0, 0];
    private static readonly int[] OverflowingExponent = [4, 0, 0, 0];
    private static readonly int[] WrongLaneCount = [0, 0, 0];
    private static readonly int[] OneFactorOfTwo = [1, 0, 0, 0];

    [Fact]
    public void S4CapsAreComputedExactlyForEveryFrozenWidth()
    {
        Assert.Equal(S4, ValuationHardwareDomain.S4);
        Assert.Equal(Caps4, ValuationHardwareDomain.ForWidth(4).Caps);
        Assert.Equal(Caps6, ValuationHardwareDomain.ForWidth(6).Caps);
        Assert.Equal(Caps8, ValuationHardwareDomain.ForWidth(8).Caps);

        foreach (var width in Widths)
        {
            var domain = ValuationHardwareDomain.ForWidth(width);
            for (var lane = 0; lane < domain.LaneCount; lane++)
            {
                var cap = domain.CapAt(lane);
                var prime = domain.PrimeAt(lane);
                Assert.True(PrimePower(prime, cap) <= domain.MaximumMagnitude);
                Assert.True(PrimePower(prime, cap + 1) > domain.MaximumMagnitude);
                Assert.Equal(cap, ValuationHardwareDomain.ComputeCap(width, prime));
            }
        }

        Assert.Throws<ArgumentOutOfRangeException>(() => ValuationHardwareDomain.ForWidth(5));
        Assert.Throws<ArgumentOutOfRangeException>(() => ValuationHardwareDomain.ComputeCap(4, 11));
    }

    [Fact]
    public void BinaryAndThermometerEncodingsRoundTripEveryLegalState()
    {
        foreach (var width in Widths)
        {
            var domain = ValuationHardwareDomain.ForWidth(width);
            foreach (var vector in ExponentVectors(domain))
            {
                var created = ValuationHardwareState.Create(width, isZero: false, vector);
                Assert.True(created.Succeeded, created.Detail);
                var binary = Assert.IsType<ValuationHardwareState>(created.Value);
                Assert.True(binary.IsCanonical);
                Assert.True(binary.IsExact);
                Assert.Equal(vector, binary.Exponents);

                var thermometer = binary.ToThermometer();
                Assert.True(thermometer.IsCanonical);
                Assert.True(thermometer.IsExact);
                for (var lane = 0; lane < domain.LaneCount; lane++)
                {
                    for (var threshold = 1; threshold <= domain.CapAt(lane); threshold++)
                    {
                        Assert.Equal(vector[lane] >= threshold, thermometer.ThresholdAt(lane, threshold));
                    }
                }

                var roundTrip = thermometer.ToExponentState();
                Assert.Equal(vector, roundTrip.Exponents);
                Assert.Equal(binary.IsZero, roundTrip.IsZero);

                var expectedMagnitude = ReconstructOracle(domain, vector);
                var reconstructed = binary.Reconstruct();
                if (expectedMagnitude <= domain.MaximumMagnitude)
                {
                    Assert.True(reconstructed.Succeeded, reconstructed.Detail);
                    Assert.Equal(expectedMagnitude, reconstructed.Value!.Value);
                    Assert.Equal(expectedMagnitude, thermometer.Reconstruct().Value!.Value);
                }
                else
                {
                    Assert.False(reconstructed.Succeeded);
                    Assert.Equal(ValuationStateFailure.MagnitudeOverflow, reconstructed.Failure);
                    Assert.Null(reconstructed.Value);
                    Assert.Equal(
                        ValuationStateFailure.MagnitudeOverflow,
                        thermometer.Reconstruct().Failure);
                }
            }

            var zero = ValuationHardwareState.Zero(width);
            Assert.True(zero.IsZero);
            Assert.True(zero.IsCanonical);
            Assert.All(zero.Exponents, exponent => Assert.Equal(0, exponent));
            Assert.Equal(BigInteger.Zero, zero.Reconstruct().Value!.Value);
            foreach (var prime in ValuationHardwareDomain.S4)
            {
                var valuation = zero.Valuation(prime).Value!;
                Assert.True(valuation.IsPositiveInfinity);
                Assert.True(valuation.IsExact);
            }

            var zeroThermometer = zero.ToThermometer();
            Assert.True(zeroThermometer.IsZero);
            for (var lane = 0; lane < domain.LaneCount; lane++)
            {
                Assert.All(zeroThermometer.ThresholdsAt(lane), bit => Assert.False(bit));
            }
        }
    }

    [Fact]
    public void MalformedBinaryAndThermometerStatesAreRejected()
    {
        var nonCanonicalZero = ValuationHardwareState.Create(4, true, NonCanonicalZero);
        Assert.False(nonCanonicalZero.Succeeded);
        Assert.Equal(ValuationStateFailure.NonCanonicalEncoding, nonCanonicalZero.Failure);
        Assert.Null(nonCanonicalZero.Value);

        var exponentOverflow = ValuationHardwareState.Create(4, false, OverflowingExponent);
        Assert.False(exponentOverflow.Succeeded);
        Assert.Equal(ValuationStateFailure.InvalidExponent, exponentOverflow.Failure);

        var wrongLaneCount = ValuationHardwareState.Create(4, false, WrongLaneCount);
        Assert.False(wrongLaneCount.Succeeded);
        Assert.Equal(ValuationStateFailure.NonCanonicalEncoding, wrongLaneCount.Failure);

        IReadOnlyList<IReadOnlyList<bool>> malformed =
        [
            new[] { true, false, true },
            new[] { false, false },
            new[] { false },
            new[] { false },
        ];
        var malformedThermometer = ValuationThermometerState.Create(4, false, malformed);
        Assert.False(malformedThermometer.Succeeded);
        Assert.Equal(ValuationStateFailure.NonCanonicalEncoding, malformedThermometer.Failure);
        Assert.Null(malformedThermometer.Value);

        IReadOnlyList<IReadOnlyList<bool>> nonCanonicalZeroThresholds =
        [
            new[] { true, false, false },
            new[] { false, false },
            new[] { false },
            new[] { false },
        ];
        var malformedZero = ValuationThermometerState.Create(4, true, nonCanonicalZeroThresholds);
        Assert.False(malformedZero.Succeeded);
        Assert.Equal(ValuationStateFailure.NonCanonicalEncoding, malformedZero.Failure);
    }

    [Fact]
    public void StructuralOperationsMatchLaneOraclesAcrossEveryLegalPair()
    {
        foreach (var width in Widths)
        {
            var domain = ValuationHardwareDomain.ForWidth(width);
            var states = StructuralStates(domain).ToArray();
            foreach (var left in states)
            {
                foreach (var right in states)
                {
                    CheckCompose(domain, left, right);
                    CheckCancel(left, right);
                    CheckMeet(left, right);
                    CheckJoin(left, right);
                    CheckDivides(left, right);
                }
            }
        }
    }

    [Fact]
    public void PowerSaturationAndAtomicRejectionAreExplicit()
    {
        foreach (var width in Widths)
        {
            var domain = ValuationHardwareDomain.ForWidth(width);
            for (var lane = 0; lane < domain.LaneCount; lane++)
            {
                var prime = domain.PrimeAt(lane);
                for (var exponent = 0; exponent <= domain.CapAt(lane); exponent++)
                {
                    var power = ValuationHardwareState.Power(width, prime, exponent);
                    Assert.True(power.Succeeded, power.Detail);
                    Assert.Equal(exponent, power.Value!.ExponentAt(lane));
                    Assert.All(
                        Enumerable.Range(0, domain.LaneCount).Where(other => other != lane),
                        other => Assert.Equal(0, power.Value.ExponentAt(other)));

                    var thermometer = ValuationThermometerState.Power(width, prime, exponent);
                    Assert.True(thermometer.Succeeded, thermometer.Detail);
                    Assert.Equal(exponent, thermometer.Value!.ToExponentState().ExponentAt(lane));
                }

                var rejectedPower = ValuationHardwareState.Power(width, prime, domain.CapAt(lane) + 1);
                Assert.False(rejectedPower.Succeeded);
                Assert.Equal(ValuationStateFailure.InvalidExponent, rejectedPower.Failure);
            }

            var capVector = domain.Caps.ToArray();
            var capState = ExactState(width, capVector);
            var identityFactor = ExactState(width, OneFactorOfTwo);
            var saturated = capState.Compose(identityFactor);
            Assert.True(saturated.Succeeded);
            Assert.False(saturated.Value!.IsExact);
            Assert.True(saturated.Value.IsLaneSaturated(0));
            Assert.Equal(domain.CapAt(0), saturated.Value.ExponentAt(0));
            Assert.Equal(ValuationStateFailure.SaturatedInput, saturated.Value.Reconstruct().Failure);
            Assert.Equal(ValuationStateFailure.SaturatedInput, saturated.Value.Cancel(identityFactor).Failure);

            var dividend = ValuationHardwareState.Identity(width);
            var divisor = identityFactor;
            var before = dividend.Exponents.ToArray();
            var rejectedCancel = dividend.Cancel(divisor);
            Assert.False(rejectedCancel.Succeeded);
            Assert.Equal(ValuationStateFailure.CancellationUnderflow, rejectedCancel.Failure);
            Assert.Null(rejectedCancel.Value);
            Assert.Equal(before, dividend.Exponents);

            var zeroDivisor = dividend.Cancel(ValuationHardwareState.Zero(width));
            Assert.False(zeroDivisor.Succeeded);
            Assert.Equal(ValuationStateFailure.DivisionByZero, zeroDivisor.Failure);
            Assert.Null(zeroDivisor.Value);
        }

        Assert.Equal(
            ValuationStateFailure.InvalidPrime,
            ValuationHardwareState.Power(4, 11, 1).Failure);
        Assert.Equal(
            ValuationStateFailure.InvalidExponent,
            ValuationHardwareState.Power(4, 2, -1).Failure);
    }

    [Fact]
    public void ExactSidecarEncodeAndQueriesMatchEveryMagnitude()
    {
        foreach (var width in Widths)
        {
            var domain = ValuationHardwareDomain.ForWidth(width);
            for (var magnitude = 0; magnitude <= domain.MaximumMagnitude; magnitude++)
            {
                var encoded = BinaryValuationSidecar.Encode(width, magnitude);
                Assert.True(encoded.Succeeded, encoded.Detail);
                var sidecar = Assert.IsType<BinaryValuationSidecar>(encoded.Value);
                Assert.Equal(magnitude, sidecar.Magnitude);
                Assert.True(sidecar.Valid);

                for (var lane = 0; lane < domain.LaneCount; lane++)
                {
                    var prime = domain.PrimeAt(lane);
                    var expectedValuation = magnitude == 0 ? 0 : IntegerValuation(magnitude, prime);
                    var valuation = sidecar.Valuation(prime);
                    Assert.True(valuation.Succeeded, valuation.Detail);
                    Assert.Equal(magnitude == 0, valuation.Value!.IsPositiveInfinity);
                    Assert.True(valuation.Value.IsExact);
                    if (magnitude != 0)
                    {
                        Assert.Equal(expectedValuation, valuation.Value.LowerBound);
                        Assert.Equal(expectedValuation, sidecar.LowerBoundAtPrime(prime));
                    }

                    for (var exponent = 0; exponent <= domain.CapAt(lane); exponent++)
                    {
                        var query = sidecar.IsDivisibleByPrimePower(prime, exponent);
                        Assert.True(query.Succeeded, query.Detail);
                        Assert.True(query.Value!.IsKnown);
                        var expected = exponent == 0 ||
                                       magnitude == 0 ||
                                       magnitude % PrimePower(prime, exponent) == 0;
                        Assert.Equal(expected, query.Value.Value);
                        if (exponent > 0)
                        {
                            Assert.Equal(expected, sidecar.ThresholdAt(prime, exponent));
                        }
                    }
                }
            }

            var negative = BinaryValuationSidecar.Encode(width, -1);
            Assert.False(negative.Succeeded);
            Assert.Equal(ValuationStateFailure.MagnitudeOutOfRange, negative.Failure);
            Assert.Null(negative.Value);

            var tooWide = BinaryValuationSidecar.Encode(width, domain.MaximumMagnitude + 1);
            Assert.False(tooWide.Succeeded);
            Assert.Equal(ValuationStateFailure.MagnitudeOutOfRange, tooWide.Failure);
            Assert.Null(tooWide.Value);
        }
    }

    [Fact]
    public void SidecarAdditionExhaustivelyPreservesLowerBoundsAndValidityDiscipline()
    {
        foreach (var width in Widths)
        {
            var domain = ValuationHardwareDomain.ForWidth(width);
            var encoded = Enumerable.Range(0, domain.MaximumMagnitude + 1)
                .Select(magnitude => BinaryValuationSidecar.Encode(width, magnitude).Value!)
                .ToArray();

            foreach (var left in encoded)
            {
                foreach (var right in encoded)
                {
                    var leftSnapshot = Snapshot(left);
                    var rightSnapshot = Snapshot(right);
                    var sum = left.Add(right);
                    if (left.Magnitude + right.Magnitude > domain.MaximumMagnitude)
                    {
                        Assert.False(sum.Succeeded);
                        Assert.Equal(ValuationStateFailure.MagnitudeOverflow, sum.Failure);
                        Assert.Null(sum.Value);
                        Assert.Equal(leftSnapshot, Snapshot(left));
                        Assert.Equal(rightSnapshot, Snapshot(right));
                        continue;
                    }

                    Assert.True(sum.Succeeded, sum.Detail);
                    var result = Assert.IsType<BinaryValuationSidecar>(sum.Value);
                    var expectedMagnitude = left.Magnitude + right.Magnitude;
                    Assert.Equal(expectedMagnitude, result.Magnitude);

                    if (left.IsZero)
                    {
                        Assert.Equal(right.Valid, result.Valid);
                    }
                    else if (right.IsZero)
                    {
                        Assert.Equal(left.Valid, result.Valid);
                    }
                    else
                    {
                        var allUnequal = ValuationHardwareDomain.S4.All(prime =>
                            IntegerValuation(left.Magnitude, prime) !=
                            IntegerValuation(right.Magnitude, prime));
                        Assert.Equal(allUnequal, result.Valid);
                    }

                    foreach (var prime in ValuationHardwareDomain.S4)
                    {
                        var valuation = result.Valuation(prime).Value!;
                        if (expectedMagnitude == 0)
                        {
                            Assert.True(valuation.IsPositiveInfinity);
                        }
                        else
                        {
                            var expectedLowerBound = left.IsZero
                                ? IntegerValuation(right.Magnitude, prime)
                                : right.IsZero
                                    ? IntegerValuation(left.Magnitude, prime)
                                    : Math.Min(
                                        IntegerValuation(left.Magnitude, prime),
                                        IntegerValuation(right.Magnitude, prime));
                            Assert.Equal(expectedLowerBound, valuation.LowerBound);
                            Assert.Equal(result.Valid, valuation.IsExact);
                        }
                    }

                    var refreshed = left.Add(right, refreshExact: true);
                    Assert.True(refreshed.Succeeded, refreshed.Detail);
                    Assert.True(refreshed.Value!.Valid);
                    Assert.Equal(expectedMagnitude, refreshed.Value.Magnitude);
                    foreach (var prime in ValuationHardwareDomain.S4)
                    {
                        var valuation = refreshed.Value.Valuation(prime).Value!;
                        if (expectedMagnitude == 0)
                        {
                            Assert.True(valuation.IsPositiveInfinity);
                        }
                        else
                        {
                            Assert.Equal(IntegerValuation(expectedMagnitude, prime), valuation.LowerBound);
                            Assert.True(valuation.IsExact);
                        }
                    }
                }
            }
        }
    }

    [Fact]
    public void KnownFactorScaleAndCancelAreExactAtomicAndKeepUnsupportedCofactors()
    {
        foreach (var width in Widths)
        {
            var domain = ValuationHardwareDomain.ForWidth(width);
            for (var magnitude = 0; magnitude <= domain.MaximumMagnitude; magnitude++)
            {
                var source = BinaryValuationSidecar.Encode(width, magnitude).Value!;
                foreach (var prime in ValuationHardwareDomain.S4)
                {
                    var lane = domain.IndexOfPrime(prime);
                    for (var exponent = 0; exponent <= domain.CapAt(lane); exponent++)
                    {
                        var factor = PrimePower(prime, exponent);
                        var snapshot = Snapshot(source);
                        var scaled = source.ScaleKnownFactor(prime, exponent);
                        if (magnitude != 0 && magnitude > domain.MaximumMagnitude / factor)
                        {
                            Assert.False(scaled.Succeeded);
                            Assert.Equal(ValuationStateFailure.MagnitudeOverflow, scaled.Failure);
                            Assert.Null(scaled.Value);
                            Assert.Equal(snapshot, Snapshot(source));
                        }
                        else
                        {
                            Assert.True(scaled.Succeeded, scaled.Detail);
                            Assert.Equal(magnitude * factor, scaled.Value!.Magnitude);
                            Assert.True(scaled.Value.Valid);
                        }

                        var cancelled = source.CancelKnownFactor(prime, exponent);
                        if (magnitude == 0 || magnitude % factor == 0)
                        {
                            Assert.True(cancelled.Succeeded, cancelled.Detail);
                            Assert.Equal(magnitude == 0 ? 0 : magnitude / factor, cancelled.Value!.Magnitude);
                            Assert.True(cancelled.Value.Valid);
                        }
                        else
                        {
                            Assert.False(cancelled.Succeeded);
                            Assert.Equal(ValuationStateFailure.NotDivisible, cancelled.Failure);
                            Assert.Null(cancelled.Value);
                            Assert.Equal(snapshot, Snapshot(source));
                        }
                    }
                }
            }

            foreach (var unsupported in UnsupportedMagnitudes(domain.MaximumMagnitude))
            {
                var exact = BinaryValuationSidecar.Encode(width, unsupported).Value!;
                Assert.Equal(unsupported, exact.Magnitude);
                Assert.True(exact.Valid);
                Assert.True(RemoveCatalogFactors(unsupported) > 1);

                if (unsupported <= domain.MaximumMagnitude / 2)
                {
                    var scaled = exact.ScaleKnownFactor(2).Value!;
                    Assert.Equal(unsupported * 2, scaled.Magnitude);
                    var cancelled = scaled.CancelKnownFactor(2).Value!;
                    Assert.Equal(unsupported, cancelled.Magnitude);
                    Assert.True(cancelled.Valid);
                    Assert.Equal(
                        RemoveCatalogFactors(unsupported),
                        RemoveCatalogFactors(cancelled.Magnitude));
                }
            }
        }

        var three = BinaryValuationSidecar.Encode(8, 3).Value!;
        var five = BinaryValuationSidecar.Encode(8, 5).Value!;
        var stale = three.Add(five).Value!;
        Assert.False(stale.Valid);
        Assert.Equal(0, stale.Valuation(2).Value!.LowerBound);
        Assert.False(stale.Valuation(2).Value!.IsExact);
        Assert.False(stale.IsDivisibleByPrimePower(2, 1).Value!.IsKnown);

        var staleScaled = stale.ScaleKnownFactor(2).Value!;
        Assert.Equal(16, staleScaled.Magnitude);
        Assert.False(staleScaled.Valid);
        Assert.Equal(1, staleScaled.Valuation(2).Value!.LowerBound);
        Assert.True(staleScaled.IsDivisibleByPrimePower(2, 1).Value!.Value);
        Assert.False(staleScaled.IsDivisibleByPrimePower(2, 2).Value!.IsKnown);

        var staleCancelled = staleScaled.CancelKnownFactor(2).Value!;
        Assert.Equal(8, staleCancelled.Magnitude);
        Assert.False(staleCancelled.Valid);
        Assert.Equal(0, staleCancelled.Valuation(2).Value!.LowerBound);

        var refreshed = staleCancelled.Refresh().Value!;
        Assert.True(refreshed.Valid);
        Assert.Equal(3, refreshed.Valuation(2).Value!.LowerBound);
        Assert.True(refreshed.IsDivisibleByPrimePower(2, 3).Value!.Value);
    }

    private static void CheckCompose(
        ValuationHardwareDomain domain,
        ValuationHardwareState left,
        ValuationHardwareState right)
    {
        var result = left.Compose(right);
        Assert.True(result.Succeeded, result.Detail);
        var value = result.Value!;
        if (left.IsZero || right.IsZero)
        {
            Assert.True(value.IsZero);
            Assert.True(value.IsExact);
        }
        else
        {
            for (var lane = 0; lane < domain.LaneCount; lane++)
            {
                var sum = left.ExponentAt(lane) + right.ExponentAt(lane);
                Assert.Equal(Math.Min(sum, domain.CapAt(lane)), value.ExponentAt(lane));
                Assert.Equal(sum > domain.CapAt(lane), value.IsLaneSaturated(lane));
            }
        }

        var thermometer = left.ToThermometer().Compose(right.ToThermometer());
        Assert.True(thermometer.Succeeded, thermometer.Detail);
        AssertEquivalent(value, thermometer.Value!.ToExponentState());
    }

    private static void CheckCancel(ValuationHardwareState dividend, ValuationHardwareState divisor)
    {
        var result = dividend.Cancel(divisor);
        var thermometer = dividend.ToThermometer().Cancel(divisor.ToThermometer());
        if (divisor.IsZero)
        {
            Assert.Equal(ValuationStateFailure.DivisionByZero, result.Failure);
            Assert.Equal(result.Failure, thermometer.Failure);
            Assert.Null(result.Value);
            return;
        }

        if (dividend.IsZero)
        {
            Assert.True(result.Succeeded, result.Detail);
            Assert.True(result.Value!.IsZero);
            AssertEquivalent(result.Value, thermometer.Value!.ToExponentState());
            return;
        }

        var underflows = Enumerable.Range(0, dividend.Domain.LaneCount)
            .Any(lane => dividend.ExponentAt(lane) < divisor.ExponentAt(lane));
        if (underflows)
        {
            Assert.Equal(ValuationStateFailure.CancellationUnderflow, result.Failure);
            Assert.Equal(result.Failure, thermometer.Failure);
            Assert.Null(result.Value);
            Assert.Null(thermometer.Value);
            return;
        }

        Assert.True(result.Succeeded, result.Detail);
        for (var lane = 0; lane < dividend.Domain.LaneCount; lane++)
        {
            Assert.Equal(
                dividend.ExponentAt(lane) - divisor.ExponentAt(lane),
                result.Value!.ExponentAt(lane));
        }

        AssertEquivalent(result.Value!, thermometer.Value!.ToExponentState());
    }

    private static void CheckMeet(ValuationHardwareState left, ValuationHardwareState right)
    {
        var result = left.Meet(right);
        var thermometer = left.ToThermometer().Meet(right.ToThermometer());
        Assert.True(result.Succeeded, result.Detail);
        Assert.True(thermometer.Succeeded, thermometer.Detail);
        if (left.IsZero)
        {
            AssertEquivalent(right, result.Value!);
        }
        else if (right.IsZero)
        {
            AssertEquivalent(left, result.Value!);
        }
        else
        {
            for (var lane = 0; lane < left.Domain.LaneCount; lane++)
            {
                Assert.Equal(
                    Math.Min(left.ExponentAt(lane), right.ExponentAt(lane)),
                    result.Value!.ExponentAt(lane));
            }
        }

        AssertEquivalent(result.Value!, thermometer.Value!.ToExponentState());
    }

    private static void CheckJoin(ValuationHardwareState left, ValuationHardwareState right)
    {
        var result = left.Join(right);
        var thermometer = left.ToThermometer().Join(right.ToThermometer());
        Assert.True(result.Succeeded, result.Detail);
        Assert.True(thermometer.Succeeded, thermometer.Detail);
        if (left.IsZero || right.IsZero)
        {
            Assert.True(result.Value!.IsZero);
        }
        else
        {
            for (var lane = 0; lane < left.Domain.LaneCount; lane++)
            {
                Assert.Equal(
                    Math.Max(left.ExponentAt(lane), right.ExponentAt(lane)),
                    result.Value!.ExponentAt(lane));
            }
        }

        AssertEquivalent(result.Value!, thermometer.Value!.ToExponentState());
    }

    private static void CheckDivides(ValuationHardwareState divisor, ValuationHardwareState dividend)
    {
        var result = divisor.Divides(dividend);
        var thermometer = divisor.ToThermometer().Divides(dividend.ToThermometer());
        Assert.True(result.Succeeded, result.Detail);
        Assert.True(thermometer.Succeeded, thermometer.Detail);
        var expected = divisor.IsZero
            ? dividend.IsZero
            : dividend.IsZero || Enumerable.Range(0, divisor.Domain.LaneCount)
                .All(lane => divisor.ExponentAt(lane) <= dividend.ExponentAt(lane));
        Assert.Equal(expected, result.Value!.Value);
        Assert.Equal(expected, thermometer.Value!.Value);
    }

    private static IEnumerable<ValuationHardwareState> StructuralStates(ValuationHardwareDomain domain)
    {
        yield return ValuationHardwareState.Zero(domain.Width);
        foreach (var vector in ExponentVectors(domain))
        {
            yield return ExactState(domain.Width, vector);
        }
    }

    private static IEnumerable<int[]> ExponentVectors(ValuationHardwareDomain domain)
    {
        for (var e2 = 0; e2 <= domain.CapAt(0); e2++)
        {
            for (var e3 = 0; e3 <= domain.CapAt(1); e3++)
            {
                for (var e5 = 0; e5 <= domain.CapAt(2); e5++)
                {
                    for (var e7 = 0; e7 <= domain.CapAt(3); e7++)
                    {
                        yield return [e2, e3, e5, e7];
                    }
                }
            }
        }
    }

    private static ValuationHardwareState ExactState(int width, IReadOnlyList<int> exponents)
    {
        var result = ValuationHardwareState.Create(width, false, exponents);
        Assert.True(result.Succeeded, result.Detail);
        return result.Value!;
    }

    private static void AssertEquivalent(ValuationHardwareState expected, ValuationHardwareState actual)
    {
        Assert.Equal(expected.Width, actual.Width);
        Assert.Equal(expected.IsZero, actual.IsZero);
        Assert.Equal(expected.Exponents, actual.Exponents);
        Assert.Equal(expected.SaturatedLanes, actual.SaturatedLanes);
    }

    private static int IntegerValuation(int magnitude, int prime)
    {
        Assert.True(magnitude > 0);
        var exponent = 0;
        var residual = magnitude;
        while (residual % prime == 0)
        {
            exponent++;
            residual /= prime;
        }

        return exponent;
    }

    private static int PrimePower(int prime, int exponent)
    {
        var result = 1;
        for (var index = 0; index < exponent; index++)
        {
            result *= prime;
        }

        return result;
    }

    private static BigInteger ReconstructOracle(
        ValuationHardwareDomain domain,
        IReadOnlyList<int> exponents)
    {
        var result = BigInteger.One;
        for (var lane = 0; lane < domain.LaneCount; lane++)
        {
            result *= BigInteger.Pow(domain.PrimeAt(lane), exponents[lane]);
        }

        return result;
    }

    private static int RemoveCatalogFactors(int magnitude)
    {
        var residual = magnitude;
        foreach (var prime in ValuationHardwareDomain.S4)
        {
            while (residual % prime == 0)
            {
                residual /= prime;
            }
        }

        return residual;
    }

    private static IEnumerable<int> UnsupportedMagnitudes(int maximum) =>
        Enumerable.Range(2, maximum - 1)
            .Where(value => RemoveCatalogFactors(value) > 1);

    private static string Snapshot(BinaryValuationSidecar sidecar)
    {
        var thresholds = string.Join(
            '|',
            ValuationHardwareDomain.S4.Select(prime =>
                string.Concat(
                    Enumerable.Range(1, sidecar.Domain.CapAt(sidecar.Domain.IndexOfPrime(prime)))
                        .Select(exponent => sidecar.ThresholdAt(prime, exponent) ? '1' : '0'))));
        return $"{sidecar.Width}:{sidecar.Magnitude}:{sidecar.Valid}:{thresholds}";
    }
}
