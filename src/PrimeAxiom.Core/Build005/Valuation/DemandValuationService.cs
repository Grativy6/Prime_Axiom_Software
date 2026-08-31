using System.Collections.ObjectModel;

namespace PrimeAxiom.Core.Build005.Valuation;

/// <summary>
/// Exact Build 005 semantic service. Unsigned binary magnitude remains the
/// authority; retained frontiers are versioned, derived evidence only.
/// </summary>
public sealed class DemandValuationService
{
    public const int SlotCount = 4;
    private const int MaximumCatalogueEntries = 16;

    private readonly SlotState[] _slots =
        Enumerable.Range(0, SlotCount).Select(_ => new SlotState()).ToArray();
    private readonly CacheLine[] _cache;
    private readonly MetricCounter _metrics = new();
    private readonly int[] _divisors;
    private readonly ReadOnlyCollection<int> _readOnlyDivisors;
    private readonly bool _allowMultiplicativePropagation;
    private readonly bool _usesPrimeCatalogue;
    private ulong _lastUseTick;

    public DemandValuationService(
        int width,
        ValuationCachePolicy policy,
        int cacheCapacity)
        : this(
            width,
            policy,
            cacheCapacity,
            Build005PrimeCatalogue.Primes,
            allowMultiplicativePropagation: true,
            usesPrimeCatalogue: true)
    {
    }

    private DemandValuationService(
        int width,
        ValuationCachePolicy policy,
        int cacheCapacity,
        IReadOnlyList<int> divisors,
        bool allowMultiplicativePropagation,
        bool usesPrimeCatalogue)
    {
        if (width is not (8 or 16 or 32))
        {
            throw new ArgumentOutOfRangeException(
                nameof(width),
                "Build 005 semantic widths are exactly 8, 16, and 32 bits.");
        }

        if (cacheCapacity is not (0 or 1 or 2 or 4))
        {
            throw new ArgumentOutOfRangeException(
                nameof(cacheCapacity),
                "The frozen cache capacities are 0, 1, 2, and 4 lines.");
        }

        if (policy == ValuationCachePolicy.BinDirectBest && cacheCapacity != 0)
        {
            throw new ArgumentException(
                "BIN_DIRECT_BEST cannot retain cache lines.",
                nameof(cacheCapacity));
        }

        Width = width;
        Policy = policy;
        CacheCapacity = cacheCapacity;
        MaximumMagnitude = (1UL << width) - 1UL;
        _divisors = divisors.ToArray();
        if (_divisors.Length == 0 ||
            _divisors.Length > MaximumCatalogueEntries ||
            _divisors.Any(divisor => divisor <= 1) ||
            _divisors.Distinct().Count() != _divisors.Length)
        {
            throw new ArgumentException(
                "A divisor catalogue must contain one to sixteen distinct values greater than one.",
                nameof(divisors));
        }

        _readOnlyDivisors = Array.AsReadOnly(_divisors);
        _allowMultiplicativePropagation = allowMultiplicativePropagation;
        _usesPrimeCatalogue = usesPrimeCatalogue;
        _cache = Enumerable.Range(0, cacheCapacity).Select(_ => new CacheLine()).ToArray();
    }

    /// <summary>
    /// Constructs the size-matched non-prime control. It uses the exact same
    /// query, frontier, replacement, invalidation, and generation machinery,
    /// but multiplicative frontier propagation is disabled because Euclid's
    /// lemma does not justify it for composite bases.
    /// </summary>
    public static DemandValuationService CreateCompositeControl(
        int width,
        ValuationCachePolicy policy,
        int cacheCapacity,
        IReadOnlyList<int> compositeDivisors)
    {
        ArgumentNullException.ThrowIfNull(compositeDivisors);
        return new DemandValuationService(
            width,
            policy,
            cacheCapacity,
            compositeDivisors,
            allowMultiplicativePropagation: false,
            usesPrimeCatalogue: false);
    }

    public int Width { get; }

    public ulong MaximumMagnitude { get; }

    public ValuationCachePolicy Policy { get; }

    public int CacheCapacity { get; }

    public IReadOnlyList<int> DivisorCatalogue => _readOnlyDivisors;

    public bool MultiplicativePropagationEnabled => _allowMultiplicativePropagation;

    public ValuationMetrics Metrics => _metrics.Snapshot();

    public ValueSlotSnapshot SnapshotSlot(int slot)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual((uint)slot, (uint)SlotCount);
        var state = _slots[slot];
        return new ValueSlotSnapshot(slot, state.Initialized, state.Magnitude, state.Generation);
    }

    public IReadOnlyList<ValuationCacheLineSnapshot> SnapshotCache() =>
        _cache
            .Select(
                (line, index) => new ValuationCacheLineSnapshot(
                    index,
                    line.Valid,
                    line.ContentKey ? line.ContentMagnitude : null,
                    line.Slot,
                    line.Generation,
                    line.PrimeIndex,
                    line.LowerBound,
                    line.Residual,
                    line.Terminal,
                    line.Infinite,
                    line.LastUseTick))
            .ToArray();

    /// <summary>
    /// Replays every currently applicable cache-line equation against the
    /// authoritative magnitude using this service's configured catalogue.
    /// Stale versioned lines are not accepted as current evidence.
    /// </summary>
    public bool ValidateCacheState(out string detail)
    {
        foreach (var line in _cache.Where(line => line.Valid))
        {
            if ((uint)line.PrimeIndex >= (uint)_divisors.Length)
            {
                detail = "A cache line has an out-of-catalogue divisor index.";
                return false;
            }

            ulong magnitude;
            if (line.ContentKey)
            {
                magnitude = line.ContentMagnitude;
            }
            else
            {
                if ((uint)line.Slot >= SlotCount)
                {
                    detail = "A frontier cache line has an invalid slot tag.";
                    return false;
                }

                var slot = _slots[line.Slot];
                if (!slot.Initialized || slot.Generation != line.Generation)
                {
                    continue;
                }

                magnitude = slot.Magnitude;
            }

            if (!TryValidateEquation(
                    magnitude,
                    _divisors[line.PrimeIndex],
                    line.LowerBound,
                    line.Residual,
                    line.Terminal,
                    line.Infinite,
                    out detail))
            {
                return false;
            }
        }

        detail = string.Empty;
        return true;
    }

    public ValuationOperationResult<ValuationMutationReceipt> Load(int slot, ulong magnitude)
    {
        var before = BeginInstruction();
        if (!IsValidSlot(slot))
        {
            return Reject<ValuationMutationReceipt>(before, ValuationFailure.InvalidSlot, "LOAD destination is outside the four-slot domain.");
        }

        if (magnitude > MaximumMagnitude)
        {
            return Reject<ValuationMutationReceipt>(before, ValuationFailure.MagnitudeOutOfRange, "LOAD magnitude does not fit the configured width.");
        }

        _metrics.Loads++;
        var receipt = CommitMagnitude(slot, magnitude, propagatedFrontiers: 0);
        return Succeed(before, receipt);
    }

    public ValuationOperationResult<PowerTestAnswer> TestPower(int slot, int prime, int threshold)
    {
        var before = BeginInstruction();
        if (!TryValidateQuery(slot, prime, out var primeIndex, out var failure, out var detail))
        {
            return Reject<PowerTestAnswer>(before, failure, detail);
        }

        if (threshold < 0)
        {
            return Reject<PowerTestAnswer>(before, ValuationFailure.InvalidThreshold, "A valuation threshold cannot be negative.");
        }

        if (threshold == 0)
        {
            var state = _slots[slot];
            return Succeed(
                before,
                new PowerTestAnswer(
                    true,
                    threshold,
                    0,
                    state.Magnitude,
                    Terminal: false,
                    Infinite: state.Magnitude == 0));
        }

        var resolution = Resolve(slot, primeIndex, requireTerminal: false, threshold);
        var atLeast = resolution.State.Infinite || resolution.State.LowerBound >= threshold;
        if (resolution.AnswerWasCacheResident)
        {
            if (atLeast)
            {
                _metrics.PositiveCacheHits++;
            }
            else
            {
                _metrics.NegativeCacheHits++;
            }
        }

        return Succeed(
            before,
            new PowerTestAnswer(
                atLeast,
                threshold,
                resolution.State.LowerBound,
                resolution.State.Residual,
                resolution.State.Terminal,
                resolution.State.Infinite));
    }

    public ValuationOperationResult<ExactValuationAnswer> Valuation(int slot, int prime)
    {
        var before = BeginInstruction();
        if (!TryValidateQuery(slot, prime, out var primeIndex, out var failure, out var detail))
        {
            return Reject<ExactValuationAnswer>(before, failure, detail);
        }

        var resolution = Resolve(slot, primeIndex, requireTerminal: true, threshold: int.MaxValue);
        return Succeed(
            before,
            new ExactValuationAnswer(
                resolution.State.LowerBound,
                resolution.State.Residual,
                resolution.State.Infinite));
    }

    public ValuationOperationResult<ExactValuationAnswer> StripAll(int slot, int prime)
    {
        // STRIP_ALL returns the exact terminal residual; it does not overwrite
        // the authoritative source magnitude.
        var before = BeginInstruction();
        if (!TryValidateQuery(slot, prime, out var primeIndex, out var failure, out var detail))
        {
            return Reject<ExactValuationAnswer>(before, failure, detail);
        }

        var resolution = Resolve(slot, primeIndex, requireTerminal: true, threshold: int.MaxValue);
        return Succeed(
            before,
            new ExactValuationAnswer(
                resolution.State.LowerBound,
                resolution.State.Residual,
                resolution.State.Infinite));
    }

    public ValuationOperationResult<ValuationMutationReceipt> Add(
        int destination,
        int left,
        int right)
    {
        var before = BeginInstruction();
        if (!TryValidateArithmeticSlots(destination, left, right, out var failure, out var detail))
        {
            return Reject<ValuationMutationReceipt>(before, failure, detail);
        }

        var leftMagnitude = _slots[left].Magnitude;
        var rightMagnitude = _slots[right].Magnitude;
        var sum = leftMagnitude + rightMagnitude;
        if (sum > MaximumMagnitude)
        {
            return Reject<ValuationMutationReceipt>(before, ValuationFailure.Overflow, "ADD result does not fit the configured width.");
        }

        _metrics.Additions++;
        var receipt = CommitMagnitude(destination, sum, propagatedFrontiers: 0);
        return Succeed(before, receipt);
    }

    public ValuationOperationResult<ValuationMutationReceipt> Multiply(
        int destination,
        int left,
        int right)
    {
        var before = BeginInstruction();
        if (!TryValidateArithmeticSlots(destination, left, right, out var failure, out var detail))
        {
            return Reject<ValuationMutationReceipt>(before, failure, detail);
        }

        var leftMagnitude = _slots[left].Magnitude;
        var rightMagnitude = _slots[right].Magnitude;
        if (rightMagnitude != 0 && leftMagnitude > MaximumMagnitude / rightMagnitude)
        {
            return Reject<ValuationMutationReceipt>(before, ValuationFailure.Overflow, "MUL result does not fit the configured width.");
        }

        var product = leftMagnitude * rightMagnitude;
        var nextGeneration = NextGeneration(destination);
        var pending = _allowMultiplicativePropagation &&
            Policy == ValuationCachePolicy.BinPrimeFrontierPropK &&
            CacheCapacity > 0
            ? PrepareMultiplyPropagation(destination, nextGeneration, left, right, product)
            : [];

        _metrics.Multiplications++;
        var receipt = CommitMagnitude(destination, product, pending.Count);
        WriteTransferredFrontiers(pending);
        return Succeed(before, receipt);
    }

    public ValuationOperationResult<ValuationMutationReceipt> MultiplyByPrime(
        int destination,
        int source,
        int prime)
    {
        var before = BeginInstruction();
        if (!IsValidSlot(destination) || !IsValidSlot(source))
        {
            return Reject<ValuationMutationReceipt>(before, ValuationFailure.InvalidSlot, "MUL_BY_PRIME slot is outside the four-slot domain.");
        }

        if (!_slots[source].Initialized)
        {
            return Reject<ValuationMutationReceipt>(before, ValuationFailure.UninitializedSlot, "MUL_BY_PRIME source has not been loaded.");
        }

        if (!_usesPrimeCatalogue || !Build005PrimeCatalogue.TryGetIndex(prime, out var primeIndex))
        {
            return Reject<ValuationMutationReceipt>(before, ValuationFailure.InvalidPrime, "MUL_BY_PRIME accepts only the frozen prime catalogue.");
        }

        var sourceMagnitude = _slots[source].Magnitude;
        if (sourceMagnitude != 0 && sourceMagnitude > MaximumMagnitude / (ulong)prime)
        {
            return Reject<ValuationMutationReceipt>(before, ValuationFailure.Overflow, "MUL_BY_PRIME result does not fit the configured width.");
        }

        var product = sourceMagnitude * (ulong)prime;
        var nextGeneration = NextGeneration(destination);
        var pending = _allowMultiplicativePropagation &&
            Policy == ValuationCachePolicy.BinPrimeFrontierPropK &&
            CacheCapacity > 0
            ? PreparePrimeMultiplyPropagation(
                destination,
                nextGeneration,
                source,
                primeIndex,
                product)
            : [];

        _metrics.MultiplyByPrimeOperations++;
        var receipt = CommitMagnitude(destination, product, pending.Count);
        WriteTransferredFrontiers(pending);
        return Succeed(before, receipt);
    }

    private QueryResolution Resolve(
        int slot,
        int primeIndex,
        bool requireTerminal,
        int threshold)
    {
        var source = _slots[slot];
        var found = false;
        var lineIndex = -1;
        FrontierState frontier;

        switch (Policy)
        {
            case ValuationCachePolicy.BinContentAnswerLruK:
                found = TryLookupContent(source.Magnitude, primeIndex, out lineIndex, out frontier);
                break;
            case ValuationCachePolicy.BinFrontierNoPropK:
            case ValuationCachePolicy.BinPrimeFrontierPropK:
                found = TryLookupFrontier(slot, source.Generation, primeIndex, out lineIndex, out frontier);
                break;
            default:
                frontier = default;
                break;
        }

        if (!found)
        {
            frontier = NewFrontier(slot, source.Generation, primeIndex, source.Magnitude);
        }

        var answerWasCacheResident = found &&
            (frontier.Infinite || frontier.Terminal || (!requireTerminal && frontier.LowerBound >= threshold));

        if (!answerWasCacheResident)
        {
            if (!found && (frontier.Infinite || frontier.Terminal))
            {
                _metrics.TerminalCertificatesEarned++;
            }
            else if (!frontier.Infinite && !frontier.Terminal)
            {
                var beforeTerminal = frontier.Terminal;
                var beforeLowerBound = frontier.LowerBound;
                frontier = Refine(frontier, requireTerminal, threshold);
                if (!beforeTerminal && frontier.Terminal)
                {
                    _metrics.TerminalCertificatesEarned++;
                }
                else if (!frontier.Terminal && frontier.LowerBound > beforeLowerBound)
                {
                    _metrics.LowerBoundCertificatesEarned++;
                }
            }

            if (Policy == ValuationCachePolicy.BinContentAnswerLruK)
            {
                if (frontier.Terminal || frontier.Infinite)
                {
                    WriteContent(source.Magnitude, primeIndex, frontier, lineIndex);
                }
            }
            else if (Policy is ValuationCachePolicy.BinFrontierNoPropK or ValuationCachePolicy.BinPrimeFrontierPropK)
            {
                WriteFrontier(frontier, lineIndex);
            }
        }

        return new QueryResolution(frontier, answerWasCacheResident);
    }

    private FrontierState Refine(FrontierState frontier, bool requireTerminal, int threshold)
    {
        var didInspect = false;
        var lowerBound = frontier.LowerBound;
        var residual = frontier.Residual;
        var terminal = frontier.Terminal;
        var prime = _divisors[frontier.PrimeIndex];

        if (prime == 2)
        {
            _metrics.CtzCalls++;
            while (!terminal && (requireTerminal || lowerBound < threshold))
            {
                didInspect = true;
                _metrics.CtzBitInspections++;
                if ((residual & 1UL) != 0)
                {
                    terminal = true;
                    _metrics.FailedDivisibilityProbes++;
                    break;
                }

                residual >>= 1;
                lowerBound++;
                _metrics.ExactDivisions++;
            }
        }
        else
        {
            var divisor = (ulong)prime;
            while (!terminal && (requireTerminal || lowerBound < threshold))
            {
                didInspect = true;
                _metrics.DivModCalls++;
                var remainder = residual % divisor;
                if (remainder != 0)
                {
                    terminal = true;
                    _metrics.FailedDivisibilityProbes++;
                    break;
                }

                residual /= divisor;
                lowerBound++;
                _metrics.ExactDivisions++;
            }
        }

        if (didInspect)
        {
            _metrics.FrontierRefinements++;
        }

        return frontier with
        {
            LowerBound = lowerBound,
            Residual = residual,
            Terminal = terminal,
        };
    }

    private static FrontierState NewFrontier(
        int slot,
        byte generation,
        int primeIndex,
        ulong magnitude)
    {
        if (magnitude == 0)
        {
            return new FrontierState(slot, generation, primeIndex, 0, 0, Terminal: true, Infinite: true);
        }

        if (magnitude == 1)
        {
            return new FrontierState(slot, generation, primeIndex, 0, 1, Terminal: true, Infinite: false);
        }

        return new FrontierState(slot, generation, primeIndex, 0, magnitude, Terminal: false, Infinite: false);
    }

    private bool TryLookupFrontier(
        int slot,
        byte generation,
        int primeIndex,
        out int lineIndex,
        out FrontierState frontier)
    {
        _metrics.CacheLookups++;
        lineIndex = -1;
        frontier = default;

        for (var index = 0; index < _cache.Length; index++)
        {
            var line = _cache[index];
            if (!line.Valid)
            {
                continue;
            }

            _metrics.CacheTagComparisons++;
            if (!line.ContentKey && line.Slot == slot && line.PrimeIndex == primeIndex)
            {
                if (line.Generation != generation)
                {
                    _metrics.RejectedStaleHitAttempts++;
                    continue;
                }

                lineIndex = index;
            }
        }

        if (lineIndex < 0)
        {
            _metrics.CacheMisses++;
            return false;
        }

        _metrics.CacheHits++;
        var hit = _cache[lineIndex];
        Touch(hit);
        frontier = hit.ToFrontier();
        EnsureValidInternalFrontier(frontier, _slots[slot].Magnitude);
        return true;
    }

    private bool TryLookupContent(
        ulong magnitude,
        int primeIndex,
        out int lineIndex,
        out FrontierState frontier)
    {
        _metrics.CacheLookups++;
        lineIndex = -1;
        frontier = default;

        for (var index = 0; index < _cache.Length; index++)
        {
            var line = _cache[index];
            if (!line.Valid)
            {
                continue;
            }

            _metrics.CacheTagComparisons++;
            if (line.ContentKey && line.ContentMagnitude == magnitude && line.PrimeIndex == primeIndex)
            {
                lineIndex = index;
            }
        }

        if (lineIndex < 0)
        {
            _metrics.CacheMisses++;
            return false;
        }

        _metrics.CacheHits++;
        var hit = _cache[lineIndex];
        Touch(hit);
        frontier = new FrontierState(
            Slot: -1,
            Generation: 0,
            primeIndex,
            hit.LowerBound,
            hit.Residual,
            hit.Terminal,
            hit.Infinite);
        return true;
    }

    private void WriteFrontier(FrontierState frontier, int knownLineIndex = -1)
    {
        if (_cache.Length == 0)
        {
            return;
        }

        EnsureValidInternalFrontier(frontier, _slots[frontier.Slot].Magnitude);
        var targetIndex = knownLineIndex >= 0
            ? knownLineIndex
            : FindFrontierLine(frontier.Slot, frontier.Generation, frontier.PrimeIndex);
        if (targetIndex >= 0)
        {
            var target = _cache[targetIndex];
            target.AssignFrontier(frontier);
            Touch(target);
            _metrics.CacheUpdates++;
            return;
        }

        targetIndex = SelectFillTarget();
        var line = _cache[targetIndex];
        if (line.Valid)
        {
            _metrics.CacheEvictions++;
        }

        line.AssignFrontier(frontier);
        Touch(line);
        _metrics.CacheFills++;
    }

    private void WriteContent(
        ulong magnitude,
        int primeIndex,
        FrontierState frontier,
        int knownLineIndex = -1)
    {
        if (_cache.Length == 0)
        {
            return;
        }

        if (!frontier.Terminal && !frontier.Infinite)
        {
            throw new InvalidOperationException("Content control cannot store a partial frontier.");
        }

        var targetIndex = knownLineIndex >= 0
            ? knownLineIndex
            : FindContentLine(magnitude, primeIndex);
        if (targetIndex >= 0)
        {
            var target = _cache[targetIndex];
            target.AssignContent(magnitude, primeIndex, frontier);
            Touch(target);
            _metrics.CacheUpdates++;
            return;
        }

        targetIndex = SelectFillTarget();
        var line = _cache[targetIndex];
        if (line.Valid)
        {
            _metrics.CacheEvictions++;
        }

        line.AssignContent(magnitude, primeIndex, frontier);
        Touch(line);
        _metrics.CacheFills++;
    }

    private int FindFrontierLine(int slot, byte generation, int primeIndex)
    {
        for (var index = 0; index < _cache.Length; index++)
        {
            var line = _cache[index];
            if (line.Valid &&
                !line.ContentKey &&
                line.Slot == slot &&
                line.Generation == generation &&
                line.PrimeIndex == primeIndex)
            {
                return index;
            }
        }

        return -1;
    }

    private int FindContentLine(ulong magnitude, int primeIndex)
    {
        for (var index = 0; index < _cache.Length; index++)
        {
            var line = _cache[index];
            if (line.Valid &&
                line.ContentKey &&
                line.ContentMagnitude == magnitude &&
                line.PrimeIndex == primeIndex)
            {
                return index;
            }
        }

        return -1;
    }

    private int SelectFillTarget()
    {
        for (var index = 0; index < _cache.Length; index++)
        {
            if (!_cache[index].Valid)
            {
                return index;
            }
        }

        var selected = 0;
        for (var index = 1; index < _cache.Length; index++)
        {
            if (_cache[index].LastUseTick < _cache[selected].LastUseTick)
            {
                selected = index;
            }
        }

        return selected;
    }

    private List<FrontierState> PrepareMultiplyPropagation(
        int destination,
        byte generation,
        int left,
        int right,
        ulong product)
    {
        var leftFrontiers = CaptureCurrentFrontiers(left);
        var rightFrontiers = left == right
            ? leftFrontiers
            : CaptureCurrentFrontiers(right);
        var pending = new List<FrontierState>();

        foreach (var primeIndex in leftFrontiers.Keys.Intersect(rightFrontiers.Keys).Order())
        {
            var first = leftFrontiers[primeIndex];
            var second = rightFrontiers[primeIndex];
            FrontierState combined;
            if (product == 0)
            {
                combined = new FrontierState(
                    destination,
                    generation,
                    primeIndex,
                    0,
                    0,
                    Terminal: true,
                    Infinite: true);
            }
            else
            {
                _metrics.PropagationExponentAdds++;
                _metrics.PropagationResidualMultiplies++;
                combined = new FrontierState(
                    destination,
                    generation,
                    primeIndex,
                    checked(first.LowerBound + second.LowerBound),
                    checked(first.Residual * second.Residual),
                    Terminal: first.Terminal && second.Terminal,
                    Infinite: false);
            }

            EnsureValidInternalFrontier(combined, product);
            pending.Add(combined);
        }

        return pending;
    }

    private List<FrontierState> PreparePrimeMultiplyPropagation(
        int destination,
        byte generation,
        int source,
        int multiplierPrimeIndex,
        ulong product)
    {
        var sourceFrontiers = CaptureCurrentFrontiers(source);
        var pending = new List<FrontierState>();

        foreach (var pair in sourceFrontiers.OrderBy(pair => pair.Key))
        {
            var sourceFrontier = pair.Value;
            FrontierState transferred;
            if (product == 0)
            {
                transferred = new FrontierState(
                    destination,
                    generation,
                    pair.Key,
                    0,
                    0,
                    Terminal: true,
                    Infinite: true);
            }
            else if (pair.Key == multiplierPrimeIndex)
            {
                _metrics.PropagationExponentAdds++;
                transferred = new FrontierState(
                    destination,
                    generation,
                    pair.Key,
                    checked(sourceFrontier.LowerBound + 1),
                    sourceFrontier.Residual,
                    sourceFrontier.Terminal,
                    Infinite: false);
            }
            else
            {
                _metrics.PropagationResidualMultiplies++;
                transferred = new FrontierState(
                    destination,
                    generation,
                    pair.Key,
                    sourceFrontier.LowerBound,
                    checked(sourceFrontier.Residual * (ulong)_divisors[multiplierPrimeIndex]),
                    sourceFrontier.Terminal,
                    Infinite: false);
            }

            EnsureValidInternalFrontier(transferred, product);
            pending.Add(transferred);
        }

        if (!sourceFrontiers.ContainsKey(multiplierPrimeIndex))
        {
            var constructed = product == 0
                ? new FrontierState(
                    destination,
                    generation,
                    multiplierPrimeIndex,
                    0,
                    0,
                    Terminal: true,
                    Infinite: true)
                : new FrontierState(
                    destination,
                    generation,
                    multiplierPrimeIndex,
                    1,
                    _slots[source].Magnitude,
                    Terminal: false,
                    Infinite: false);
            EnsureValidInternalFrontier(constructed, product);
            pending.Add(constructed);
        }

        return pending.OrderBy(frontier => frontier.PrimeIndex).ToList();
    }

    private Dictionary<int, FrontierState> CaptureCurrentFrontiers(int slot)
    {
        _metrics.CacheLookups++;
        var generation = _slots[slot].Generation;
        var captured = new Dictionary<int, FrontierState>();
        var hitLines = new List<CacheLine>();

        foreach (var line in _cache)
        {
            if (!line.Valid)
            {
                continue;
            }

            _metrics.CacheTagComparisons++;
            if (line.ContentKey || line.Slot != slot)
            {
                continue;
            }

            if (line.Generation != generation)
            {
                _metrics.RejectedStaleHitAttempts++;
                continue;
            }

            var frontier = line.ToFrontier();
            EnsureValidInternalFrontier(frontier, _slots[slot].Magnitude);
            captured.Add(frontier.PrimeIndex, frontier);
            hitLines.Add(line);
        }

        if (captured.Count == 0)
        {
            _metrics.CacheMisses++;
        }
        else
        {
            _metrics.CacheHits++;
            foreach (var line in hitLines)
            {
                Touch(line);
            }
        }

        return captured;
    }

    private void WriteTransferredFrontiers(IEnumerable<FrontierState> frontiers)
    {
        foreach (var frontier in frontiers)
        {
            WriteFrontier(frontier);
            _metrics.CacheTransfers++;
            if (frontier.Terminal || frontier.Infinite)
            {
                _metrics.TerminalCertificatesPropagated++;
            }
            else
            {
                _metrics.LowerBoundCertificatesPropagated++;
            }
        }
    }

    private ValuationMutationReceipt CommitMagnitude(
        int destination,
        ulong magnitude,
        int propagatedFrontiers)
    {
        var slot = _slots[destination];
        var previousGeneration = slot.Generation;
        InvalidateCurrentFrontiers(destination, previousGeneration);

        var generationWrapped = previousGeneration == byte.MaxValue;
        if (generationWrapped &&
            Policy is (ValuationCachePolicy.BinFrontierNoPropK or
                ValuationCachePolicy.BinPrimeFrontierPropK) &&
            CacheCapacity > 0)
        {
            FlushCacheForGenerationWrap();
        }

        slot.Generation = unchecked((byte)(previousGeneration + 1));
        slot.Magnitude = magnitude;
        slot.Initialized = true;
        return new ValuationMutationReceipt(
            destination,
            magnitude,
            previousGeneration,
            slot.Generation,
            generationWrapped,
            propagatedFrontiers);
    }

    private void InvalidateCurrentFrontiers(int slot, byte generation)
    {
        if (Policy is not (ValuationCachePolicy.BinFrontierNoPropK or ValuationCachePolicy.BinPrimeFrontierPropK))
        {
            return;
        }

        _metrics.CacheInvalidations += _cache.LongCount(
            line => line.Valid &&
                !line.ContentKey &&
                line.Slot == slot &&
                line.Generation == generation);
    }

    private void FlushCacheForGenerationWrap()
    {
        var validLines = _cache.LongCount(line => line.Valid);
        foreach (var line in _cache)
        {
            line.Clear();
        }

        _metrics.GenerationWrapFlushes++;
        _metrics.CacheLinesFlushed += validLines;
    }

    private byte NextGeneration(int destination) =>
        unchecked((byte)(_slots[destination].Generation + 1));

    private void Touch(CacheLine line)
    {
        _lastUseTick++;
        line.LastUseTick = _lastUseTick;
    }

    private bool TryValidateQuery(
        int slot,
        int prime,
        out int primeIndex,
        out ValuationFailure failure,
        out string detail)
    {
        primeIndex = -1;
        if (!IsValidSlot(slot))
        {
            failure = ValuationFailure.InvalidSlot;
            detail = "Query slot is outside the four-slot domain.";
            return false;
        }

        if (!_slots[slot].Initialized)
        {
            failure = ValuationFailure.UninitializedSlot;
            detail = "Query slot has not been loaded.";
            return false;
        }

        primeIndex = Array.IndexOf(_divisors, prime);
        if (primeIndex < 0)
        {
            failure = ValuationFailure.InvalidPrime;
            detail = "Queries accept only the configured frozen divisor catalogue.";
            return false;
        }

        failure = ValuationFailure.None;
        detail = string.Empty;
        return true;
    }

    private bool TryValidateArithmeticSlots(
        int destination,
        int left,
        int right,
        out ValuationFailure failure,
        out string detail)
    {
        if (!IsValidSlot(destination) || !IsValidSlot(left) || !IsValidSlot(right))
        {
            failure = ValuationFailure.InvalidSlot;
            detail = "Arithmetic slot is outside the four-slot domain.";
            return false;
        }

        if (!_slots[left].Initialized || !_slots[right].Initialized)
        {
            failure = ValuationFailure.UninitializedSlot;
            detail = "Arithmetic source has not been loaded.";
            return false;
        }

        failure = ValuationFailure.None;
        detail = string.Empty;
        return true;
    }

    private static bool IsValidSlot(int slot) => (uint)slot < SlotCount;

    private void EnsureValidInternalFrontier(FrontierState frontier, ulong magnitude)
    {
        if ((uint)frontier.Slot >= SlotCount ||
            (uint)frontier.PrimeIndex >= (uint)_divisors.Length ||
            frontier.LowerBound < 0)
        {
            throw new InvalidOperationException("Internal malformed frontier tag or exponent.");
        }

        if (!TryValidateEquation(
                magnitude,
                _divisors[frontier.PrimeIndex],
                frontier.LowerBound,
                frontier.Residual,
                frontier.Terminal,
                frontier.Infinite,
                out var detail))
        {
            throw new InvalidOperationException($"Internal malformed frontier: {detail}");
        }
    }

    private static bool TryValidateEquation(
        ulong magnitude,
        int divisor,
        int lowerBound,
        ulong residual,
        bool terminal,
        bool infinite,
        out string detail)
    {
        if (lowerBound < 0)
        {
            detail = "The lower bound is negative.";
            return false;
        }

        if (infinite)
        {
            var valid = magnitude == 0 && lowerBound == 0 && residual == 0 && terminal;
            detail = valid
                ? string.Empty
                : "Only the canonical zero payload may carry an infinite terminal valuation.";
            return valid;
        }

        if (magnitude == 0 || residual == 0)
        {
            detail = "A finite frontier has a zero magnitude or residual.";
            return false;
        }

        var quotient = magnitude;
        var unsignedDivisor = (ulong)divisor;
        for (var exponent = 0; exponent < lowerBound; exponent++)
        {
            if (quotient % unsignedDivisor != 0)
            {
                detail = "The exponent exceeds the exact divisible prefix.";
                return false;
            }

            quotient /= unsignedDivisor;
        }

        var validEquation = quotient == residual && (!terminal || residual % unsignedDivisor != 0);
        detail = validEquation ? string.Empty : "The residual equation or terminal flag is malformed.";
        return validEquation;
    }

    private ValuationMetrics BeginInstruction()
    {
        var before = _metrics.Snapshot();
        _metrics.RequestedInstructions++;
        return before;
    }

    private ValuationOperationResult<T> Reject<T>(
        ValuationMetrics before,
        ValuationFailure failure,
        string detail)
        where T : class
    {
        _metrics.RejectedOperations++;
        return new ValuationOperationResult<T>(
            false,
            failure,
            detail,
            null,
            ValuationMetrics.Difference(_metrics.Snapshot(), before));
    }

    private ValuationOperationResult<T> Succeed<T>(ValuationMetrics before, T value)
        where T : class =>
        new(
            true,
            ValuationFailure.None,
            string.Empty,
            value,
            ValuationMetrics.Difference(_metrics.Snapshot(), before));

    private sealed class SlotState
    {
        public bool Initialized { get; set; }
        public ulong Magnitude { get; set; }
        public byte Generation { get; set; }
    }

    private sealed class CacheLine
    {
        public bool Valid { get; set; }
        public bool ContentKey { get; set; }
        public ulong ContentMagnitude { get; set; }
        public int Slot { get; set; } = -1;
        public byte Generation { get; set; }
        public int PrimeIndex { get; set; } = -1;
        public int LowerBound { get; set; }
        public ulong Residual { get; set; }
        public bool Terminal { get; set; }
        public bool Infinite { get; set; }
        public ulong LastUseTick { get; set; }

        public FrontierState ToFrontier() =>
            new(Slot, Generation, PrimeIndex, LowerBound, Residual, Terminal, Infinite);

        public void AssignFrontier(FrontierState frontier)
        {
            Valid = true;
            ContentKey = false;
            ContentMagnitude = 0;
            Slot = frontier.Slot;
            Generation = frontier.Generation;
            PrimeIndex = frontier.PrimeIndex;
            LowerBound = frontier.LowerBound;
            Residual = frontier.Residual;
            Terminal = frontier.Terminal;
            Infinite = frontier.Infinite;
        }

        public void AssignContent(ulong magnitude, int primeIndex, FrontierState frontier)
        {
            Valid = true;
            ContentKey = true;
            ContentMagnitude = magnitude;
            Slot = -1;
            Generation = 0;
            PrimeIndex = primeIndex;
            LowerBound = frontier.LowerBound;
            Residual = frontier.Residual;
            Terminal = frontier.Terminal;
            Infinite = frontier.Infinite;
        }

        public void Clear()
        {
            Valid = false;
            ContentKey = false;
            ContentMagnitude = 0;
            Slot = -1;
            Generation = 0;
            PrimeIndex = -1;
            LowerBound = 0;
            Residual = 0;
            Terminal = false;
            Infinite = false;
            LastUseTick = 0;
        }
    }

    private readonly record struct FrontierState(
        int Slot,
        byte Generation,
        int PrimeIndex,
        int LowerBound,
        ulong Residual,
        bool Terminal,
        bool Infinite)
    {
        public ValuationFrontier ToPublic() =>
            new(
                Valid: true,
                Slot,
                Generation,
                PrimeIndex,
                LowerBound,
                Residual,
                Terminal,
                Infinite);
    }

    private readonly record struct QueryResolution(
        FrontierState State,
        bool AnswerWasCacheResident);

    private sealed class MetricCounter
    {
        public long RequestedInstructions;
        public long Loads;
        public long Additions;
        public long Multiplications;
        public long MultiplyByPrimeOperations;
        public long RejectedOperations;
        public long CtzCalls;
        public long CtzBitInspections;
        public long DivModCalls;
        public long ExactDivisions;
        public long FailedDivisibilityProbes;
        public long FrontierRefinements;
        public long CacheLookups;
        public long CacheTagComparisons;
        public long CacheHits;
        public long PositiveCacheHits;
        public long NegativeCacheHits;
        public long CacheMisses;
        public long CacheFills;
        public long CacheUpdates;
        public long CacheEvictions;
        public long CacheInvalidations;
        public long CacheTransfers;
        public long RejectedStaleHitAttempts;
        public long GenerationWrapFlushes;
        public long CacheLinesFlushed;
        public long TerminalCertificatesEarned;
        public long LowerBoundCertificatesEarned;
        public long TerminalCertificatesPropagated;
        public long LowerBoundCertificatesPropagated;
        public long PropagationExponentAdds;
        public long PropagationResidualMultiplies;

        public ValuationMetrics Snapshot() =>
            new()
            {
                RequestedInstructions = RequestedInstructions,
                Loads = Loads,
                Additions = Additions,
                Multiplications = Multiplications,
                MultiplyByPrimeOperations = MultiplyByPrimeOperations,
                RejectedOperations = RejectedOperations,
                CtzCalls = CtzCalls,
                CtzBitInspections = CtzBitInspections,
                DivModCalls = DivModCalls,
                ExactDivisions = ExactDivisions,
                FailedDivisibilityProbes = FailedDivisibilityProbes,
                FrontierRefinements = FrontierRefinements,
                CacheLookups = CacheLookups,
                CacheTagComparisons = CacheTagComparisons,
                CacheHits = CacheHits,
                PositiveCacheHits = PositiveCacheHits,
                NegativeCacheHits = NegativeCacheHits,
                CacheMisses = CacheMisses,
                CacheFills = CacheFills,
                CacheUpdates = CacheUpdates,
                CacheEvictions = CacheEvictions,
                CacheInvalidations = CacheInvalidations,
                CacheTransfers = CacheTransfers,
                RejectedStaleHitAttempts = RejectedStaleHitAttempts,
                GenerationWrapFlushes = GenerationWrapFlushes,
                CacheLinesFlushed = CacheLinesFlushed,
                TerminalCertificatesEarned = TerminalCertificatesEarned,
                LowerBoundCertificatesEarned = LowerBoundCertificatesEarned,
                TerminalCertificatesPropagated = TerminalCertificatesPropagated,
                LowerBoundCertificatesPropagated = LowerBoundCertificatesPropagated,
                PropagationExponentAdds = PropagationExponentAdds,
                PropagationResidualMultiplies = PropagationResidualMultiplies,
            };
    }
}
