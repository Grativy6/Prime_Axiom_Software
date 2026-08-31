using System.Collections.ObjectModel;

namespace PrimeAxiom.Cli;

internal enum Build005EventKind
{
    Load,
    TestPower,
    Valuation,
    StripAll,
    Multiply,
    MultiplyByPrime,
    Add,
    Overwrite,
    ProducerPrimeFact,
    RationalReduce,
    CompositeValuationControl,
}

internal sealed record Build005TraceEvent(
    Build005EventKind Kind,
    int Destination = 0,
    int Left = 0,
    int Right = 0,
    ulong Magnitude = 0,
    int Divisor = 0,
    int Threshold = 0);

internal sealed record Build005Trace(
    string Family,
    string TraceId,
    int Width,
    string SourceRegime,
    string OutputObligation,
    IReadOnlyList<Build005TraceEvent> Events,
    bool Hostile = false,
    bool PrimeAttributionEligible = false,
    bool SearchRepaymentEligible = true);

internal static class Build005Workloads
{
    public static IReadOnlyList<Build005Trace> Create(int width)
    {
        if (width is not 8 and not 16 and not 32)
        {
            throw new ArgumentOutOfRangeException(nameof(width));
        }

        var traces = new List<Build005Trace>
        {
            StaticReuse(width),
            ThresholdStaircase(width),
            RationalCancel(width),
            PersistentFilter(width),
            StreamingFilter(width),
            SmoothStrip(width),
            MultiplicativeDag(width),
            ProducerFactored(width),
            AdditionMutation(width),
            PhaseShift(width),
            CompositeControl(width),
            RadixTwo(width),
            SlotThrash(width),
            PrimeThrash(width),
            MutationAfterFill(width),
            SpeculationPoison(width),
            BoundaryAndFailure(width),
            GenerationWrap(width),
        };
        return new ReadOnlyCollection<Build005Trace>(traces);
    }

    private static Build005Trace StaticReuse(int width)
    {
        var value = CheckedPowerTimes(width, 3, width == 8 ? 3 : width == 16 ? 8 : 16, 5);
        var events = new List<Build005TraceEvent> { Load(0, value) };
        for (var index = 0; index < 24; index++)
        {
            events.Add(index % 3 == 0 ? Test(0, 3, 2 + (index % 6)) : Valuation(0, 3));
        }

        events.Add(Overwrite(0, Maximum(width)));
        for (var index = 0; index < 12; index++)
        {
            events.Add(Valuation(0, 29));
        }

        return Trace("STATIC_REUSE", "unchanged-positive-and-negative-hits", width, events);
    }

    private static Build005Trace ThresholdStaircase(int width)
    {
        var exponent = width == 8 ? 5 : width == 16 ? 9 : 18;
        var value = CheckedPowerTimes(width, 3, exponent, 1);
        var events = new List<Build005TraceEvent> { Load(0, value) };
        for (var threshold = 1; threshold <= exponent + 2; threshold++)
        {
            events.Add(Test(0, 3, threshold));
        }

        for (var threshold = exponent + 2; threshold >= 1; threshold--)
        {
            events.Add(Test(0, 3, threshold));
        }

        var order = Enumerable.Range(1, exponent + 2).ToArray();
        var random = new Build005SplitMix64(Build005Protocol.DeriveSeed(width, "THRESHOLD_STAIRCASE"));
        for (var index = order.Length - 1; index > 0; index--)
        {
            var swap = (int)random.NextBelow((ulong)(index + 1));
            (order[index], order[swap]) = (order[swap], order[index]);
        }

        events.AddRange(order.Select(threshold => Test(0, 3, threshold)));
        events.Add(Valuation(0, 3));
        return Trace("THRESHOLD_STAIRCASE", "ascending-descending-shuffled", width, events);
    }

    private static Build005Trace RationalCancel(int width)
    {
        var factor = width == 8 ? 1UL : width == 16 ? 13UL : 5_005UL;
        var left = Fit(width, 60UL * factor);
        var right = Fit(width, 84UL * factor);
        var events = new List<Build005TraceEvent>
        {
            Load(0, left),
            Load(1, right),
        };
        foreach (var prime in new[] { 2, 3, 5, 7 })
        {
            events.Add(Valuation(0, prime));
            events.Add(Valuation(1, prime));
            events.Add(Valuation(0, prime));
            events.Add(Valuation(1, prime));
        }

        events.Add(new Build005TraceEvent(Build005EventKind.RationalReduce, Left: 0, Right: 1));
        return Trace("RATIONAL_CANCEL", "selected-primes-plus-full-gcd", width, events);
    }

    private static Build005Trace PersistentFilter(int width)
    {
        var events = new List<Build005TraceEvent>();
        var values = width == 8
            ? new ulong[] { 180, 189, 221, 242 }
            : width == 16
                ? new ulong[] { 27_720, 30_030, 32_760, 46_189 }
                : new ulong[] { 216_216_000, 245_945_700, 323_484_661, 735_134_400 };
        for (var slot = 0; slot < values.Length; slot++)
        {
            events.Add(Load(slot, Fit(width, values[slot])));
        }

        var random = new Build005SplitMix64(Build005Protocol.DeriveSeed(width, "DIVISIBILITY_FILTER_PERSISTENT"));
        for (var index = 0; index < 256; index++)
        {
            var slot = (int)random.NextBelow(4);
            var prime = Build005Protocol.PrimeCatalog[(int)random.NextBelow((ulong)Build005Protocol.PrimeCatalog.Length)];
            events.Add(Test(slot, prime, 1 + (int)random.NextBelow(3)));
        }

        return Trace("DIVISIBILITY_FILTER_PERSISTENT", "four-slot-reuse", width, events);
    }

    private static Build005Trace StreamingFilter(int width)
    {
        var events = new List<Build005TraceEvent>();
        var random = new Build005SplitMix64(Build005Protocol.DeriveSeed(width, "DIVISIBILITY_FILTER_STREAM"));
        var maximum = Maximum(width);
        for (var index = 0; index < 128; index++)
        {
            var value = 1 + random.NextBelow(maximum);
            var prime = Build005Protocol.PrimeCatalog[(int)random.NextBelow((ulong)Build005Protocol.PrimeCatalog.Length)];
            events.Add(Load(0, value));
            events.Add(Test(0, prime, 1));
        }

        return Trace("DIVISIBILITY_FILTER_STREAM", "one-query-per-value", width, events);
    }

    private static Build005Trace SmoothStrip(int width)
    {
        var values = new ulong[]
        {
            0, 1, 2, 3, 6, 30, 64, 210, 231, 251, 253, 255,
            30_030, 32_767, 65_521, 65_535,
            2_147_483_647, 3_735_928_559, 4_294_967_291,
        };
        var events = new List<Build005TraceEvent>();
        foreach (var value in values.Where(value => value <= Maximum(width)))
        {
            events.Add(Load(0, value));
            foreach (var prime in Build005Protocol.PrimeCatalog)
            {
                events.Add(Strip(0, prime));
            }
        }

        return Trace("SMOOTH_STRIP", "smooth-near-smooth-rough", width, events);
    }

    private static Build005Trace MultiplicativeDag(int width)
    {
        var events = new List<Build005TraceEvent>
        {
            Load(0, 6),
            Load(1, 10),
            Load(2, 14),
            Load(3, 15),
            Valuation(0, 3),
            Valuation(1, 3),
            Valuation(2, 3),
            Valuation(3, 3),
        };
        events.Add(Multiply(0, 0, 1));
        events.Add(Multiply(2, 2, 3));
        events.Add(Multiply(0, 0, 2));
        events.Add(Valuation(0, 3));

        return Trace(
            "MULTIPLICATIVE_DAG",
            "cold-leaves-balanced-products",
            width,
            events,
            primeAttributionEligible: true);
    }

    private static Build005Trace ProducerFactored(int width)
    {
        int[] primes = [2, 3, 5, 7, 11];
        var events = new List<Build005TraceEvent> { Load(0, 1) };
        foreach (var prime in primes)
        {
            events.Add(new Build005TraceEvent(
                Build005EventKind.ProducerPrimeFact,
                Destination: 0,
                Left: 0,
                Divisor: prime));
        }

        events.AddRange(primes.Select(prime => Valuation(0, prime)));
        return Trace(
            "PRODUCER_FACTORED",
            "known-prime-constructor",
            width,
            events,
            sourceRegime: "PRODUCER_GENERATED",
            searchRepaymentEligible: false);
    }

    private static Build005Trace AdditionMutation(int width)
    {
        var events = new List<Build005TraceEvent> { Load(0, 6), Load(1, 1) };
        for (var index = 0; index < 48; index++)
        {
            events.Add(Valuation(0, 3));
            events.Add(Add(0, 0, 1));
        }

        return Trace("ADDITION_MUTATION", "query-then-increment", width, events, hostile: true);
    }

    private static Build005Trace PhaseShift(int width)
    {
        var events = new List<Build005TraceEvent> { Load(0, CheckedPowerTimes(width, 3, width == 8 ? 4 : 8, 1)) };
        for (var index = 0; index < 64; index++)
        {
            events.Add(Valuation(0, 3));
        }

        var random = new Build005SplitMix64(Build005Protocol.DeriveSeed(width, "PHASE_SHIFT"));
        for (var index = 0; index < 64; index++)
        {
            events.Add(Overwrite(0, 1 + random.NextBelow(Maximum(width))));
            events.Add(Valuation(
                0,
                Build005Protocol.PrimeCatalog[(int)random.NextBelow((ulong)Build005Protocol.PrimeCatalog.Length)]));
        }

        return Trace("PHASE_SHIFT", "locality-to-stream", width, events, hostile: true);
    }

    private static Build005Trace CompositeControl(int width)
    {
        var events = new List<Build005TraceEvent> { Load(0, Fit(width, 2UL * 3 * 5 * 7 * 11)) };
        foreach (var divisor in Build005Protocol.CompositeControls)
        {
            events.Add(new Build005TraceEvent(
                Build005EventKind.CompositeValuationControl,
                Left: 0,
                Divisor: divisor));
        }

        events.Add(Overwrite(0, 6));
        events.Add(new Build005TraceEvent(
            Build005EventKind.CompositeValuationControl,
            Left: 0,
            Divisor: 6));
        return Trace(
            "COMPOSITE_CONTROL",
            "size-matched-divisors-and-2x3-counterexample",
            width,
            events,
            searchRepaymentEligible: false);
    }

    private static Build005Trace RadixTwo(int width)
    {
        var events = new List<Build005TraceEvent>();
        for (ulong value = 0; value < 64 && value <= Maximum(width); value++)
        {
            events.Add(Load(0, value));
            events.Add(Valuation(0, 2));
        }

        return Trace(
            "RADIX_V2",
            "ctz-isolated",
            width,
            events,
            searchRepaymentEligible: false);
    }

    private static Build005Trace SlotThrash(int width)
    {
        var events = new List<Build005TraceEvent>();
        for (var slot = 0; slot < 4; slot++)
        {
            events.Add(Load(slot, (ulong)(45 + (slot * 14))));
        }

        for (var index = 0; index < 64; index++)
        {
            events.Add(Valuation(index % 4, 3));
        }

        return Trace("HOSTILE_SLOT_THRASH", "four-slots", width, events, hostile: true);
    }

    private static Build005Trace PrimeThrash(int width)
    {
        var value = Fit(width, width == 8 ? 210UL : 30_030UL);
        var events = new List<Build005TraceEvent> { Load(0, value) };
        for (var cycle = 0; cycle < 8; cycle++)
        {
            foreach (var prime in Build005Protocol.PrimeCatalog.Take(5))
            {
                events.Add(Valuation(0, prime));
            }
        }

        return Trace("HOSTILE_PRIME_THRASH", "five-prime-rotation", width, events, hostile: true);
    }

    private static Build005Trace MutationAfterFill(int width)
    {
        var events = new List<Build005TraceEvent> { Load(0, 45) };
        for (var index = 0; index < 40; index++)
        {
            events.Add(Valuation(0, 3));
            events.Add(Overwrite(0, (ulong)(45 + (index % 2))));
        }

        return Trace("HOSTILE_MUTATE_AFTER_FILL", "fill-then-discard", width, events, hostile: true);
    }

    private static Build005Trace SpeculationPoison(int width)
    {
        var events = new List<Build005TraceEvent>();
        var random = new Build005SplitMix64(Build005Protocol.DeriveSeed(width, "SPECULATION_POISON"));
        for (var index = 0; index < 64; index++)
        {
            events.Add(Load(0, 1 + random.NextBelow(Maximum(width))));
            events.Add(Valuation(0, 31));
        }

        return Trace("HOSTILE_SPECULATION_POISON", "request-last-prime-only", width, events, hostile: true);
    }

    private static Build005Trace BoundaryAndFailure(int width)
    {
        var maximum = Maximum(width);
        var events = new List<Build005TraceEvent>
        {
            Load(0, 0),
            Valuation(0, 2),
            Valuation(0, 31),
            Overwrite(0, 1),
            Valuation(0, 3),
            Overwrite(0, maximum),
            Valuation(0, 31),
            Load(1, 2),
            Multiply(2, 0, 1),
            Add(2, 0, 1),
            Multiply(0, 0, 0),
        };
        return Trace("HOSTILE_BOUNDARY_FAILURE", "zero-one-max-overflow-alias", width, events, hostile: true);
    }

    private static Build005Trace GenerationWrap(int width)
    {
        var events = new List<Build005TraceEvent> { Load(0, 3), Valuation(0, 3) };
        for (var index = 0; index < 256; index++)
        {
            events.Add(Overwrite(0, (ulong)(3 + (index & 1))));
        }

        events.Add(Valuation(0, 3));
        return Trace("HOSTILE_GENERATION_WRAP", "full-flush-before-tag-reuse", width, events, hostile: true);
    }

    private static Build005Trace Trace(
        string family,
        string traceId,
        int width,
        List<Build005TraceEvent> events,
        bool hostile = false,
        bool primeAttributionEligible = false,
        bool searchRepaymentEligible = true,
        string sourceRegime = "COLD_MAG") =>
        new(
            family,
            traceId,
            width,
            sourceRegime,
            "MAGNITUDE_FINAL",
            new ReadOnlyCollection<Build005TraceEvent>(events),
            hostile,
            primeAttributionEligible,
            searchRepaymentEligible);

    private static Build005TraceEvent Load(int slot, ulong value) =>
        new(Build005EventKind.Load, Destination: slot, Magnitude: value);

    private static Build005TraceEvent Overwrite(int slot, ulong value) =>
        new(Build005EventKind.Overwrite, Destination: slot, Magnitude: value);

    private static Build005TraceEvent Test(int slot, int prime, int threshold) =>
        new(Build005EventKind.TestPower, Left: slot, Divisor: prime, Threshold: threshold);

    private static Build005TraceEvent Valuation(int slot, int prime) =>
        new(Build005EventKind.Valuation, Left: slot, Divisor: prime);

    private static Build005TraceEvent Strip(int slot, int prime) =>
        new(Build005EventKind.StripAll, Left: slot, Divisor: prime);

    private static Build005TraceEvent Multiply(int destination, int left, int right) =>
        new(Build005EventKind.Multiply, Destination: destination, Left: left, Right: right);

    private static Build005TraceEvent Add(int destination, int left, int right) =>
        new(Build005EventKind.Add, Destination: destination, Left: left, Right: right);

    private static ulong CheckedPowerTimes(int width, int prime, int exponent, ulong residual)
    {
        var value = residual;
        for (var index = 0; index < exponent; index++)
        {
            value = checked(value * (ulong)prime);
        }

        return Fit(width, value);
    }

    private static ulong Fit(int width, ulong value) => Math.Min(value, Maximum(width));

    private static ulong Maximum(int width) => width == 32 ? uint.MaxValue : (1UL << width) - 1;
}
