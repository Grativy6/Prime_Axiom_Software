namespace PrimeAxiom.Cli;

internal enum Build002TraceOperation
{
    ScaleKnownFactor,
    CancelKnownFactor,
    AddMagnitude,
}

internal sealed record Build002TraceStep(Build002TraceOperation Operation, int Operand);

internal sealed record Build002Trace(
    string Experiment,
    string Id,
    int Width,
    int InitialMagnitude,
    IReadOnlyList<Build002TraceStep> Steps,
    int ExpectedRejectedCancellations,
    string Feature);

internal sealed record Build002RationalCase(
    string Id,
    int Width,
    int Numerator,
    int Denominator,
    string Feature);

internal sealed record Build002HostileCase(int Width, int Magnitude, string Kind);

internal static class Build002Workloads
{
    private static readonly int[] Catalog = [2, 3, 5, 7];

    public static IReadOnlyList<Build002Trace> RepeatedScaleCancel(int width)
    {
        ValidateWidth(width);
        var legalPatterns = new[]
        {
            Repeat([Scale(2), Cancel(2)], 16),
            Repeat([Scale(3), Cancel(3)], 16),
            Repeat([Scale(2), Scale(3), Cancel(2), Cancel(3)], 8),
            Repeat([Scale(5), Cancel(5), Scale(7), Cancel(7)], 8),
        };
        var rejectingPatterns = new[]
        {
            Repeat([Cancel(2), Scale(2), Cancel(2), Scale(2)], 8),
            Repeat([Scale(3), Cancel(3), Cancel(3), Scale(3)], 8),
            Repeat([Scale(2), Cancel(5), Cancel(2), Cancel(2)], 8),
            Repeat(
                [Scale(7), Cancel(7), Cancel(3), Scale(3), Cancel(3), Cancel(2), Scale(2), Cancel(2)],
                4),
        };

        var traces = new List<Build002Trace>(8);
        for (var index = 0; index < legalPatterns.Length; index++)
        {
            traces.Add(new Build002Trace(
                "B",
                $"B-W{width}-LEGAL-{index}",
                width,
                1,
                legalPatterns[index],
                0,
                "warm legal catalog scaling/cancellation"));
        }

        for (var index = 0; index < rejectingPatterns.Length; index++)
        {
            var expectedRejections = CountExpectedCancellationRejections(
                initialMagnitude: 1,
                maximumMagnitude: (1 << width) - 1,
                rejectingPatterns[index]);
            if (expectedRejections == 0)
            {
                throw new InvalidOperationException("A frozen rejecting trace contains no rejected cancellation.");
            }

            traces.Add(new Build002Trace(
                "B",
                $"B-W{width}-REJECT-{index}",
                width,
                1,
                rejectingPatterns[index],
                expectedRejections,
                "atomic illegal cancellation"));
        }

        if (traces.Any(trace => trace.Steps.Count != 32))
        {
            throw new InvalidOperationException("Every frozen Experiment B trace must contain 32 instructions.");
        }

        return traces;
    }

    public static IReadOnlyList<Build002Trace> MixedAddition(int width)
    {
        ValidateWidth(width);
        var half = 1 << (width - 1);
        var unsupportedPrime = width == 4 ? 11 : 11;
        return
        [
            Mixed(width, 0, 1, 2, 3, 2, 2, 2, "unequal 2-adic valuations"),
            Mixed(width, 1, 1, 2, 2, 2, 2, 3, "equal valuation with extra cancellation"),
            Mixed(width, 2, 1, 3, 5, 3, 2, 2, "coprime sum"),
            Mixed(width, 3, 0, 2, 3, 2, 1, 2, "explicit zero crosses into nonzero state"),
            Mixed(width, 4, 1, 7, 2, 2, half - 7, 2, "final W-bit overflow"),
            Mixed(width, 5, 1, 2, 3, 2, unsupportedPrime, 2, "unsupported prime introduced by addition"),
            Mixed(width, 6, 1, 2, 3, 3, 6, 3, "equal valuation can gain additional powers"),
            Mixed(width, 7, 1, 5, 7, 5, 6, 2, "addition produces outside-basis structure"),
        ];
    }

    public static IReadOnlyList<Build002RationalCase> RationalReduction(int width)
    {
        ValidateWidth(width);
        return width switch
        {
            4 =>
            [
                Rational(width, 0, 12, 8, "catalog-smooth common factor"),
                Rational(width, 1, 15, 10, "catalog-smooth common factor"),
                Rational(width, 2, 14, 7, "exact denominator cancellation"),
                Rational(width, 3, 9, 6, "catalog-smooth common factor"),
                Rational(width, 4, 11, 11, "shared unsupported prime"),
                Rational(width, 5, 13, 13, "shared unsupported prime"),
                Rational(width, 6, 10, 15, "non-integral reduced result"),
                Rational(width, 7, 7, 14, "proper fraction"),
            ],
            6 =>
            [
                Rational(width, 0, 12, 8, "catalog-smooth common factor"),
                Rational(width, 1, 45, 30, "catalog-smooth common factor"),
                Rational(width, 2, 42, 63, "mixed catalog support"),
                Rational(width, 3, 49, 35, "catalog-smooth common factor"),
                Rational(width, 4, 22, 33, "shared unsupported prime 11"),
                Rational(width, 5, 26, 39, "shared unsupported prime 13"),
                Rational(width, 6, 34, 51, "shared unsupported prime 17"),
                Rational(width, 7, 0, 11, "zero numerator"),
            ],
            8 =>
            [
                Rational(width, 0, 180, 168, "mixed catalog support"),
                Rational(width, 1, 225, 150, "catalog-smooth common factor"),
                Rational(width, 2, 210, 126, "catalog-smooth common factor"),
                Rational(width, 3, 196, 245, "catalog-smooth common factor"),
                Rational(width, 4, 187, 221, "shared unsupported prime 17"),
                Rational(width, 5, 209, 247, "shared unsupported prime 19"),
                Rational(width, 6, 0, 143, "zero numerator with unsupported denominator"),
                Rational(width, 7, 242, 154, "shared unsupported factor 22"),
            ],
            _ => throw new InvalidOperationException("Width validation failed."),
        };
    }

    public static IReadOnlyList<Build002HostileCase> HostileValues(int width)
    {
        ValidateWidth(width);
        var maximum = (1 << width) - 1;
        var cases = new Dictionary<int, string>();

        for (var value = 11; value <= maximum; value++)
        {
            if (IsPrime(value))
            {
                cases[value] = "PRIME_OUTSIDE_S4";
            }
        }

        var outsidePrimes = cases.Keys.ToArray();
        foreach (var left in outsidePrimes)
        {
            foreach (var right in outsidePrimes)
            {
                var product = left * right;
                if (product > maximum)
                {
                    break;
                }

                cases.TryAdd(product, "SEMIPRIME_OUTSIDE_S4");
            }
        }

        var random = new SplitMix64(Build002Protocol.DeriveSeed(width, "F", 0));
        var attempts = 0;
        while (cases.Count(pair => pair.Value == "ODD_CATALOG_ROUGH_SAMPLE") < 8 && attempts < 10_000)
        {
            attempts++;
            var candidate = 1 + (int)random.NextBelow((ulong)maximum);
            candidate |= 1;
            if (candidate <= maximum && Catalog.All(prime => candidate % prime != 0))
            {
                cases.TryAdd(candidate, "ODD_CATALOG_ROUGH_SAMPLE");
            }
        }

        return cases
            .OrderBy(pair => pair.Key)
            .Select(pair => new Build002HostileCase(width, pair.Key, pair.Value))
            .ToArray();
    }

    private static Build002Trace Mixed(
        int width,
        int index,
        int initial,
        int firstScale,
        int secondScale,
        int cancellation,
        int addend,
        int finalScale,
        string feature) =>
        new(
            "E",
            $"E-W{width}-{index}",
            width,
            initial,
            [
                Scale(firstScale),
                Scale(secondScale),
                Cancel(cancellation),
                new Build002TraceStep(Build002TraceOperation.AddMagnitude, addend),
                Scale(finalScale),
            ],
            0,
            feature);

    private static Build002RationalCase Rational(
        int width,
        int index,
        int numerator,
        int denominator,
        string feature) =>
        new($"D-W{width}-{index}", width, numerator, denominator, feature);

    private static Build002TraceStep Scale(int prime) =>
        new(Build002TraceOperation.ScaleKnownFactor, prime);

    private static Build002TraceStep Cancel(int prime) =>
        new(Build002TraceOperation.CancelKnownFactor, prime);

    private static List<Build002TraceStep> Repeat(
        IReadOnlyList<Build002TraceStep> pattern,
        int count)
    {
        var result = new List<Build002TraceStep>(checked(pattern.Count * count));
        for (var index = 0; index < count; index++)
        {
            result.AddRange(pattern);
        }

        return result;
    }

    private static int CountExpectedCancellationRejections(
        int initialMagnitude,
        int maximumMagnitude,
        IEnumerable<Build002TraceStep> steps)
    {
        var value = initialMagnitude;
        var rejections = 0;
        foreach (var step in steps)
        {
            switch (step.Operation)
            {
                case Build002TraceOperation.ScaleKnownFactor:
                    if (value <= maximumMagnitude / step.Operand)
                    {
                        value *= step.Operand;
                    }

                    break;
                case Build002TraceOperation.CancelKnownFactor:
                    if (value % step.Operand == 0)
                    {
                        value /= step.Operand;
                    }
                    else
                    {
                        rejections++;
                    }

                    break;
                case Build002TraceOperation.AddMagnitude:
                    if (value <= maximumMagnitude - step.Operand)
                    {
                        value += step.Operand;
                    }

                    break;
                default:
                    throw new InvalidOperationException("Undefined Build 002 trace operation.");
            }
        }

        return rejections;
    }

    private static bool IsPrime(int value)
    {
        if (value < 2)
        {
            return false;
        }

        for (var divisor = 2; divisor <= value / divisor; divisor++)
        {
            if (value % divisor == 0)
            {
                return false;
            }
        }

        return true;
    }

    private static void ValidateWidth(int width)
    {
        if (!Build002Protocol.Widths.Contains(width))
        {
            throw new ArgumentOutOfRangeException(nameof(width));
        }
    }
}
