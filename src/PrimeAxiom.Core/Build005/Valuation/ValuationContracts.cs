using System.Collections.ObjectModel;

namespace PrimeAxiom.Core.Build005.Valuation;

/// <summary>
/// The four non-speculative policies frozen by
/// PAH-BUILD005-DEMAND-VALUATION-0001.
/// </summary>
public enum ValuationCachePolicy
{
    BinDirectBest,
    BinContentAnswerLruK,
    BinFrontierNoPropK,
    BinPrimeFrontierPropK,
}

public enum ValuationFailure
{
    None,
    InvalidSlot,
    InvalidPrime,
    InvalidThreshold,
    MagnitudeOutOfRange,
    UninitializedSlot,
    Overflow,
    MalformedFrontier,
}

/// <summary>
/// The only labels accepted as primes by the Build 005 semantic service.
/// Prime identity is catalogue configuration, not a caller assertion.
/// </summary>
public static class Build005PrimeCatalogue
{
    private static readonly int[] Values = [2, 3, 5, 7, 11, 13, 17, 19, 23, 29, 31];
    private static readonly ReadOnlyCollection<int> ReadOnlyValues = Array.AsReadOnly(Values);

    public static IReadOnlyList<int> Primes => ReadOnlyValues;

    public static bool TryGetIndex(int prime, out int index)
    {
        index = Array.IndexOf(Values, prime);
        return index >= 0;
    }

    public static int GetPrime(int index)
    {
        if ((uint)index >= (uint)Values.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        return Values[index];
    }
}

/// <summary>
/// One exact frontier equation. For a finite nonzero value n, the payload
/// means n = Prime^LowerBound * Residual. Terminal additionally certifies
/// that Prime does not divide Residual. Infinite is the dedicated zero state.
/// </summary>
public sealed record ValuationFrontier(
    bool Valid,
    int Slot,
    byte Generation,
    int PrimeIndex,
    int LowerBound,
    ulong Residual,
    bool Terminal,
    bool Infinite)
{
    public int Prime => Build005PrimeCatalogue.GetPrime(PrimeIndex);

    /// <summary>
    /// Checks the semantic equation without constructing p^L, so the
    /// validation itself cannot overflow.
    /// </summary>
    public bool Satisfies(ulong authoritativeMagnitude, out string detail)
    {
        if (!Valid)
        {
            detail = "An invalid cache line carries no frontier certificate.";
            return false;
        }

        if ((uint)Slot >= DemandValuationService.SlotCount)
        {
            detail = "The slot tag is outside the four-slot domain.";
            return false;
        }

        if ((uint)PrimeIndex >= (uint)Build005PrimeCatalogue.Primes.Count)
        {
            detail = "The prime index is outside the frozen catalogue.";
            return false;
        }

        if (LowerBound < 0)
        {
            detail = "A finite lower bound cannot be negative.";
            return false;
        }

        if (Infinite)
        {
            var validZero = authoritativeMagnitude == 0 && Terminal;
            detail = validZero
                ? string.Empty
                : "Only zero may carry an infinite terminal valuation.";
            return validZero;
        }

        if (authoritativeMagnitude == 0 || Residual == 0)
        {
            detail = "A finite frontier requires nonzero magnitude and residual.";
            return false;
        }

        var quotient = authoritativeMagnitude;
        var prime = (ulong)Prime;
        for (var exponent = 0; exponent < LowerBound; exponent++)
        {
            if (quotient % prime != 0)
            {
                detail = "The claimed exponent exceeds the exact divisible prefix.";
                return false;
            }

            quotient /= prime;
        }

        if (quotient != Residual)
        {
            detail = "The residual does not satisfy n = p^L * R.";
            return false;
        }

        if (Terminal && Residual % prime == 0)
        {
            detail = "A terminal residual is still divisible by the selected prime.";
            return false;
        }

        detail = string.Empty;
        return true;
    }
}

public sealed record ValueSlotSnapshot(
    int Slot,
    bool Initialized,
    ulong Magnitude,
    byte Generation);

/// <summary>
/// A stable physical-line view used by tests and deterministic experiment
/// runners. ContentMagnitude is populated only by the content-addressed
/// control; frontier controls use Slot and Generation.
/// </summary>
public sealed record ValuationCacheLineSnapshot(
    int PhysicalIndex,
    bool Valid,
    ulong? ContentMagnitude,
    int Slot,
    byte Generation,
    int PrimeIndex,
    int LowerBound,
    ulong Residual,
    bool Terminal,
    bool Infinite,
    ulong LastUseTick);

public sealed record PowerTestAnswer(
    bool IsAtLeastThreshold,
    int Threshold,
    int CertifiedLowerBound,
    ulong Residual,
    bool Terminal,
    bool Infinite)
{
    public bool IsExact => Terminal || Infinite;
}

public sealed record ExactValuationAnswer(
    int Exponent,
    ulong Residual,
    bool Infinite);

public sealed record ValuationMutationReceipt(
    int Destination,
    ulong Magnitude,
    byte PreviousGeneration,
    byte Generation,
    bool GenerationWrapped,
    int PropagatedFrontiers);

/// <summary>
/// Cumulative or per-operation semantic accounting. CacheHits counts a
/// matching retained line even when a partial line still needs refinement.
/// PositiveCacheHits and NegativeCacheHits count TEST_POWER requests answered
/// completely by that retained line. CacheFills are insertions; CacheUpdates
/// are in-place refinements of an existing key.
/// </summary>
public sealed record ValuationMetrics
{
    public long RequestedInstructions { get; init; }
    public long Loads { get; init; }
    public long Additions { get; init; }
    public long Multiplications { get; init; }
    public long MultiplyByPrimeOperations { get; init; }
    public long RejectedOperations { get; init; }

    public long CtzCalls { get; init; }
    public long CtzBitInspections { get; init; }
    public long DivModCalls { get; init; }
    public long ExactDivisions { get; init; }
    public long FailedDivisibilityProbes { get; init; }
    public long FrontierRefinements { get; init; }

    public long CacheLookups { get; init; }
    public long CacheTagComparisons { get; init; }
    public long CacheHits { get; init; }
    public long PositiveCacheHits { get; init; }
    public long NegativeCacheHits { get; init; }
    public long CacheMisses { get; init; }
    public long CacheFills { get; init; }
    public long CacheUpdates { get; init; }
    public long CacheEvictions { get; init; }
    public long CacheInvalidations { get; init; }
    public long CacheTransfers { get; init; }
    public long RejectedStaleHitAttempts { get; init; }
    public long GenerationWrapFlushes { get; init; }
    public long CacheLinesFlushed { get; init; }

    public long TerminalCertificatesEarned { get; init; }
    public long LowerBoundCertificatesEarned { get; init; }
    public long TerminalCertificatesPropagated { get; init; }
    public long LowerBoundCertificatesPropagated { get; init; }
    public long PropagationExponentAdds { get; init; }
    public long PropagationResidualMultiplies { get; init; }

    internal static ValuationMetrics Difference(ValuationMetrics after, ValuationMetrics before) =>
        new()
        {
            RequestedInstructions = after.RequestedInstructions - before.RequestedInstructions,
            Loads = after.Loads - before.Loads,
            Additions = after.Additions - before.Additions,
            Multiplications = after.Multiplications - before.Multiplications,
            MultiplyByPrimeOperations = after.MultiplyByPrimeOperations - before.MultiplyByPrimeOperations,
            RejectedOperations = after.RejectedOperations - before.RejectedOperations,
            CtzCalls = after.CtzCalls - before.CtzCalls,
            CtzBitInspections = after.CtzBitInspections - before.CtzBitInspections,
            DivModCalls = after.DivModCalls - before.DivModCalls,
            ExactDivisions = after.ExactDivisions - before.ExactDivisions,
            FailedDivisibilityProbes = after.FailedDivisibilityProbes - before.FailedDivisibilityProbes,
            FrontierRefinements = after.FrontierRefinements - before.FrontierRefinements,
            CacheLookups = after.CacheLookups - before.CacheLookups,
            CacheTagComparisons = after.CacheTagComparisons - before.CacheTagComparisons,
            CacheHits = after.CacheHits - before.CacheHits,
            PositiveCacheHits = after.PositiveCacheHits - before.PositiveCacheHits,
            NegativeCacheHits = after.NegativeCacheHits - before.NegativeCacheHits,
            CacheMisses = after.CacheMisses - before.CacheMisses,
            CacheFills = after.CacheFills - before.CacheFills,
            CacheUpdates = after.CacheUpdates - before.CacheUpdates,
            CacheEvictions = after.CacheEvictions - before.CacheEvictions,
            CacheInvalidations = after.CacheInvalidations - before.CacheInvalidations,
            CacheTransfers = after.CacheTransfers - before.CacheTransfers,
            RejectedStaleHitAttempts = after.RejectedStaleHitAttempts - before.RejectedStaleHitAttempts,
            GenerationWrapFlushes = after.GenerationWrapFlushes - before.GenerationWrapFlushes,
            CacheLinesFlushed = after.CacheLinesFlushed - before.CacheLinesFlushed,
            TerminalCertificatesEarned = after.TerminalCertificatesEarned - before.TerminalCertificatesEarned,
            LowerBoundCertificatesEarned = after.LowerBoundCertificatesEarned - before.LowerBoundCertificatesEarned,
            TerminalCertificatesPropagated = after.TerminalCertificatesPropagated - before.TerminalCertificatesPropagated,
            LowerBoundCertificatesPropagated = after.LowerBoundCertificatesPropagated - before.LowerBoundCertificatesPropagated,
            PropagationExponentAdds = after.PropagationExponentAdds - before.PropagationExponentAdds,
            PropagationResidualMultiplies = after.PropagationResidualMultiplies - before.PropagationResidualMultiplies,
        };
}

public sealed record ValuationOperationResult<T>(
    bool Succeeded,
    ValuationFailure Failure,
    string Detail,
    T? Value,
    ValuationMetrics MetricsDelta)
    where T : class;
