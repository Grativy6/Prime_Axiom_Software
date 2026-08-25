using System.Numerics;
using PrimeAxiom.Core.Circuits;
using PrimeAxiom.Core.Substrate;

namespace PrimeAxiom.Core.Hybrid;

public sealed partial class HybridInteger
{
    public HybridResult<HybridInteger> ExactDivide(HybridInteger divisor)
    {
        if (!Compatible(divisor, "EXACT_DIVIDE", out var failure))
        {
            return failure!;
        }

        if (divisor.IsZero)
        {
            return Failed<HybridInteger>(
                "EXACT_DIVIDE",
                HybridFailure.DivisionByZero,
                HybridDomain.Mixed,
                HybridCostLedger.Zero,
                Validity,
                "Zero is never an executable divisor");
        }

        if (Validity != HybridValidity.Canonical || divisor.Validity != HybridValidity.Canonical)
        {
            return Failed<HybridInteger>(
                "EXACT_DIVIDE",
                HybridFailure.RequiresCanonical,
                HybridDomain.Mixed,
                HybridCostLedger.Zero,
                MergeValidity(Validity, divisor.Validity),
                "Refresh explicit lower-bound lanes before exact division");
        }

        if (IsZero)
        {
            return Succeeded(
                "EXACT_DIVIDE",
                CreateZero(Bank, ExponentWidth, LaneProvenance.ExactDivision),
                HybridDomain.BankNative,
                HybridCostLedger.Zero,
                Validity,
                "Zero divided by a nonzero exact value is zero");
        }

        var words = new BinaryWord[LaneCount];
        var borrows = new BitState[LaneCount];
        var gateCosts = new List<GateCost>(LaneCount);
        for (var lane = 0; lane < LaneCount; lane++)
        {
            var subtracted = BinaryCircuit.Subtract(_exponents[lane], divisor._exponents[lane]);
            words[lane] = subtracted.Value;
            borrows[lane] = subtracted.Borrow;
            gateCosts.Add(subtracted.Cost);
        }

        var cost = new HybridCostVector(
            AggregateOverflow(borrows, GateCost.Parallel(gateCosts)),
            LaneReads: LaneCount * 2L,
            LaneWrites: LaneCount,
            MetadataReads: LaneCount * 2L + 4,
            MetadataWrites: LaneCount + 2L);
        if (borrows.Any(state => state == BitState.On))
        {
            return Failed<HybridInteger>(
                "EXACT_DIVIDE",
                HybridFailure.NotDivisible,
                HybridDomain.BankNative,
                HybridCostLedger.Zero.Add(CostPhase.Native, cost),
                Validity,
                "At least one exact valuation lane underflowed; cofactor division was not attempted");
        }

        cost += new HybridCostVector(
            GateCost.Zero,
            CofactorRemainders: 1,
            BinaryOperandBits: checked(BitLength(Cofactor) + BitLength(divisor.Cofactor)));
        if (Cofactor % divisor.Cofactor != BigInteger.Zero)
        {
            return Failed<HybridInteger>(
                "EXACT_DIVIDE",
                HybridFailure.NotDivisible,
                HybridDomain.Mixed,
                HybridCostLedger.Zero.Add(CostPhase.Native, cost),
                Validity,
                "Valuation lanes cover the divisor, but its exact cofactor does not divide");
        }

        cost += new HybridCostVector(GateCost.Zero, CofactorDivisions: 1);
        var result = new HybridInteger(
            Bank,
            ExponentWidth,
            Sign * divisor.Sign,
            Cofactor / divisor.Cofactor,
            words,
            Enumerable.Repeat(ValuationKnowledge.KnownExact, LaneCount),
            Enumerable.Repeat(LaneProvenance.ExactDivision, LaneCount));
        return Succeeded(
            "EXACT_DIVIDE",
            result,
            HybridDomain.Mixed,
            HybridCostLedger.Zero.Add(CostPhase.Native, cost),
            Validity,
            "Checked lane subtraction plus exact ordinary cofactor division");
    }

    /// <summary>Tests whether this value divides <paramref name="dividend"/>.</summary>
    public HybridQueryResult<bool?> Divides(HybridInteger dividend)
    {
        if (!Bank.Equals(dividend.Bank))
        {
            return UnknownBoolean(
                "DIVIDES",
                HybridFailure.BankMismatch,
                HybridDomain.None,
                "Migrate operands to a common bank before a structural divisibility query");
        }

        if (ExponentWidth != dividend.ExponentWidth)
        {
            return UnknownBoolean(
                "DIVIDES",
                HybridFailure.ExponentWidthMismatch,
                HybridDomain.None,
                "Exponent widths differ");
        }

        if (IsZero)
        {
            return KnownBoolean(
                "DIVIDES",
                dividend.IsZero,
                HybridDomain.BankNative,
                "Mathematical convention used here: zero divides only zero");
        }

        if (dividend.IsZero)
        {
            return KnownBoolean(
                "DIVIDES",
                true,
                HybridDomain.BankNative,
                "Every nonzero integer divides zero");
        }

        if (Validity != HybridValidity.Canonical || dividend.Validity != HybridValidity.Canonical)
        {
            return UnknownBoolean(
                "DIVIDES",
                HybridFailure.RequiresCanonical,
                HybridDomain.Mixed,
                "Lower-bound lanes cannot disprove hidden cofactor valuations; refresh first");
        }

        var gates = new List<GateCost>(LaneCount);
        var greaterStates = new List<BitState>(LaneCount);
        var laneFails = false;
        for (var lane = 0; lane < LaneCount; lane++)
        {
            var compared = BinaryCircuit.Compare(_exponents[lane], dividend._exponents[lane]);
            gates.Add(compared.Cost);
            greaterStates.Add(compared.Greater);
            laneFails |= compared.Greater == BitState.On;
        }

        var cost = new HybridCostVector(
            AggregateOverflow(greaterStates, GateCost.Parallel(gates)),
            LaneReads: LaneCount * 2L,
            MetadataReads: LaneCount * 2L + 4);
        if (laneFails)
        {
            return KnownBoolean(
                "DIVIDES",
                false,
                HybridDomain.BankNative,
                "An exact bank lane disproves divisibility",
                HybridCostLedger.Zero.Add(CostPhase.Native, cost));
        }

        cost += new HybridCostVector(
            GateCost.Zero,
            CofactorRemainders: 1,
            BinaryOperandBits: checked(BitLength(Cofactor) + BitLength(dividend.Cofactor)));
        return KnownBoolean(
            "DIVIDES",
            dividend.Cofactor % Cofactor == BigInteger.Zero,
            HybridDomain.Mixed,
            "Lane order plus exact cofactor remainder",
            HybridCostLedger.Zero.Add(CostPhase.Native, cost));
    }

    public HybridResult<HybridInteger> GreatestCommonDivisor(HybridInteger other) =>
        ExtremumWithCofactor(other, minimum: true, "GCD");

    public HybridResult<HybridInteger> LeastCommonMultiple(HybridInteger other) =>
        ExtremumWithCofactor(other, minimum: false, "LCM");

    public HybridResult<HybridInteger> MigrateBank(ValuationBank targetBank, int? targetExponentWidth = null)
    {
        ArgumentNullException.ThrowIfNull(targetBank);
        var width = targetExponentWidth ?? ExponentWidth;
        if (width <= 0 || width > MaximumExponentWidth)
        {
            return Failed<HybridInteger>(
                "MIGRATE_BANK",
                HybridFailure.ExponentWidthMismatch,
                HybridDomain.Maintenance,
                HybridCostLedger.Zero,
                Validity,
                $"Target exponent width must be in 1..{MaximumExponentWidth}");
        }

        if (IsZero)
        {
            var zero = CreateZero(targetBank, width, LaneProvenance.BankMigration);
            var zeroCost = new HybridCostVector(
                GateCost.Zero,
                LaneReads: LaneCount,
                LaneWrites: targetBank.Count,
                MetadataReads: LaneCount + targetBank.Count + 2L,
                MetadataWrites: targetBank.Count + 2L,
                Migrations: 1);
            return Succeeded(
                "MIGRATE_BANK",
                zero,
                HybridDomain.Maintenance,
                HybridCostLedger.Zero.Add(CostPhase.Maintenance, zeroCost),
                Validity,
                "Zero migration changes only its explicit bank context");
        }

        var residual = Cofactor;
        long cofactorMultiplications = 0;
        for (var oldLane = 0; oldLane < LaneCount; oldLane++)
        {
            if (targetBank.IndexOf(Bank[oldLane]) >= 0)
            {
                continue;
            }

            var exponent = _exponents[oldLane].ToUnsigned();
            if (!exponent.IsZero)
            {
                residual *= PowCounted(Bank[oldLane], exponent, ref cofactorMultiplications);
                cofactorMultiplications++;
            }
        }

        var targetWords = new BinaryWord[targetBank.Count];
        var targetKnowledge = new ValuationKnowledge[targetBank.Count];
        var targetSources = Enumerable.Repeat(LaneProvenance.BankMigration, targetBank.Count).ToArray();
        long trialRemainders = 0;
        long factorDivisions = 0;
        long targetLanesVisited = 0;
        long targetLanesWritten = 0;
        var maximum = (BigInteger.One << width) - BigInteger.One;
        for (var targetLane = 0; targetLane < targetBank.Count; targetLane++)
        {
            targetLanesVisited++;
            var oldLane = Bank.IndexOf(targetBank[targetLane]);
            if (oldLane >= 0)
            {
                var exponent = _exponents[oldLane].ToUnsigned();
                if (exponent > maximum)
                {
                    return MigrationOverflow(targetBank[targetLane], trialRemainders, factorDivisions, cofactorMultiplications);
                }

                targetWords[targetLane] = BinaryWord.FromUnsigned(exponent, width);
                targetKnowledge[targetLane] = _knowledge[oldLane];
                targetLanesWritten++;
                continue;
            }

            var extracted = BigInteger.Zero;
            while (true)
            {
                trialRemainders++;
                if (residual % targetBank[targetLane] != BigInteger.Zero)
                {
                    break;
                }

                residual /= targetBank[targetLane];
                extracted++;
                factorDivisions++;
                if (extracted > maximum)
                {
                    return MigrationOverflow(targetBank[targetLane], trialRemainders, factorDivisions, cofactorMultiplications);
                }
            }

            targetWords[targetLane] = BinaryWord.FromUnsigned(extracted, width);
            targetKnowledge[targetLane] = ValuationKnowledge.KnownExact;
            targetLanesWritten++;
        }

        var result = new HybridInteger(
            targetBank,
            width,
            Sign,
            residual,
            targetWords,
            targetKnowledge,
            targetSources);
        var cost = MigrationCost(trialRemainders, factorDivisions, cofactorMultiplications, completed: true);
        return Succeeded(
            "MIGRATE_BANK",
            result,
            HybridDomain.Maintenance,
            HybridCostLedger.Zero.Add(CostPhase.Maintenance, cost),
            Validity,
            "Evicted powers fold into the cofactor; admitted primes are stripped exactly; retained lane knowledge is preserved");

        HybridResult<HybridInteger> MigrationOverflow(
            int prime,
            long checks,
            long divisions,
            long multiplications) =>
            Failed<HybridInteger>(
                "MIGRATE_BANK",
                HybridFailure.ExponentOverflow,
                HybridDomain.Maintenance,
                HybridCostLedger.Zero.Add(CostPhase.Maintenance, MigrationCost(checks, divisions, multiplications, completed: false)),
                Validity,
                "Migration is transactional; source value and bank remain unchanged",
                $"Prime {prime} does not fit the target {width}-bit lane.");

        HybridCostVector MigrationCost(long checks, long divisions, long multiplications, bool completed) =>
            new(
                GateCost.Zero,
                TrialRemainders: checks,
                FactorDivisions: divisions,
                CofactorMultiplications: multiplications,
                BinaryOperandBits: checked(BitLength(Cofactor) + BitLength(residual)),
                LaneReads: LaneCount,
                LaneWrites: targetLanesWritten,
                MetadataReads: checked(LaneCount + targetLanesVisited + 2L),
                MetadataWrites: checked(targetLanesWritten + (completed ? 2L : 0L)),
                Migrations: 1);
    }

    public HybridQueryResult<bool?> NumericEquals(HybridInteger other)
    {
        ArgumentNullException.ThrowIfNull(other);
        if (Bank.Equals(other.Bank) && ExponentWidth == other.ExponentWidth &&
            Validity == HybridValidity.Canonical && other.Validity == HybridValidity.Canonical)
        {
            var gateCosts = new List<GateCost>(LaneCount);
            var unequalStates = new List<BitState>(LaneCount);
            for (var lane = 0; lane < LaneCount; lane++)
            {
                var compared = BinaryCircuit.Compare(_exponents[lane], other._exponents[lane]);
                gateCosts.Add(compared.Cost);
                unequalStates.Add(BitStateExtensions.FromBoolean(compared.Equal != BitState.On));
            }

            var signsEqual = Sign == other.Sign;
            var cofactorsEqual = signsEqual && Cofactor == other.Cofactor;
            var equal = cofactorsEqual && unequalStates.All(state => state == BitState.Off);
            var cost = new HybridCostVector(
                AggregateOverflow(unequalStates, GateCost.Parallel(gateCosts)),
                CofactorComparisons: signsEqual ? 1 : 0,
                BinaryOperandBits: signsEqual ? checked(BitLength(Cofactor) + BitLength(other.Cofactor)) : 0,
                LaneReads: LaneCount * 2L,
                MetadataReads: LaneCount * 2L + 4);
            return new HybridQueryResult<bool?>(
                equal,
                true,
                Receipt(
                    "NUMERIC_EQUALS",
                    true,
                    HybridFailure.None,
                    HybridDomain.Mixed,
                    HybridCostLedger.Zero.Add(CostPhase.Native, cost),
                    Validity,
                    Validity,
                    "Same-bank canonical equality compares sign, exact cofactor, every lane, and status reduction"));
        }

        var left = Reconstruct();
        var right = other.Reconstruct();
        var comparison = new HybridCostVector(
            GateCost.Zero,
            CofactorComparisons: 1,
            BinaryOperandBits: checked(BitLength(left.Value) + BitLength(right.Value)));
        return new HybridQueryResult<bool?>(
            left.Value == right.Value,
            true,
            Receipt(
                "NUMERIC_EQUALS",
                true,
                HybridFailure.None,
                HybridDomain.Boundary,
                (left.Receipt.Cost + right.Receipt.Cost).Add(CostPhase.Egress, comparison),
                MergeValidity(Validity, other.Validity),
                MergeValidity(Validity, other.Validity),
                "Cross-bank or deferred equality reconstructs exact ordinary magnitudes and charges egress"));
    }

    private HybridResult<HybridInteger> ExtremumWithCofactor(HybridInteger other, bool minimum, string operation)
    {
        if (!Compatible(other, operation, out var failure))
        {
            return failure!;
        }

        if (Validity != HybridValidity.Canonical || other.Validity != HybridValidity.Canonical)
        {
            return Failed<HybridInteger>(
                operation,
                HybridFailure.RequiresCanonical,
                HybridDomain.Mixed,
                HybridCostLedger.Zero,
                MergeValidity(Validity, other.Validity),
                "Exact gcd/lcm requires canonical lane valuations; refresh first");
        }

        if (minimum && IsZero)
        {
            return AbsoluteCopy(other, operation, "gcd(0, n) = |n|");
        }

        if (minimum && other.IsZero)
        {
            return AbsoluteCopy(this, operation, "gcd(n, 0) = |n|");
        }

        if (!minimum && (IsZero || other.IsZero))
        {
            return Succeeded(
                operation,
                CreateZero(Bank, ExponentWidth, LaneProvenance.RationalCancellation),
                HybridDomain.BankNative,
                HybridCostLedger.Zero,
                Validity,
                "lcm with zero is zero");
        }

        var words = new BinaryWord[LaneCount];
        var gateCosts = new List<GateCost>(LaneCount);
        for (var lane = 0; lane < LaneCount; lane++)
        {
            var selected = minimum
                ? BinaryCircuit.Min(_exponents[lane], other._exponents[lane])
                : BinaryCircuit.Max(_exponents[lane], other._exponents[lane]);
            words[lane] = selected.Value;
            gateCosts.Add(selected.Cost);
        }

        var gcd = BigInteger.GreatestCommonDivisor(Cofactor, other.Cofactor);
        var cofactor = minimum ? gcd : (Cofactor / gcd) * other.Cofactor;
        var cost = new HybridCostVector(
            GateCost.Parallel(gateCosts),
            CofactorMultiplications: minimum ? 0 : 1,
            CofactorDivisions: minimum ? 0 : 1,
            CofactorGcds: 1,
            BinaryOperandBits: checked(BitLength(Cofactor) + BitLength(other.Cofactor)),
            LaneReads: LaneCount * 2L,
            LaneWrites: LaneCount,
            MetadataReads: LaneCount * 2L + 4,
            MetadataWrites: LaneCount + 2L);
        var result = new HybridInteger(
            Bank,
            ExponentWidth,
            1,
            cofactor,
            words,
            Enumerable.Repeat(ValuationKnowledge.KnownExact, LaneCount),
            Enumerable.Repeat(LaneProvenance.RationalCancellation, LaneCount));
        return Succeeded(
            operation,
            result,
            HybridDomain.Mixed,
            HybridCostLedger.Zero.Add(CostPhase.Native, cost),
            Validity,
            minimum
                ? "Lane minima plus ordinary cofactor gcd"
                : "Lane maxima plus ordinary cofactor lcm using gcd/divide/multiply");
    }

    private static HybridResult<HybridInteger> AbsoluteCopy(HybridInteger source, string operation, string scope)
    {
        var value = source.IsZero
            ? source
            : new HybridInteger(
                source.Bank,
                source.ExponentWidth,
                1,
                source.Cofactor,
                source._exponents,
                source._knowledge,
                Enumerable.Repeat(LaneProvenance.RationalCancellation, source.LaneCount));
        return Succeeded(
            operation,
            value,
            HybridDomain.BankNative,
            HybridCostLedger.Zero.Add(CostPhase.Native, MetadataCopyCost(source)),
            source.Validity,
            scope);
    }

    private HybridQueryResult<bool?> KnownBoolean(
        string operation,
        bool value,
        HybridDomain domain,
        string scope,
        HybridCostLedger? cost = null) =>
        new(
            value,
            true,
            Receipt(
                operation,
                true,
                HybridFailure.None,
                domain,
                cost ?? HybridCostLedger.Zero.Add(
                    CostPhase.Native,
                    new HybridCostVector(GateCost.Zero, MetadataReads: 2)),
                Validity,
                Validity,
                scope));

    private HybridQueryResult<bool?> UnknownBoolean(
        string operation,
        HybridFailure failure,
        HybridDomain domain,
        string scope) =>
        new(
            null,
            false,
            Receipt(
                operation,
                false,
                failure,
                domain,
                HybridCostLedger.Zero,
                Validity,
                Validity,
                scope));
}

public sealed class HybridRational
{
    private HybridRational(HybridInteger numerator, HybridInteger denominator)
    {
        Numerator = numerator;
        Denominator = denominator;
    }

    public HybridInteger Numerator { get; }

    public HybridInteger Denominator { get; }

    public static HybridResult<HybridRational> Create(HybridInteger numerator, HybridInteger denominator)
    {
        ArgumentNullException.ThrowIfNull(numerator);
        ArgumentNullException.ThrowIfNull(denominator);
        if (denominator.IsZero)
        {
            return Fail(HybridFailure.DivisionByZero, "A rational denominator must be nonzero.");
        }

        if (!numerator.Bank.Equals(denominator.Bank))
        {
            return Fail(HybridFailure.BankMismatch, "Rational components require one bank.");
        }

        if (numerator.ExponentWidth != denominator.ExponentWidth)
        {
            return Fail(HybridFailure.ExponentWidthMismatch, "Rational components require one exponent width.");
        }

        var rational = new HybridRational(numerator, denominator);
        return new HybridResult<HybridRational>(
            rational,
            new HybridReceipt(
                "RATIONAL_CREATE",
                true,
                HybridFailure.None,
                HybridDomain.BankNative,
                HybridCostLedger.Zero,
                numerator.Validity == HybridValidity.Partial || denominator.Validity == HybridValidity.Partial
                    ? HybridValidity.Partial
                    : HybridValidity.Canonical,
                numerator.Validity == HybridValidity.Partial || denominator.Validity == HybridValidity.Partial
                    ? HybridValidity.Partial
                    : HybridValidity.Canonical,
                "Exact numerator and denominator; simplification is explicit"));

        HybridResult<HybridRational> Fail(HybridFailure failure, string detail) =>
            new(
                null,
                new HybridReceipt(
                    "RATIONAL_CREATE",
                    false,
                    failure,
                    HybridDomain.None,
                    HybridCostLedger.Zero,
                    null,
                    null,
                    "Rejected rational construction",
                    detail));
    }

    public HybridResult<HybridRational> Simplify()
    {
        if (Numerator.Validity != HybridValidity.Canonical || Denominator.Validity != HybridValidity.Canonical)
        {
            return new HybridResult<HybridRational>(
                null,
                new HybridReceipt(
                    "RATIONAL_SIMPLIFY",
                    false,
                    HybridFailure.RequiresCanonical,
                    HybridDomain.Mixed,
                    HybridCostLedger.Zero,
                    HybridValidity.Partial,
                    null,
                    "Refresh deferred lanes before exact rational cancellation"));
        }

        var gcd = Numerator.GreatestCommonDivisor(Denominator);
        var numerator = Numerator.ExactDivide(gcd.Value!);
        var denominator = Denominator.ExactDivide(gcd.Value!);
        var ledger = gcd.Receipt.Cost + numerator.Receipt.Cost + denominator.Receipt.Cost;
        if (!numerator.Receipt.Succeeded || !denominator.Receipt.Succeeded)
        {
            return new HybridResult<HybridRational>(
                null,
                new HybridReceipt(
                    "RATIONAL_SIMPLIFY",
                    false,
                    HybridFailure.NotDivisible,
                    HybridDomain.Mixed,
                    ledger,
                    HybridValidity.Canonical,
                    null,
                    "Internal gcd cancellation unexpectedly failed"));
        }

        var simplifiedNumerator = numerator.Value!;
        var simplifiedDenominator = denominator.Value!;
        if (simplifiedDenominator.Sign < 0)
        {
            var negateNumerator = simplifiedNumerator.Negate();
            var negateDenominator = simplifiedDenominator.Negate();
            ledger += negateNumerator.Receipt.Cost + negateDenominator.Receipt.Cost;
            simplifiedNumerator = negateNumerator.Value!;
            simplifiedDenominator = negateDenominator.Value!;
        }

        var result = new HybridRational(simplifiedNumerator, simplifiedDenominator);
        return new HybridResult<HybridRational>(
            result,
            new HybridReceipt(
                "RATIONAL_SIMPLIFY",
                true,
                HybridFailure.None,
                HybridDomain.Mixed,
                ledger,
                HybridValidity.Canonical,
                HybridValidity.Canonical,
                "Bank-lane cancellation plus ordinary cofactor gcd/division; denominator sign normalized"));
    }
}
