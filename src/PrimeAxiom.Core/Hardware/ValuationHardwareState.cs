using System.Collections.ObjectModel;
using System.Numerics;

namespace PrimeAxiom.Core.Hardware;

public enum ValuationStateFailure
{
    None,
    UnsupportedWidth,
    WidthMismatch,
    InvalidLane,
    InvalidPrime,
    InvalidExponent,
    NonCanonicalEncoding,
    SaturatedInput,
    CancellationUnderflow,
    DivisionByZero,
    MagnitudeOutOfRange,
    MagnitudeOverflow,
    NotDivisible,
}

public sealed record ValuationStateResult<T>(
    T? Value,
    ValuationStateFailure Failure,
    string? Detail = null)
    where T : class
{
    public bool Succeeded => Failure == ValuationStateFailure.None && Value is not null;

    internal static ValuationStateResult<T> Success(T value) =>
        new(value, ValuationStateFailure.None);

    internal static ValuationStateResult<T> Reject(
        ValuationStateFailure failure,
        string detail) =>
        new(null, failure, detail);
}

public sealed record ValuationAnswer(int LowerBound, bool IsExact, bool IsPositiveInfinity = false);

public sealed record ValuationPredicateAnswer(bool? Value)
{
    public bool IsKnown => Value.HasValue;
}

public sealed record ValuationMagnitude(BigInteger Value);

/// <summary>
/// The frozen Build 002 S4 domain. Caps are computed with integer arithmetic;
/// no floating-point logarithm participates in the semantic contract.
/// </summary>
public sealed class ValuationHardwareDomain
{
    private static readonly int[] Catalog = [2, 3, 5, 7];
    private readonly int[] _caps;
    private readonly ReadOnlyCollection<int> _capsView;

    private ValuationHardwareDomain(int width)
    {
        Width = width;
        MaximumMagnitude = (1 << width) - 1;
        _caps = Catalog.Select(prime => ComputeCap(width, prime)).ToArray();
        _capsView = Array.AsReadOnly(_caps);
    }

    public static IReadOnlyList<int> S4 { get; } = Array.AsReadOnly(Catalog);

    public int Width { get; }

    public int MaximumMagnitude { get; }

    public int LaneCount => _caps.Length;

    public IReadOnlyList<int> Caps => _capsView;

    public static bool IsSupportedWidth(int width) => width is 4 or 6 or 8;

    public static ValuationHardwareDomain ForWidth(int width)
    {
        if (!IsSupportedWidth(width))
        {
            throw new ArgumentOutOfRangeException(
                nameof(width),
                width,
                "Build 002 hardware widths are exactly 4, 6, and 8 bits.");
        }

        return new ValuationHardwareDomain(width);
    }

    public static int ComputeCap(int width, int prime)
    {
        if (!IsSupportedWidth(width))
        {
            throw new ArgumentOutOfRangeException(nameof(width));
        }

        if (Array.IndexOf(Catalog, prime) < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(prime));
        }

        var maximum = (1 << width) - 1;
        var cap = 0;
        var power = prime;
        while (power <= maximum)
        {
            cap++;
            if (power > maximum / prime)
            {
                break;
            }

            power *= prime;
        }

        return cap;
    }

    public int PrimeAt(int lane)
    {
        ValidateLane(lane);
        return Catalog[lane];
    }

    public int CapAt(int lane)
    {
        ValidateLane(lane);
        return _caps[lane];
    }

    public int IndexOfPrime(int prime)
    {
        for (var lane = 0; lane < _caps.Length; lane++)
        {
            if (Catalog[lane] == prime)
            {
                return lane;
            }
        }

        return -1;
    }

    internal int PrimePower(int lane, int exponent)
    {
        ValidateLane(lane);
        if (exponent < 0 || exponent > _caps[lane])
        {
            throw new ArgumentOutOfRangeException(nameof(exponent));
        }

        var result = 1;
        for (var index = 0; index < exponent; index++)
        {
            result *= Catalog[lane];
        }

        return result;
    }

    private void ValidateLane(int lane)
    {
        if ((uint)lane >= _caps.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(lane));
        }
    }
}

/// <summary>
/// Pure structural S4 state using minimally bounded binary exponents. A
/// saturated lane carries only a lower bound equal to its cap and is not an
/// exact free-commutative-monoid value.
/// </summary>
public sealed class ValuationHardwareState
{
    private readonly int[] _exponents;
    private readonly bool[] _saturatedLanes;
    private readonly ReadOnlyCollection<int> _exponentsView;
    private readonly ReadOnlyCollection<bool> _saturatedView;

    private ValuationHardwareState(
        ValuationHardwareDomain domain,
        bool isZero,
        int[] exponents,
        bool[] saturatedLanes)
    {
        Domain = domain;
        IsZero = isZero;
        _exponents = (int[])exponents.Clone();
        _saturatedLanes = (bool[])saturatedLanes.Clone();
        _exponentsView = Array.AsReadOnly(_exponents);
        _saturatedView = Array.AsReadOnly(_saturatedLanes);
    }

    public ValuationHardwareDomain Domain { get; }

    public int Width => Domain.Width;

    public bool IsZero { get; }

    public bool IsCanonical =>
        _exponents.Length == Domain.LaneCount &&
        _saturatedLanes.Length == Domain.LaneCount &&
        (!IsZero || _exponents.All(exponent => exponent == 0));

    public bool IsExact => !_saturatedLanes.Any(value => value);

    public IReadOnlyList<int> Exponents => _exponentsView;

    public IReadOnlyList<bool> SaturatedLanes => _saturatedView;

    public static ValuationStateResult<ValuationHardwareState> Create(
        int width,
        bool isZero,
        IReadOnlyList<int>? exponents)
    {
        if (!ValuationHardwareDomain.IsSupportedWidth(width))
        {
            return ValuationStateResult<ValuationHardwareState>.Reject(
                ValuationStateFailure.UnsupportedWidth,
                "The semantic hardware model supports only W in {4,6,8}.");
        }

        var domain = ValuationHardwareDomain.ForWidth(width);
        if (exponents is null || exponents.Count != domain.LaneCount)
        {
            return ValuationStateResult<ValuationHardwareState>.Reject(
                ValuationStateFailure.NonCanonicalEncoding,
                "A binary-exponent state must provide exactly four S4 lanes.");
        }

        var copied = exponents.ToArray();
        for (var lane = 0; lane < copied.Length; lane++)
        {
            if (copied[lane] < 0 || copied[lane] > domain.CapAt(lane))
            {
                return ValuationStateResult<ValuationHardwareState>.Reject(
                    ValuationStateFailure.InvalidExponent,
                    $"Lane {lane} is outside [0,{domain.CapAt(lane)}].");
            }
        }

        if (isZero && copied.Any(exponent => exponent != 0))
        {
            return ValuationStateResult<ValuationHardwareState>.Reject(
                ValuationStateFailure.NonCanonicalEncoding,
                "Canonical structural zero has an explicit zero tag and all exponent payload bits clear.");
        }

        return ValuationStateResult<ValuationHardwareState>.Success(
            new ValuationHardwareState(domain, isZero, copied, new bool[domain.LaneCount]));
    }

    public static ValuationHardwareState Zero(int width) =>
        CreateExactUnchecked(ValuationHardwareDomain.ForWidth(width), true, new int[4]);

    public static ValuationHardwareState Identity(int width) =>
        CreateExactUnchecked(ValuationHardwareDomain.ForWidth(width), false, new int[4]);

    public static ValuationStateResult<ValuationHardwareState> Power(
        int width,
        int prime,
        int exponent)
    {
        if (!ValuationHardwareDomain.IsSupportedWidth(width))
        {
            return ValuationStateResult<ValuationHardwareState>.Reject(
                ValuationStateFailure.UnsupportedWidth,
                "The semantic hardware model supports only W in {4,6,8}.");
        }

        var domain = ValuationHardwareDomain.ForWidth(width);
        var lane = domain.IndexOfPrime(prime);
        if (lane < 0)
        {
            return ValuationStateResult<ValuationHardwareState>.Reject(
                ValuationStateFailure.InvalidPrime,
                "POWER accepts only a configured S4 prime.");
        }

        if (exponent < 0 || exponent > domain.CapAt(lane))
        {
            return ValuationStateResult<ValuationHardwareState>.Reject(
                ValuationStateFailure.InvalidExponent,
                "The requested power is not encodable in the selected bounded lane.");
        }

        var exponents = new int[domain.LaneCount];
        exponents[lane] = exponent;
        return ValuationStateResult<ValuationHardwareState>.Success(
            CreateExactUnchecked(domain, false, exponents));
    }

    public int ExponentAt(int lane)
    {
        ValidateLane(lane);
        return _exponents[lane];
    }

    public bool IsLaneSaturated(int lane)
    {
        ValidateLane(lane);
        return _saturatedLanes[lane];
    }

    public ValuationStateResult<ValuationHardwareState> Compose(ValuationHardwareState other)
    {
        ArgumentNullException.ThrowIfNull(other);
        var mismatch = RejectWidthMismatch<ValuationHardwareState>(other.Width);
        if (mismatch is not null)
        {
            return mismatch;
        }

        if (IsZero || other.IsZero)
        {
            return ValuationStateResult<ValuationHardwareState>.Success(Zero(Width));
        }

        var exponents = new int[Domain.LaneCount];
        var saturated = new bool[Domain.LaneCount];
        for (var lane = 0; lane < exponents.Length; lane++)
        {
            var sum = _exponents[lane] + other._exponents[lane];
            saturated[lane] = _saturatedLanes[lane] ||
                              other._saturatedLanes[lane] ||
                              sum > Domain.CapAt(lane);
            exponents[lane] = saturated[lane] ? Domain.CapAt(lane) : sum;
        }

        return ValuationStateResult<ValuationHardwareState>.Success(
            CreateUnchecked(Domain, false, exponents, saturated));
    }

    public ValuationStateResult<ValuationHardwareState> Cancel(ValuationHardwareState divisor)
    {
        ArgumentNullException.ThrowIfNull(divisor);
        var mismatch = RejectWidthMismatch<ValuationHardwareState>(divisor.Width);
        if (mismatch is not null)
        {
            return mismatch;
        }

        if (divisor.IsZero)
        {
            return ValuationStateResult<ValuationHardwareState>.Reject(
                ValuationStateFailure.DivisionByZero,
                "CANCEL rejects a zero divisor.");
        }

        if (!IsExact || !divisor.IsExact)
        {
            return ValuationStateResult<ValuationHardwareState>.Reject(
                ValuationStateFailure.SaturatedInput,
                "CANCEL requires exact, unsaturated source states.");
        }

        if (IsZero)
        {
            return ValuationStateResult<ValuationHardwareState>.Success(Zero(Width));
        }

        var difference = new int[Domain.LaneCount];
        for (var lane = 0; lane < difference.Length; lane++)
        {
            if (_exponents[lane] < divisor._exponents[lane])
            {
                return ValuationStateResult<ValuationHardwareState>.Reject(
                    ValuationStateFailure.CancellationUnderflow,
                    "CANCEL rejected atomically because at least one exponent would underflow.");
            }

            difference[lane] = _exponents[lane] - divisor._exponents[lane];
        }

        return ValuationStateResult<ValuationHardwareState>.Success(
            CreateExactUnchecked(Domain, false, difference));
    }

    public ValuationStateResult<ValuationHardwareState> Meet(ValuationHardwareState other)
    {
        ArgumentNullException.ThrowIfNull(other);
        var exact = RequireCompatibleExact(other, "MEET");
        if (exact is not null)
        {
            return exact;
        }

        if (IsZero)
        {
            return ValuationStateResult<ValuationHardwareState>.Success(other.Copy());
        }

        if (other.IsZero)
        {
            return ValuationStateResult<ValuationHardwareState>.Success(Copy());
        }

        var minima = _exponents.Zip(other._exponents, Math.Min).ToArray();
        return ValuationStateResult<ValuationHardwareState>.Success(
            CreateExactUnchecked(Domain, false, minima));
    }

    public ValuationStateResult<ValuationHardwareState> Join(ValuationHardwareState other)
    {
        ArgumentNullException.ThrowIfNull(other);
        var exact = RequireCompatibleExact(other, "JOIN");
        if (exact is not null)
        {
            return exact;
        }

        if (IsZero || other.IsZero)
        {
            return ValuationStateResult<ValuationHardwareState>.Success(Zero(Width));
        }

        var maxima = _exponents.Zip(other._exponents, Math.Max).ToArray();
        return ValuationStateResult<ValuationHardwareState>.Success(
            CreateExactUnchecked(Domain, false, maxima));
    }

    public ValuationStateResult<ValuationPredicateAnswer> Divides(ValuationHardwareState dividend)
    {
        ArgumentNullException.ThrowIfNull(dividend);
        if (Width != dividend.Width)
        {
            return ValuationStateResult<ValuationPredicateAnswer>.Reject(
                ValuationStateFailure.WidthMismatch,
                "DIVIDES requires equal hardware widths.");
        }

        if (!IsExact || !dividend.IsExact)
        {
            return ValuationStateResult<ValuationPredicateAnswer>.Reject(
                ValuationStateFailure.SaturatedInput,
                "DIVIDES requires exact, unsaturated source states.");
        }

        if (IsZero)
        {
            return ValuationStateResult<ValuationPredicateAnswer>.Success(
                new ValuationPredicateAnswer(dividend.IsZero));
        }

        if (dividend.IsZero)
        {
            return ValuationStateResult<ValuationPredicateAnswer>.Success(
                new ValuationPredicateAnswer(true));
        }

        var divides = Enumerable.Range(0, Domain.LaneCount)
            .All(lane => _exponents[lane] <= dividend._exponents[lane]);
        return ValuationStateResult<ValuationPredicateAnswer>.Success(
            new ValuationPredicateAnswer(divides));
    }

    public ValuationStateResult<ValuationAnswer> Valuation(int prime)
    {
        var lane = Domain.IndexOfPrime(prime);
        if (lane < 0)
        {
            return ValuationStateResult<ValuationAnswer>.Reject(
                ValuationStateFailure.InvalidPrime,
                "VALUATION accepts only a configured S4 prime.");
        }

        if (IsZero)
        {
            return ValuationStateResult<ValuationAnswer>.Success(
                new ValuationAnswer(0, true, IsPositiveInfinity: true));
        }

        return ValuationStateResult<ValuationAnswer>.Success(
            new ValuationAnswer(_exponents[lane], !_saturatedLanes[lane]));
    }

    public ValuationThermometerState ToThermometer()
    {
        var lanes = new bool[Domain.LaneCount][];
        for (var lane = 0; lane < lanes.Length; lane++)
        {
            lanes[lane] = new bool[Domain.CapAt(lane)];
            if (IsZero)
            {
                continue;
            }

            for (var threshold = 0; threshold < lanes[lane].Length; threshold++)
            {
                lanes[lane][threshold] = threshold < _exponents[lane];
            }
        }

        return ValuationThermometerState.CreateUnchecked(
            Domain,
            IsZero,
            lanes,
            _saturatedLanes);
    }

    public ValuationStateResult<ValuationMagnitude> Reconstruct()
    {
        if (!IsExact)
        {
            return ValuationStateResult<ValuationMagnitude>.Reject(
                ValuationStateFailure.SaturatedInput,
                "A saturated state has no exact reconstructable magnitude.");
        }

        if (IsZero)
        {
            return ValuationStateResult<ValuationMagnitude>.Success(
                new ValuationMagnitude(BigInteger.Zero));
        }

        var magnitude = BigInteger.One;
        for (var lane = 0; lane < Domain.LaneCount; lane++)
        {
            magnitude *= BigInteger.Pow(Domain.PrimeAt(lane), _exponents[lane]);
        }

        if (magnitude > Domain.MaximumMagnitude)
        {
            return ValuationStateResult<ValuationMagnitude>.Reject(
                ValuationStateFailure.MagnitudeOverflow,
                "The structural state is legal, but it lies outside the common W-bit magnitude domain.");
        }

        return ValuationStateResult<ValuationMagnitude>.Success(new ValuationMagnitude(magnitude));
    }

    internal static ValuationHardwareState CreateUnchecked(
        ValuationHardwareDomain domain,
        bool isZero,
        int[] exponents,
        bool[] saturatedLanes) =>
        new(domain, isZero, exponents, saturatedLanes);

    private static ValuationHardwareState CreateExactUnchecked(
        ValuationHardwareDomain domain,
        bool isZero,
        int[] exponents) =>
        new(domain, isZero, exponents, new bool[domain.LaneCount]);

    private ValuationHardwareState Copy() =>
        CreateUnchecked(Domain, IsZero, _exponents, _saturatedLanes);

    private ValuationStateResult<T>? RejectWidthMismatch<T>(int otherWidth)
        where T : class =>
        Width == otherWidth
            ? null
            : ValuationStateResult<T>.Reject(
                ValuationStateFailure.WidthMismatch,
                "The operation requires equal hardware widths.");

    private ValuationStateResult<ValuationHardwareState>? RequireCompatibleExact(
        ValuationHardwareState other,
        string operation)
    {
        var mismatch = RejectWidthMismatch<ValuationHardwareState>(other.Width);
        if (mismatch is not null)
        {
            return mismatch;
        }

        return IsExact && other.IsExact
            ? null
            : ValuationStateResult<ValuationHardwareState>.Reject(
                ValuationStateFailure.SaturatedInput,
                $"{operation} requires exact, unsaturated source states.");
    }

    private void ValidateLane(int lane)
    {
        if ((uint)lane >= Domain.LaneCount)
        {
            throw new ArgumentOutOfRangeException(nameof(lane));
        }
    }
}

/// <summary>
/// Canonical threshold encoding of the same bounded S4 exponent state. The
/// threshold payload for structural zero is normalized to all false because
/// the explicit zero tag, rather than a finite vector, carries infinity.
/// </summary>
public sealed class ValuationThermometerState
{
    private readonly bool[][] _thresholds;
    private readonly bool[] _saturatedLanes;
    private readonly ReadOnlyCollection<bool> _saturatedView;

    private ValuationThermometerState(
        ValuationHardwareDomain domain,
        bool isZero,
        bool[][] thresholds,
        bool[] saturatedLanes)
    {
        Domain = domain;
        IsZero = isZero;
        _thresholds = thresholds.Select(lane => (bool[])lane.Clone()).ToArray();
        _saturatedLanes = (bool[])saturatedLanes.Clone();
        _saturatedView = Array.AsReadOnly(_saturatedLanes);
    }

    public ValuationHardwareDomain Domain { get; }

    public int Width => Domain.Width;

    public bool IsZero { get; }

    public bool IsCanonical
    {
        get
        {
            if (_thresholds.Length != Domain.LaneCount ||
                _saturatedLanes.Length != Domain.LaneCount)
            {
                return false;
            }

            for (var lane = 0; lane < _thresholds.Length; lane++)
            {
                if (_thresholds[lane].Length != Domain.CapAt(lane) ||
                    (IsZero && _thresholds[lane].Any(bit => bit)))
                {
                    return false;
                }

                var observedFalse = false;
                foreach (var bit in _thresholds[lane])
                {
                    if (!bit)
                    {
                        observedFalse = true;
                    }
                    else if (observedFalse)
                    {
                        return false;
                    }
                }
            }

            return true;
        }
    }

    public bool IsExact => !_saturatedLanes.Any(value => value);

    public IReadOnlyList<bool> SaturatedLanes => _saturatedView;

    public static ValuationStateResult<ValuationThermometerState> Create(
        int width,
        bool isZero,
        IReadOnlyList<IReadOnlyList<bool>>? thresholds)
    {
        if (!ValuationHardwareDomain.IsSupportedWidth(width))
        {
            return ValuationStateResult<ValuationThermometerState>.Reject(
                ValuationStateFailure.UnsupportedWidth,
                "The semantic hardware model supports only W in {4,6,8}.");
        }

        var domain = ValuationHardwareDomain.ForWidth(width);
        if (thresholds is null || thresholds.Count != domain.LaneCount)
        {
            return ValuationStateResult<ValuationThermometerState>.Reject(
                ValuationStateFailure.NonCanonicalEncoding,
                "A thermometer state must provide exactly four S4 lanes.");
        }

        var copied = new bool[domain.LaneCount][];
        for (var lane = 0; lane < copied.Length; lane++)
        {
            if (thresholds[lane] is null || thresholds[lane].Count != domain.CapAt(lane))
            {
                return ValuationStateResult<ValuationThermometerState>.Reject(
                    ValuationStateFailure.NonCanonicalEncoding,
                    $"Thermometer lane {lane} has the wrong threshold count.");
            }

            copied[lane] = thresholds[lane].ToArray();
            var observedFalse = false;
            foreach (var bit in copied[lane])
            {
                if (!bit)
                {
                    observedFalse = true;
                }
                else if (observedFalse)
                {
                    return ValuationStateResult<ValuationThermometerState>.Reject(
                        ValuationStateFailure.NonCanonicalEncoding,
                        "Canonical thermometer lanes are monotone true-prefix vectors.");
                }
            }

            if (isZero && copied[lane].Any(bit => bit))
            {
                return ValuationStateResult<ValuationThermometerState>.Reject(
                    ValuationStateFailure.NonCanonicalEncoding,
                    "Canonical structural zero clears all thermometer payload bits.");
            }
        }

        return ValuationStateResult<ValuationThermometerState>.Success(
            new ValuationThermometerState(domain, isZero, copied, new bool[domain.LaneCount]));
    }

    public static ValuationThermometerState FromExponentState(ValuationHardwareState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        return state.ToThermometer();
    }

    public static ValuationStateResult<ValuationThermometerState> Power(
        int width,
        int prime,
        int exponent)
    {
        var binary = ValuationHardwareState.Power(width, prime, exponent);
        return binary.Succeeded
            ? ValuationStateResult<ValuationThermometerState>.Success(binary.Value!.ToThermometer())
            : ValuationStateResult<ValuationThermometerState>.Reject(binary.Failure, binary.Detail!);
    }

    public bool ThresholdAt(int lane, int threshold)
    {
        ValidateThreshold(lane, threshold);
        return _thresholds[lane][threshold - 1];
    }

    public IReadOnlyList<bool> ThresholdsAt(int lane)
    {
        ValidateLane(lane);
        return Array.AsReadOnly((bool[])_thresholds[lane].Clone());
    }

    public ValuationHardwareState ToExponentState()
    {
        var exponents = _thresholds.Select(lane => lane.Count(bit => bit)).ToArray();
        if (IsZero)
        {
            Array.Clear(exponents);
        }

        return ValuationHardwareState.CreateUnchecked(
            Domain,
            IsZero,
            exponents,
            _saturatedLanes);
    }

    public ValuationStateResult<ValuationThermometerState> Compose(ValuationThermometerState other)
    {
        ArgumentNullException.ThrowIfNull(other);
        var binary = ToExponentState().Compose(other.ToExponentState());
        return ConvertResult(binary);
    }

    public ValuationStateResult<ValuationThermometerState> Cancel(ValuationThermometerState divisor)
    {
        ArgumentNullException.ThrowIfNull(divisor);
        var binary = ToExponentState().Cancel(divisor.ToExponentState());
        return ConvertResult(binary);
    }

    public ValuationStateResult<ValuationThermometerState> Meet(ValuationThermometerState other)
    {
        ArgumentNullException.ThrowIfNull(other);
        var error = RequireCompatibleExact(other, "MEET");
        if (error is not null)
        {
            return error;
        }

        if (IsZero)
        {
            return ValuationStateResult<ValuationThermometerState>.Success(other.Copy());
        }

        if (other.IsZero)
        {
            return ValuationStateResult<ValuationThermometerState>.Success(Copy());
        }

        var result = new bool[Domain.LaneCount][];
        for (var lane = 0; lane < result.Length; lane++)
        {
            result[lane] = _thresholds[lane]
                .Zip(other._thresholds[lane], (left, right) => left && right)
                .ToArray();
        }

        return ValuationStateResult<ValuationThermometerState>.Success(
            CreateUnchecked(Domain, false, result, new bool[Domain.LaneCount]));
    }

    public ValuationStateResult<ValuationThermometerState> Join(ValuationThermometerState other)
    {
        ArgumentNullException.ThrowIfNull(other);
        var error = RequireCompatibleExact(other, "JOIN");
        if (error is not null)
        {
            return error;
        }

        if (IsZero || other.IsZero)
        {
            return ValuationStateResult<ValuationThermometerState>.Success(
                ValuationHardwareState.Zero(Width).ToThermometer());
        }

        var result = new bool[Domain.LaneCount][];
        for (var lane = 0; lane < result.Length; lane++)
        {
            result[lane] = _thresholds[lane]
                .Zip(other._thresholds[lane], (left, right) => left || right)
                .ToArray();
        }

        return ValuationStateResult<ValuationThermometerState>.Success(
            CreateUnchecked(Domain, false, result, new bool[Domain.LaneCount]));
    }

    public ValuationStateResult<ValuationPredicateAnswer> Divides(ValuationThermometerState dividend)
    {
        ArgumentNullException.ThrowIfNull(dividend);
        if (Width != dividend.Width)
        {
            return ValuationStateResult<ValuationPredicateAnswer>.Reject(
                ValuationStateFailure.WidthMismatch,
                "DIVIDES requires equal hardware widths.");
        }

        if (!IsExact || !dividend.IsExact)
        {
            return ValuationStateResult<ValuationPredicateAnswer>.Reject(
                ValuationStateFailure.SaturatedInput,
                "DIVIDES requires exact, unsaturated source states.");
        }

        if (IsZero)
        {
            return ValuationStateResult<ValuationPredicateAnswer>.Success(
                new ValuationPredicateAnswer(dividend.IsZero));
        }

        if (dividend.IsZero)
        {
            return ValuationStateResult<ValuationPredicateAnswer>.Success(
                new ValuationPredicateAnswer(true));
        }

        for (var lane = 0; lane < Domain.LaneCount; lane++)
        {
            for (var bit = 0; bit < _thresholds[lane].Length; bit++)
            {
                if (_thresholds[lane][bit] && !dividend._thresholds[lane][bit])
                {
                    return ValuationStateResult<ValuationPredicateAnswer>.Success(
                        new ValuationPredicateAnswer(false));
                }
            }
        }

        return ValuationStateResult<ValuationPredicateAnswer>.Success(
            new ValuationPredicateAnswer(true));
    }

    public ValuationStateResult<ValuationAnswer> Valuation(int prime) =>
        ToExponentState().Valuation(prime);

    public ValuationStateResult<ValuationMagnitude> Reconstruct() =>
        ToExponentState().Reconstruct();

    internal static ValuationThermometerState CreateUnchecked(
        ValuationHardwareDomain domain,
        bool isZero,
        bool[][] thresholds,
        bool[] saturatedLanes) =>
        new(domain, isZero, thresholds, saturatedLanes);

    private static ValuationStateResult<ValuationThermometerState> ConvertResult(
        ValuationStateResult<ValuationHardwareState> binary) =>
        binary.Succeeded
            ? ValuationStateResult<ValuationThermometerState>.Success(binary.Value!.ToThermometer())
            : ValuationStateResult<ValuationThermometerState>.Reject(binary.Failure, binary.Detail!);

    private ValuationThermometerState Copy() =>
        CreateUnchecked(Domain, IsZero, _thresholds, _saturatedLanes);

    private ValuationStateResult<ValuationThermometerState>? RequireCompatibleExact(
        ValuationThermometerState other,
        string operation)
    {
        if (Width != other.Width)
        {
            return ValuationStateResult<ValuationThermometerState>.Reject(
                ValuationStateFailure.WidthMismatch,
                "The operation requires equal hardware widths.");
        }

        return IsExact && other.IsExact
            ? null
            : ValuationStateResult<ValuationThermometerState>.Reject(
                ValuationStateFailure.SaturatedInput,
                $"{operation} requires exact, unsaturated source states.");
    }

    private void ValidateLane(int lane)
    {
        if ((uint)lane >= Domain.LaneCount)
        {
            throw new ArgumentOutOfRangeException(nameof(lane));
        }
    }

    private void ValidateThreshold(int lane, int threshold)
    {
        ValidateLane(lane);
        if (threshold < 1 || threshold > Domain.CapAt(lane))
        {
            throw new ArgumentOutOfRangeException(nameof(threshold));
        }
    }
}

/// <summary>
/// Exact binary magnitude with an S4 threshold sidecar. Magnitude is always
/// authoritative. When Valid is false, true threshold bits remain certified
/// lower-bound facts; clear bits are unknown rather than certified negatives.
/// </summary>
public sealed class BinaryValuationSidecar
{
    private readonly bool[][] _thresholds;

    private BinaryValuationSidecar(
        ValuationHardwareDomain domain,
        int magnitude,
        bool valid,
        bool[][] thresholds)
    {
        Domain = domain;
        Magnitude = magnitude;
        Valid = valid;
        _thresholds = thresholds.Select(lane => (bool[])lane.Clone()).ToArray();
    }

    public ValuationHardwareDomain Domain { get; }

    public int Width => Domain.Width;

    public int Magnitude { get; }

    public bool Valid { get; }

    public bool IsZero => Magnitude == 0;

    public static ValuationStateResult<BinaryValuationSidecar> Encode(int width, int magnitude)
    {
        if (!ValuationHardwareDomain.IsSupportedWidth(width))
        {
            return ValuationStateResult<BinaryValuationSidecar>.Reject(
                ValuationStateFailure.UnsupportedWidth,
                "The semantic hardware model supports only W in {4,6,8}.");
        }

        var domain = ValuationHardwareDomain.ForWidth(width);
        if (magnitude < 0 || magnitude > domain.MaximumMagnitude)
        {
            return ValuationStateResult<BinaryValuationSidecar>.Reject(
                ValuationStateFailure.MagnitudeOutOfRange,
                "Magnitude must fit the declared unsigned W-bit word.");
        }

        if (magnitude == 0)
        {
            var infinite = Enumerable.Range(0, domain.LaneCount)
                .Select(lane => Enumerable.Repeat(true, domain.CapAt(lane)).ToArray())
                .ToArray();
            return ValuationStateResult<BinaryValuationSidecar>.Success(
                new BinaryValuationSidecar(domain, 0, true, infinite));
        }

        var remainder = magnitude;
        var exponents = new int[domain.LaneCount];
        for (var lane = 0; lane < domain.LaneCount; lane++)
        {
            var prime = domain.PrimeAt(lane);
            while (remainder % prime == 0)
            {
                exponents[lane]++;
                remainder /= prime;
            }
        }

        return ValuationStateResult<BinaryValuationSidecar>.Success(
            CreateFromLowerBounds(domain, magnitude, valid: true, exponents));
    }

    public bool ThresholdAt(int prime, int exponent)
    {
        var lane = RequirePrimeLane(prime);
        if (exponent < 1 || exponent > Domain.CapAt(lane))
        {
            throw new ArgumentOutOfRangeException(nameof(exponent));
        }

        return _thresholds[lane][exponent - 1];
    }

    public int LowerBoundAtPrime(int prime)
    {
        var lane = RequirePrimeLane(prime);
        return LowerBoundAtLane(lane);
    }

    public ValuationStateResult<ValuationAnswer> Valuation(int prime)
    {
        var lane = Domain.IndexOfPrime(prime);
        if (lane < 0)
        {
            return ValuationStateResult<ValuationAnswer>.Reject(
                ValuationStateFailure.InvalidPrime,
                "VALUATION accepts only a configured S4 prime.");
        }

        if (IsZero)
        {
            return ValuationStateResult<ValuationAnswer>.Success(
                new ValuationAnswer(0, true, IsPositiveInfinity: true));
        }

        return ValuationStateResult<ValuationAnswer>.Success(
            new ValuationAnswer(LowerBoundAtLane(lane), Valid));
    }

    public ValuationStateResult<ValuationPredicateAnswer> IsDivisibleByPrimePower(
        int prime,
        int exponent)
    {
        var lane = Domain.IndexOfPrime(prime);
        if (lane < 0)
        {
            return ValuationStateResult<ValuationPredicateAnswer>.Reject(
                ValuationStateFailure.InvalidPrime,
                "The query accepts only a configured S4 prime.");
        }

        if (exponent < 0 || exponent > Domain.CapAt(lane))
        {
            return ValuationStateResult<ValuationPredicateAnswer>.Reject(
                ValuationStateFailure.InvalidExponent,
                "The requested threshold is outside the configured lane.");
        }

        if (exponent == 0 || IsZero)
        {
            return ValuationStateResult<ValuationPredicateAnswer>.Success(
                new ValuationPredicateAnswer(true));
        }

        var threshold = _thresholds[lane][exponent - 1];
        if (Valid)
        {
            return ValuationStateResult<ValuationPredicateAnswer>.Success(
                new ValuationPredicateAnswer(threshold));
        }

        return ValuationStateResult<ValuationPredicateAnswer>.Success(
            new ValuationPredicateAnswer(threshold ? true : null));
    }

    public ValuationStateResult<BinaryValuationSidecar> ScaleKnownFactor(
        int prime,
        int exponent = 1)
    {
        var factor = ResolveKnownFactor(prime, exponent);
        if (!factor.Succeeded)
        {
            return ValuationStateResult<BinaryValuationSidecar>.Reject(factor.Failure, factor.Detail!);
        }

        if (IsZero)
        {
            return Encode(Width, 0);
        }

        if (Magnitude > Domain.MaximumMagnitude / factor.Value!.Value)
        {
            return ValuationStateResult<BinaryValuationSidecar>.Reject(
                ValuationStateFailure.MagnitudeOverflow,
                "Known-factor scaling would overflow the authoritative W-bit magnitude.");
        }

        var lane = Domain.IndexOfPrime(prime);
        var lowerBounds = LowerBounds();
        lowerBounds[lane] += exponent;
        return ValuationStateResult<BinaryValuationSidecar>.Success(
            CreateFromLowerBounds(
                Domain,
                Magnitude * factor.Value.Value,
                Valid,
                lowerBounds));
    }

    public ValuationStateResult<BinaryValuationSidecar> CancelKnownFactor(
        int prime,
        int exponent = 1)
    {
        var factor = ResolveKnownFactor(prime, exponent);
        if (!factor.Succeeded)
        {
            return ValuationStateResult<BinaryValuationSidecar>.Reject(factor.Failure, factor.Detail!);
        }

        if (IsZero)
        {
            return Encode(Width, 0);
        }

        if (Magnitude % factor.Value!.Value != 0)
        {
            return ValuationStateResult<BinaryValuationSidecar>.Reject(
                ValuationStateFailure.NotDivisible,
                "Known-factor cancellation rejected atomically because magnitude is not divisible.");
        }

        var lane = Domain.IndexOfPrime(prime);
        var lowerBounds = LowerBounds();
        lowerBounds[lane] = Math.Max(0, lowerBounds[lane] - exponent);
        return ValuationStateResult<BinaryValuationSidecar>.Success(
            CreateFromLowerBounds(
                Domain,
                Magnitude / factor.Value.Value,
                Valid,
                lowerBounds));
    }

    public ValuationStateResult<BinaryValuationSidecar> Add(
        BinaryValuationSidecar other,
        bool refreshExact = false)
    {
        ArgumentNullException.ThrowIfNull(other);
        if (Width != other.Width)
        {
            return ValuationStateResult<BinaryValuationSidecar>.Reject(
                ValuationStateFailure.WidthMismatch,
                "Sidecar addition requires equal hardware widths.");
        }

        if (Magnitude > Domain.MaximumMagnitude - other.Magnitude)
        {
            return ValuationStateResult<BinaryValuationSidecar>.Reject(
                ValuationStateFailure.MagnitudeOverflow,
                "Sidecar addition would overflow the authoritative W-bit magnitude.");
        }

        var sum = Magnitude + other.Magnitude;
        if (refreshExact || sum == 0)
        {
            return Encode(Width, sum);
        }

        if (IsZero)
        {
            return ValuationStateResult<BinaryValuationSidecar>.Success(other.Copy());
        }

        if (other.IsZero)
        {
            return ValuationStateResult<BinaryValuationSidecar>.Success(Copy());
        }

        var left = LowerBounds();
        var right = other.LowerBounds();
        var commonLowerBound = left.Zip(right, Math.Min).ToArray();
        var remainsExact = Valid &&
                           other.Valid &&
                           Enumerable.Range(0, Domain.LaneCount)
                               .All(lane => left[lane] != right[lane]);

        return ValuationStateResult<BinaryValuationSidecar>.Success(
            CreateFromLowerBounds(Domain, sum, remainsExact, commonLowerBound));
    }

    public ValuationStateResult<BinaryValuationSidecar> Refresh() => Encode(Width, Magnitude);

    private static BinaryValuationSidecar CreateFromLowerBounds(
        ValuationHardwareDomain domain,
        int magnitude,
        bool valid,
        IReadOnlyList<int> lowerBounds)
    {
        var thresholds = new bool[domain.LaneCount][];
        for (var lane = 0; lane < thresholds.Length; lane++)
        {
            thresholds[lane] = new bool[domain.CapAt(lane)];
            for (var bit = 0; bit < thresholds[lane].Length; bit++)
            {
                thresholds[lane][bit] = bit < lowerBounds[lane];
            }
        }

        return new BinaryValuationSidecar(domain, magnitude, valid, thresholds);
    }

    private int[] LowerBounds() =>
        Enumerable.Range(0, Domain.LaneCount).Select(LowerBoundAtLane).ToArray();

    private int LowerBoundAtLane(int lane) => _thresholds[lane].Count(bit => bit);

    private int RequirePrimeLane(int prime)
    {
        var lane = Domain.IndexOfPrime(prime);
        if (lane < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(prime));
        }

        return lane;
    }

    private ValuationStateResult<KnownFactor> ResolveKnownFactor(int prime, int exponent)
    {
        var lane = Domain.IndexOfPrime(prime);
        if (lane < 0)
        {
            return ValuationStateResult<KnownFactor>.Reject(
                ValuationStateFailure.InvalidPrime,
                "Known-factor operations accept only configured S4 primes.");
        }

        if (exponent < 0 || exponent > Domain.CapAt(lane))
        {
            return ValuationStateResult<KnownFactor>.Reject(
                ValuationStateFailure.InvalidExponent,
                "The requested factor power is outside the configured lane.");
        }

        return ValuationStateResult<KnownFactor>.Success(
            new KnownFactor(Domain.PrimePower(lane, exponent)));
    }

    private BinaryValuationSidecar Copy() =>
        new(Domain, Magnitude, Valid, _thresholds);

    private sealed record KnownFactor(int Value);
}
