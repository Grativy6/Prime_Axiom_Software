using System.Globalization;
using System.Numerics;
using PrimeAxiom.Core.Circuits;
using PrimeAxiom.Core.Substrate;

namespace PrimeAxiom.Core.Representations;

public enum CoordinateFailure
{
    None,
    ZeroOutsidePositiveDomain,
    NegativeOutsidePositiveDomain,
    BasisEscape,
    ExponentOverflow,
    NotDivisible,
    BasisMismatch,
    ExponentWidthMismatch,
}

public readonly record struct CoordinateCost(
    GateCost Gates,
    long TrialRemainders = 0,
    long FactorDivisions = 0,
    long ReconstructionMultiplications = 0,
    long MagnitudeAdditions = 0,
    long LaneReads = 0,
    long LaneWrites = 0)
{
    public static CoordinateCost Zero => new(GateCost.Zero);
}

public sealed record CoordinateReceipt(
    string Operation,
    bool Succeeded,
    CoordinateFailure Failure,
    CoordinateCost Cost,
    bool UsedMagnitudeDomain,
    string Scope,
    BigInteger? UnrepresentedResidual = null);

public sealed record CoordinateResult(
    PrimeCoordinates? Value,
    CoordinateReceipt Receipt);

public sealed record EncodingResult(
    PrimeCoordinates? Value,
    BigInteger UnrepresentedResidual,
    CoordinateReceipt Receipt);

public sealed record MagnitudeResult(
    BigInteger Value,
    CoordinateReceipt Receipt);

/// <summary>
/// Dense, fixed-basis exponent lanes for positive integers. The runtime payload
/// contains only exponent words; the basis labels are external configuration.
/// </summary>
public sealed class PrimeCoordinates : IEquatable<PrimeCoordinates>
{
    private readonly BinaryWord[] _exponents;

    public PrimeCoordinates(PrimeBasis basis, IEnumerable<BinaryWord> exponents)
    {
        Basis = basis ?? throw new ArgumentNullException(nameof(basis));
        _exponents = exponents.Select(word => new BinaryWord(word.CopyBits())).ToArray();
        if (_exponents.Length != basis.Count)
        {
            throw new ArgumentException("There must be one exponent lane per configured prime.", nameof(exponents));
        }

        if (_exponents.Select(word => word.Width).Distinct().Count() != 1)
        {
            throw new ArgumentException("Every exponent lane must have the same width.", nameof(exponents));
        }
    }

    public PrimeBasis Basis { get; }

    public int LaneCount => _exponents.Length;

    public int ExponentWidth => _exponents[0].Width;

    public long DensePayloadBits => checked((long)LaneCount * ExponentWidth);

    public BinaryWord ExponentAt(int lane) => new(_exponents[lane].CopyBits());

    public static PrimeCoordinates Identity(PrimeBasis basis, int exponentWidth) =>
        new(basis, Enumerable.Range(0, basis.Count).Select(_ => BinaryWord.Zero(exponentWidth)));

    public static EncodingResult Encode(BigInteger magnitude, PrimeBasis basis, int exponentWidth)
    {
        ArgumentNullException.ThrowIfNull(basis);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(exponentWidth);

        if (magnitude.IsZero)
        {
            return EncodingFailure(
                CoordinateFailure.ZeroOutsidePositiveDomain,
                magnitude,
                basis,
                exponentWidth,
                0,
                0,
                0);
        }

        if (magnitude.Sign < 0)
        {
            return EncodingFailure(
                CoordinateFailure.NegativeOutsidePositiveDomain,
                magnitude,
                basis,
                exponentWidth,
                0,
                0,
                0);
        }

        var remainder = magnitude;
        var lanes = new List<BinaryWord>(basis.Count);
        long remainderChecks = 0;
        long divisions = 0;
        var maximumExponent = (BigInteger.One << exponentWidth) - BigInteger.One;

        for (var lane = 0; lane < basis.Count; lane++)
        {
            var prime = new BigInteger(basis[lane]);
            var exponent = BigInteger.Zero;
            while (true)
            {
                remainderChecks++;
                if (remainder % prime != BigInteger.Zero)
                {
                    break;
                }

                remainder /= prime;
                exponent++;
                divisions++;
                if (exponent > maximumExponent)
                {
                    return EncodingFailure(
                        CoordinateFailure.ExponentOverflow,
                        remainder,
                        basis,
                        exponentWidth,
                        remainderChecks,
                        divisions,
                        lanes.Count);
                }
            }

            lanes.Add(BinaryWord.FromUnsigned(exponent, exponentWidth));
        }

        if (remainder != BigInteger.One)
        {
            return EncodingFailure(
                CoordinateFailure.BasisEscape,
                remainder,
                basis,
                exponentWidth,
                remainderChecks,
                divisions,
                basis.Count);
        }

        var value = new PrimeCoordinates(basis, lanes);
        return new EncodingResult(
            value,
            BigInteger.One,
            new CoordinateReceipt(
                "ENCODE_BY_FACTORIZATION",
                true,
                CoordinateFailure.None,
                new CoordinateCost(
                    GateCost.Zero,
                    TrialRemainders: remainderChecks,
                    FactorDivisions: divisions,
                    LaneWrites: basis.Count),
                UsedMagnitudeDomain: true,
                Scope: "Positive magnitudes fully generated by the configured basis"));
    }

    public CoordinateResult Compose(PrimeCoordinates other)
    {
        if (!TryCompatible(other, out var failure))
        {
            return Failed("COMPOSE", failure);
        }

        var lanes = new BinaryWord[LaneCount];
        var costs = new GateCost[LaneCount];
        var carryStates = new BitState[LaneCount];
        for (var lane = 0; lane < LaneCount; lane++)
        {
            var result = BinaryCircuit.Add(_exponents[lane], other._exponents[lane]);
            lanes[lane] = result.Value;
            costs[lane] = result.Cost;
            carryStates[lane] = result.Carry;
        }

        var laneCost = GateCost.Parallel(costs);
        var overflow = AggregateStates(carryStates, laneCost.CriticalPathDepth, useOr: true);
        var cost = new CoordinateCost(
            new GateCost(
                laneCost.NandEvaluations + overflow.Cost.NandEvaluations,
                overflow.Cost.CriticalPathDepth),
            LaneReads: LaneCount * 2L,
            LaneWrites: LaneCount);
        if (overflow.State == BitState.On)
        {
            return new CoordinateResult(
                null,
                new CoordinateReceipt(
                    "COMPOSE",
                    false,
                    CoordinateFailure.ExponentOverflow,
                    cost,
                    UsedMagnitudeDomain: false,
                    Scope: "Fixed-basis lane-wise exponent addition"));
        }

        return Succeeded("COMPOSE", new PrimeCoordinates(Basis, lanes), cost, false);
    }

    public CoordinateResult Cancel(PrimeCoordinates divisor)
    {
        if (!TryCompatible(divisor, out var failure))
        {
            return Failed("CANCEL", failure);
        }

        var lanes = new BinaryWord[LaneCount];
        var costs = new GateCost[LaneCount];
        var borrowStates = new BitState[LaneCount];
        for (var lane = 0; lane < LaneCount; lane++)
        {
            var result = BinaryCircuit.Subtract(_exponents[lane], divisor._exponents[lane]);
            lanes[lane] = result.Value;
            costs[lane] = result.Cost;
            borrowStates[lane] = result.Borrow;
        }

        var laneCost = GateCost.Parallel(costs);
        var underflow = AggregateStates(borrowStates, laneCost.CriticalPathDepth, useOr: true);
        var cost = new CoordinateCost(
            new GateCost(
                laneCost.NandEvaluations + underflow.Cost.NandEvaluations,
                underflow.Cost.CriticalPathDepth),
            LaneReads: LaneCount * 2L,
            LaneWrites: LaneCount);
        if (underflow.State == BitState.On)
        {
            return new CoordinateResult(
                null,
                new CoordinateReceipt(
                    "CANCEL",
                    false,
                    CoordinateFailure.NotDivisible,
                    cost,
                    UsedMagnitudeDomain: false,
                    Scope: "Exact coordinate subtraction only"));
        }

        return Succeeded("CANCEL", new PrimeCoordinates(Basis, lanes), cost, false);
    }

    public CoordinateResult GreatestCommonDivisor(PrimeCoordinates other) =>
        CoordinateExtremum(other, minimum: true, "MEET_GCD");

    public CoordinateResult LeastCommonMultiple(PrimeCoordinates other) =>
        CoordinateExtremum(other, minimum: false, "JOIN_LCM");

    public (bool Divides, CoordinateReceipt Receipt) Divides(PrimeCoordinates other)
    {
        if (!TryCompatible(other, out var failure))
        {
            var failed = Failed("DIVIDES", failure).Receipt;
            return (false, failed);
        }

        var comparisons = new GateCost[LaneCount];
        var greaterStates = new BitState[LaneCount];
        for (var lane = 0; lane < LaneCount; lane++)
        {
            var comparison = BinaryCircuit.Compare(_exponents[lane], other._exponents[lane]);
            comparisons[lane] = comparison.Cost;
            greaterStates[lane] = comparison.Greater;
        }

        var comparisonCost = GateCost.Parallel(comparisons);
        var noLaneGreater = AggregateStates(
            greaterStates,
            comparisonCost.CriticalPathDepth,
            useOr: true,
            negateResult: true);
        var total = new CoordinateCost(
            new GateCost(
                comparisonCost.NandEvaluations + noLaneGreater.Cost.NandEvaluations,
                noLaneGreater.Cost.CriticalPathDepth),
            LaneReads: LaneCount * 2L);
        return (
            noLaneGreater.State == BitState.On,
            new CoordinateReceipt(
                "DIVIDES",
                true,
                CoordinateFailure.None,
                total,
                UsedMagnitudeDomain: false,
                Scope: "Coordinate-wise exponent order"));
    }

    public MagnitudeResult Reconstruct()
    {
        var magnitude = BigInteger.One;
        long multiplications = 0;
        for (var lane = 0; lane < LaneCount; lane++)
        {
            var exponent = _exponents[lane].ToUnsigned();
            for (var count = BigInteger.Zero; count < exponent; count++)
            {
                magnitude *= Basis[lane];
                multiplications++;
            }
        }

        return new MagnitudeResult(
            magnitude,
            new CoordinateReceipt(
                "RECONSTRUCT",
                true,
                CoordinateFailure.None,
                new CoordinateCost(
                    GateCost.Zero,
                    ReconstructionMultiplications: multiplications,
                    LaneReads: LaneCount),
                UsedMagnitudeDomain: true,
                Scope: "Ordinary positive-integer magnitude"));
    }

    public CoordinateResult AddViaMagnitudeAndRefactor(PrimeCoordinates other)
    {
        if (!TryCompatible(other, out var failure))
        {
            return Failed("ADD_VIA_RECONSTRUCT_REFACTOR", failure);
        }

        var leftMagnitude = Reconstruct();
        var rightMagnitude = other.Reconstruct();
        var encoded = Encode(leftMagnitude.Value + rightMagnitude.Value, Basis, ExponentWidth);
        var cost = new CoordinateCost(
            GateCost.Zero,
            TrialRemainders: encoded.Receipt.Cost.TrialRemainders,
            FactorDivisions: encoded.Receipt.Cost.FactorDivisions,
            ReconstructionMultiplications:
                leftMagnitude.Receipt.Cost.ReconstructionMultiplications +
                rightMagnitude.Receipt.Cost.ReconstructionMultiplications,
            MagnitudeAdditions: 1,
            LaneReads: leftMagnitude.Receipt.Cost.LaneReads + rightMagnitude.Receipt.Cost.LaneReads,
            LaneWrites: encoded.Receipt.Cost.LaneWrites);
        return new CoordinateResult(
            encoded.Value,
            new CoordinateReceipt(
                "ADD_VIA_RECONSTRUCT_REFACTOR",
                encoded.Receipt.Succeeded,
                encoded.Receipt.Failure,
                cost,
                UsedMagnitudeDomain: true,
                Scope: "Addition leaves the coordinate-local domain",
                UnrepresentedResidual: encoded.Receipt.UnrepresentedResidual));
    }

    public string ToFactorizationString()
    {
        var factors = new List<string>();
        for (var lane = 0; lane < LaneCount; lane++)
        {
            var exponent = _exponents[lane].ToUnsigned();
            if (exponent.IsZero)
            {
                continue;
            }

            factors.Add(exponent == BigInteger.One
                ? Basis[lane].ToString(CultureInfo.InvariantCulture)
                : $"{Basis[lane]}^{exponent}");
        }

        return factors.Count == 0 ? "1" : string.Join(" * ", factors);
    }

    public bool Equals(PrimeCoordinates? other) =>
        other is not null &&
        Basis.Equals(other.Basis) &&
        _exponents.SequenceEqual(other._exponents);

    public override bool Equals(object? obj) => obj is PrimeCoordinates other && Equals(other);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Basis);
        foreach (var exponent in _exponents)
        {
            hash.Add(exponent);
        }

        return hash.ToHashCode();
    }

    public override string ToString() => ToFactorizationString();

    private CoordinateResult CoordinateExtremum(PrimeCoordinates other, bool minimum, string operation)
    {
        if (!TryCompatible(other, out var failure))
        {
            return Failed(operation, failure);
        }

        var lanes = new BinaryWord[LaneCount];
        var costs = new GateCost[LaneCount];
        for (var lane = 0; lane < LaneCount; lane++)
        {
            var result = minimum
                ? BinaryCircuit.Min(_exponents[lane], other._exponents[lane])
                : BinaryCircuit.Max(_exponents[lane], other._exponents[lane]);
            lanes[lane] = result.Value;
            costs[lane] = result.Cost;
        }

        return Succeeded(
            operation,
            new PrimeCoordinates(Basis, lanes),
            new CoordinateCost(
                GateCost.Parallel(costs),
                LaneReads: LaneCount * 2L,
                LaneWrites: LaneCount),
            usedMagnitudeDomain: false);
    }

    private bool TryCompatible(PrimeCoordinates other, out CoordinateFailure failure)
    {
        ArgumentNullException.ThrowIfNull(other);
        if (!Basis.Equals(other.Basis))
        {
            failure = CoordinateFailure.BasisMismatch;
            return false;
        }

        if (ExponentWidth != other.ExponentWidth)
        {
            failure = CoordinateFailure.ExponentWidthMismatch;
            return false;
        }

        failure = CoordinateFailure.None;
        return true;
    }

    private static (BitState State, GateCost Cost) AggregateStates(
        IReadOnlyList<BitState> states,
        int arrivalDepth,
        bool useOr,
        bool negateResult = false)
    {
        if (states.Count == 0)
        {
            throw new ArgumentException("At least one state is required for aggregation.", nameof(states));
        }

        var network = new GateNetwork();
        var current = states
            .Select(state => network.Input(state, arrivalDepth))
            .ToList();
        while (current.Count > 1)
        {
            var next = new List<Signal>((current.Count + 1) / 2);
            for (var index = 0; index < current.Count; index += 2)
            {
                if (index + 1 == current.Count)
                {
                    next.Add(current[index]);
                    continue;
                }

                next.Add(useOr
                    ? network.Or(current[index], current[index + 1])
                    : network.And(current[index], current[index + 1]));
            }

            current = next;
        }

        var result = negateResult ? network.Not(current[0]) : current[0];
        return (result.State, network.Cost);
    }

    private static CoordinateResult Succeeded(
        string operation,
        PrimeCoordinates value,
        CoordinateCost cost,
        bool usedMagnitudeDomain) =>
        new(
            value,
            new CoordinateReceipt(
                operation,
                true,
                CoordinateFailure.None,
                cost,
                usedMagnitudeDomain,
                "Positive fixed-basis prime coordinates"));

    private static CoordinateResult Failed(string operation, CoordinateFailure failure) =>
        new(
            null,
            new CoordinateReceipt(
                operation,
                false,
                failure,
                CoordinateCost.Zero,
                UsedMagnitudeDomain: false,
                Scope: "No operation performed"));

    private static EncodingResult EncodingFailure(
        CoordinateFailure failure,
        BigInteger residual,
        PrimeBasis basis,
        int exponentWidth,
        long remainderChecks,
        long divisions,
        long laneWrites) =>
        new(
            null,
            residual,
            new CoordinateReceipt(
                "ENCODE_BY_FACTORIZATION",
                false,
                failure,
                new CoordinateCost(
                    GateCost.Zero,
                    TrialRemainders: remainderChecks,
                    FactorDivisions: divisions,
                    LaneWrites: laneWrites),
                UsedMagnitudeDomain: true,
                Scope: $"Positive magnitudes, {basis.Count} lanes, {exponentWidth}-bit exponents",
                UnrepresentedResidual: residual));
}
