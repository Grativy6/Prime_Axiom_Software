using System.Numerics;
using PrimeAxiom.Core.Circuits;
using PrimeAxiom.Core.Substrate;

namespace PrimeAxiom.Core.Hybrid;

/// <summary>
/// Exact signed integer represented as
/// sign * cofactor * product(bank[i] ^ exponent[i]).
/// A lower-bound lane says only that additional copies of its prime may remain
/// in the exact cofactor. The cofactor is never presumed prime or fully factored.
/// </summary>
public sealed partial class HybridInteger : IEquatable<HybridInteger>
{
    public const int MaximumExponentWidth = 4_096;
    private readonly BinaryWord[] _exponents;
    private readonly ValuationKnowledge[] _knowledge;
    private readonly LaneProvenance[] _provenance;

    private HybridInteger(
        ValuationBank bank,
        int exponentWidth,
        int sign,
        BigInteger cofactor,
        IEnumerable<BinaryWord> exponents,
        IEnumerable<ValuationKnowledge> knowledge,
        IEnumerable<LaneProvenance> provenance)
    {
        ArgumentNullException.ThrowIfNull(bank);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(exponentWidth);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(exponentWidth, MaximumExponentWidth);
        if (sign is < -1 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(sign));
        }

        Bank = bank;
        ExponentWidth = exponentWidth;
        Sign = sign;
        Cofactor = cofactor;
        _exponents = exponents.Select(word => new BinaryWord(word.CopyBits())).ToArray();
        _knowledge = knowledge.ToArray();
        _provenance = provenance.ToArray();

        if (_exponents.Length != bank.Count || _knowledge.Length != bank.Count || _provenance.Length != bank.Count)
        {
            throw new ArgumentException("Every bank lane requires an exponent, knowledge state, and provenance tag.");
        }

        if (_exponents.Any(word => word.Width != exponentWidth))
        {
            throw new ArgumentException("Every exponent word must match the declared width.", nameof(exponents));
        }

        if (_knowledge.Any(state => !Enum.IsDefined(state)) || _provenance.Any(source => !Enum.IsDefined(source)))
        {
            throw new ArgumentException("Lane knowledge and provenance values must be defined contract states.");
        }

        if (sign == 0)
        {
            if (!cofactor.IsZero || _exponents.Any(word => !word.ToUnsigned().IsZero) ||
                _knowledge.Any(state => state != ValuationKnowledge.KnownExact))
            {
                throw new ArgumentException("Zero requires zero cofactor/exponents and exact lane knowledge.");
            }
        }
        else
        {
            if (cofactor <= BigInteger.Zero)
            {
                throw new ArgumentException("A nonzero value requires a positive exact cofactor.", nameof(cofactor));
            }

            for (var lane = 0; lane < bank.Count; lane++)
            {
                if (_knowledge[lane] == ValuationKnowledge.KnownExact && cofactor % bank[lane] == BigInteger.Zero)
                {
                    throw new ArgumentException(
                        $"Lane {lane} claims an exact {bank[lane]}-valuation but the cofactor is divisible by that prime.");
                }
            }
        }
    }

    public ValuationBank Bank { get; }

    public int ExponentWidth { get; }

    public int Sign { get; }

    public BigInteger Cofactor { get; }

    public bool IsZero => Sign == 0;

    public bool IsIdentity => Sign == 1 && Cofactor == BigInteger.One && _exponents.All(word => word.ToUnsigned().IsZero);

    public HybridValidity Validity => _knowledge.Any(state => state == ValuationKnowledge.CertifiedLowerBound)
        ? HybridValidity.Partial
        : HybridValidity.Canonical;

    public int LaneCount => Bank.Count;

    public BinaryWord ExponentWordAt(int lane) => new(_exponents[ValidateLane(lane)].CopyBits());

    public BigInteger ExponentAt(int lane) => _exponents[ValidateLane(lane)].ToUnsigned();

    public ValuationKnowledge KnowledgeAt(int lane) => _knowledge[ValidateLane(lane)];

    public LaneProvenance ProvenanceAt(int lane) => _provenance[ValidateLane(lane)];

    public static HybridInteger Zero(ValuationBank bank, int exponentWidth) =>
        CreateZero(bank, exponentWidth, LaneProvenance.Zero);

    public static HybridInteger Identity(ValuationBank bank, int exponentWidth) =>
        new(
            bank,
            exponentWidth,
            1,
            BigInteger.One,
            ZeroWords(bank.Count, exponentWidth),
            Enumerable.Repeat(ValuationKnowledge.KnownExact, bank.Count),
            Enumerable.Repeat(LaneProvenance.StructuredIngress, bank.Count));

    public static HybridResult<HybridInteger> FromBinary(BigInteger value, ValuationBank bank, int exponentWidth)
    {
        ArgumentNullException.ThrowIfNull(bank);
        if (exponentWidth <= 0 || exponentWidth > MaximumExponentWidth)
        {
            return Failed<HybridInteger>(
                "BINARY_INGRESS",
                HybridFailure.ExponentWidthMismatch,
                HybridDomain.Boundary,
                HybridCostLedger.Zero,
                null,
                $"Exponent width must be in 1..{MaximumExponentWidth}.");
        }
        var operandBits = BitLength(BigInteger.Abs(value));
        if (value.IsZero)
        {
            var zero = CreateZero(bank, exponentWidth, LaneProvenance.BinaryIngress);
            return Succeeded(
                "BINARY_INGRESS",
                zero,
                HybridDomain.Boundary,
                HybridCostLedger.Zero.Add(
                    CostPhase.Ingress,
                    new HybridCostVector(
                        GateCost.Zero,
                        BinaryOperandBits: operandBits,
                        LaneWrites: bank.Count,
                        MetadataWrites: bank.Count + 2L)),
                null,
                "Exact signed binary magnitude; zero is explicitly tagged");
        }

        var remainder = BigInteger.Abs(value);
        var maximum = (BigInteger.One << exponentWidth) - BigInteger.One;
        var words = new BinaryWord[bank.Count];
        long checks = 0;
        long divisions = 0;
        for (var lane = 0; lane < bank.Count; lane++)
        {
            var exponent = BigInteger.Zero;
            while (true)
            {
                checks++;
                if (remainder % bank[lane] != BigInteger.Zero)
                {
                    break;
                }

                remainder /= bank[lane];
                exponent++;
                divisions++;
                if (exponent > maximum)
                {
                    var cost = HybridCostLedger.Zero.Add(
                        CostPhase.Ingress,
                        new HybridCostVector(
                            GateCost.Zero,
                            TrialRemainders: checks,
                            FactorDivisions: divisions,
                            BinaryOperandBits: operandBits,
                            LaneWrites: lane,
                            MetadataWrites: lane));
                    return Failed<HybridInteger>(
                        "BINARY_INGRESS",
                        HybridFailure.ExponentOverflow,
                        HybridDomain.Boundary,
                        cost,
                        null,
                        "Bounded valuation extraction from exact binary magnitude",
                        $"Prime {bank[lane]} exceeds the {exponentWidth}-bit exponent lane.");
                }
            }

            words[lane] = BinaryWord.FromUnsigned(exponent, exponentWidth);
        }

        var result = new HybridInteger(
            bank,
            exponentWidth,
            value.Sign,
            remainder,
            words,
            Enumerable.Repeat(ValuationKnowledge.KnownExact, bank.Count),
            Enumerable.Repeat(LaneProvenance.BinaryIngress, bank.Count));
        var ingress = new HybridCostVector(
            GateCost.Zero,
            TrialRemainders: checks,
            FactorDivisions: divisions,
            BinaryOperandBits: operandBits,
            LaneWrites: bank.Count,
            MetadataWrites: bank.Count + 2L);
        return Succeeded(
            "BINARY_INGRESS",
            result,
            HybridDomain.Boundary,
            HybridCostLedger.Zero.Add(CostPhase.Ingress, ingress),
            null,
            "Divide only by configured bank primes; no general factorization");
    }

    public static HybridResult<HybridInteger> FromStructured(
        int sign,
        BigInteger cofactor,
        IEnumerable<BigInteger> exponents,
        ValuationBank bank,
        int exponentWidth,
        IEnumerable<ValuationKnowledge>? knowledge = null)
    {
        ArgumentNullException.ThrowIfNull(bank);
        ArgumentNullException.ThrowIfNull(exponents);
        if (exponentWidth <= 0 || exponentWidth > MaximumExponentWidth)
        {
            return InvalidStructured($"Exponent width must be in 1..{MaximumExponentWidth}.");
        }

        var exponentArray = exponents.Take(bank.Count + 1).ToArray();
        var knowledgeArray = knowledge?.Take(bank.Count + 1).ToArray() ??
            Enumerable.Repeat(ValuationKnowledge.KnownExact, bank.Count).ToArray();
        if (exponentArray.Length != bank.Count || knowledgeArray.Length != bank.Count ||
            knowledgeArray.Any(state => !Enum.IsDefined(state)) ||
            sign is < -1 or > 1 || cofactor < BigInteger.Zero)
        {
            return InvalidStructured("Lane counts, sign, or cofactor violate the representation contract.");
        }

        var maximum = (BigInteger.One << exponentWidth) - BigInteger.One;
        if (exponentArray.Any(exponent => exponent < BigInteger.Zero || exponent > maximum))
        {
            return Failed<HybridInteger>(
                "STRUCTURED_INGRESS",
                HybridFailure.ExponentOverflow,
                HybridDomain.Boundary,
                HybridCostLedger.Zero,
                null,
                "Verified structured ingress with bounded exponent words");
        }

        long remainderChecks = 0;
        if (sign == 0)
        {
            if (!cofactor.IsZero || exponentArray.Any(exponent => !exponent.IsZero) ||
                knowledgeArray.Any(state => state != ValuationKnowledge.KnownExact))
            {
                return InvalidStructured("Zero has one canonical form: zero cofactor, zero exponents, exact lanes.");
            }
        }
        else
        {
            if (cofactor <= BigInteger.Zero)
            {
                return InvalidStructured("Nonzero structured values require a positive cofactor.");
            }

            for (var lane = 0; lane < bank.Count; lane++)
            {
                if (knowledgeArray[lane] != ValuationKnowledge.KnownExact)
                {
                    continue;
                }

                remainderChecks++;
                if (cofactor % bank[lane] == BigInteger.Zero)
                {
                    return InvalidStructured(
                        $"Exact lane {lane} is contradicted by a cofactor divisible by {bank[lane]}.",
                        remainderChecks);
                }
            }
        }

        var result = new HybridInteger(
            bank,
            exponentWidth,
            sign,
            cofactor,
            exponentArray.Select(exponent => BinaryWord.FromUnsigned(exponent, exponentWidth)),
            knowledgeArray,
            Enumerable.Repeat(LaneProvenance.StructuredIngress, bank.Count));
        var cost = new HybridCostVector(
            GateCost.Zero,
            CofactorRemainders: remainderChecks,
            BinaryOperandBits: BitLength(cofactor),
            LaneReads: bank.Count,
            LaneWrites: bank.Count,
            MetadataReads: bank.Count + 2L,
            MetadataWrites: bank.Count + 2L);
        return Succeeded(
            "STRUCTURED_INGRESS",
            result,
            HybridDomain.Boundary,
            HybridCostLedger.Zero.Add(CostPhase.Ingress, cost),
            null,
            "Verified exact cofactor and bounded valuation claims");

        HybridResult<HybridInteger> InvalidStructured(string detail, long checks = 0) =>
            Failed<HybridInteger>(
                "STRUCTURED_INGRESS",
                HybridFailure.InvalidStructuredIngress,
                HybridDomain.Boundary,
                HybridCostLedger.Zero.Add(
                    CostPhase.Ingress,
                    new HybridCostVector(GateCost.Zero, CofactorRemainders: checks)),
                null,
                "Malformed structured states are rejected before becoming executable values",
                detail);
    }

    public HybridResult<HybridInteger> Multiply(HybridInteger other)
    {
        if (!Compatible(other, "MULTIPLY", out var failure))
        {
            return failure!;
        }

        if (IsZero || other.IsZero)
        {
            return Succeeded(
                "MULTIPLY",
                CreateZero(Bank, ExponentWidth, LaneProvenance.Multiplication),
                HybridDomain.BankNative,
                HybridCostLedger.Zero.Add(
                    CostPhase.Native,
                    new HybridCostVector(
                        GateCost.Zero,
                        MetadataReads: 2,
                        MetadataWrites: Bank.Count + 2L)),
                Validity,
                "Zero tag absorbs multiplication without reading numeric payloads");
        }

        var words = new BinaryWord[LaneCount];
        var carries = new BitState[LaneCount];
        var gateCosts = new GateCost[LaneCount];
        for (var lane = 0; lane < LaneCount; lane++)
        {
            var added = BinaryCircuit.Add(_exponents[lane], other._exponents[lane]);
            words[lane] = added.Value;
            carries[lane] = added.Carry;
            gateCosts[lane] = added.Cost;
        }

        var bankGates = AggregateOverflow(carries, GateCost.Parallel(gateCosts));
        var nativeCost = new HybridCostVector(
            bankGates,
            LaneReads: LaneCount * 2L,
            LaneWrites: LaneCount,
            MetadataReads: LaneCount * 2L + 4,
            MetadataWrites: LaneCount + 2L);
        if (carries.Any(state => state == BitState.On))
        {
            return Failed<HybridInteger>(
                "MULTIPLY",
                HybridFailure.ExponentOverflow,
                HybridDomain.BankNative,
                HybridCostLedger.Zero.Add(CostPhase.Native, nativeCost),
                Validity,
                "Lane addition overflowed before cofactor multiplication; no result committed");
        }

        var width = Math.Max(BitLength(Cofactor), BitLength(other.Cofactor));
        nativeCost += new HybridCostVector(
            GateCost.Zero,
            CofactorMultiplications: 1,
            ModeledBinaryNands: ModeledMultiplyNands(width),
            BinaryOperandBits: checked(BitLength(Cofactor) + BitLength(other.Cofactor)));

        var states = new ValuationKnowledge[LaneCount];
        var sources = new LaneProvenance[LaneCount];
        for (var lane = 0; lane < LaneCount; lane++)
        {
            states[lane] = _knowledge[lane] == ValuationKnowledge.KnownExact &&
                           other._knowledge[lane] == ValuationKnowledge.KnownExact
                ? ValuationKnowledge.KnownExact
                : ValuationKnowledge.CertifiedLowerBound;
            sources[lane] = LaneProvenance.Multiplication;
        }

        var result = new HybridInteger(
            Bank,
            ExponentWidth,
            Sign * other.Sign,
            Cofactor * other.Cofactor,
            words,
            states,
            sources);
        return Succeeded(
            "MULTIPLY",
            result,
            LaneCount == 0 ? HybridDomain.CofactorArithmetic : HybridDomain.Mixed,
            HybridCostLedger.Zero.Add(CostPhase.Native, nativeCost),
            MergeValidity(Validity, other.Validity),
            "Valuation exponents add in NAND circuits; exact cofactors still multiply in binary arithmetic");
    }

    public HybridResult<HybridInteger> AddPreservingValuations(HybridInteger other)
    {
        if (!Compatible(other, "ADD_PRESERVE", out var failure))
        {
            return failure!;
        }

        if (IsZero)
        {
            return Succeeded(
                "ADD_PRESERVE",
                other.CopyWithProvenance(LaneProvenance.CommonLowerBoundAddition),
                HybridDomain.BankNative,
                HybridCostLedger.Zero.Add(CostPhase.Native, MetadataCopyCost(other)),
                Validity,
                "Adding zero preserves the other operand without refreshing it");
        }

        if (other.IsZero)
        {
            return Succeeded(
                "ADD_PRESERVE",
                CopyWithProvenance(LaneProvenance.CommonLowerBoundAddition),
                HybridDomain.BankNative,
                HybridCostLedger.Zero.Add(CostPhase.Native, MetadataCopyCost(this)),
                Validity,
                "Adding zero preserves the other operand without refreshing it");
        }

        var minima = new BigInteger[LaneCount];
        var leftResidual = Cofactor;
        var rightResidual = other.Cofactor;
        long powerMultiplications = 0;
        var bankGateCosts = new List<GateCost>(LaneCount);
        for (var lane = 0; lane < LaneCount; lane++)
        {
            var minimum = BinaryCircuit.Min(_exponents[lane], other._exponents[lane]);
            bankGateCosts.Add(minimum.Cost);
            minima[lane] = minimum.Value.ToUnsigned();
            var leftDifference = _exponents[lane].ToUnsigned() - minima[lane];
            var rightDifference = other._exponents[lane].ToUnsigned() - minima[lane];
            if (!leftDifference.IsZero)
            {
                leftResidual *= PowCounted(Bank[lane], leftDifference, ref powerMultiplications);
                powerMultiplications++;
            }

            if (!rightDifference.IsZero)
            {
                rightResidual *= PowCounted(Bank[lane], rightDifference, ref powerMultiplications);
                powerMultiplications++;
            }
        }

        var signedSum = Sign * leftResidual + other.Sign * rightResidual;
        var nativeCost = new HybridCostVector(
            GateCost.Parallel(bankGateCosts),
            CofactorAdditions: 1,
            CofactorMultiplications: powerMultiplications,
            BinaryOperandBits: checked(BitLength(leftResidual) + BitLength(rightResidual)),
            LaneReads: LaneCount * 2L,
            LaneWrites: LaneCount,
            MetadataReads: LaneCount * 2L + 4,
            MetadataWrites: LaneCount + 2L);
        if (signedSum.IsZero)
        {
            return Succeeded(
                "ADD_PRESERVE",
                CreateZero(Bank, ExponentWidth, LaneProvenance.CommonLowerBoundAddition),
                HybridDomain.Mixed,
                HybridCostLedger.Zero.Add(CostPhase.Native, nativeCost),
                MergeValidity(Validity, other.Validity),
                "Exact cancellation produces canonical zero");
        }

        var states = new ValuationKnowledge[LaneCount];
        var sources = new LaneProvenance[LaneCount];
        long transitions = 0;
        for (var lane = 0; lane < LaneCount; lane++)
        {
            var exponentsDiffer = _exponents[lane].ToUnsigned() != other._exponents[lane].ToUnsigned();
            var exactByUnequalValuation =
                _knowledge[lane] == ValuationKnowledge.KnownExact &&
                other._knowledge[lane] == ValuationKnowledge.KnownExact &&
                exponentsDiffer;
            states[lane] = exactByUnequalValuation
                ? ValuationKnowledge.KnownExact
                : ValuationKnowledge.CertifiedLowerBound;
            sources[lane] = exactByUnequalValuation
                ? LaneProvenance.UnequalValuationAddition
                : LaneProvenance.CommonLowerBoundAddition;
            if (states[lane] == ValuationKnowledge.CertifiedLowerBound)
            {
                transitions++;
            }
        }

        nativeCost += new HybridCostVector(GateCost.Zero, KnowledgeTransitions: transitions);
        var result = new HybridInteger(
            Bank,
            ExponentWidth,
            signedSum.Sign,
            BigInteger.Abs(signedSum),
            minima.Select(value => BinaryWord.FromUnsigned(value, ExponentWidth)),
            states,
            sources);
        return Succeeded(
            "ADD_PRESERVE",
            result,
            HybridDomain.Mixed,
            HybridCostLedger.Zero.Add(CostPhase.Native, nativeCost),
            MergeValidity(Validity, other.Validity),
            "Common valuations are retained; equal or uncertain lanes become explicit lower bounds, never guessed zero");
    }

    public HybridResult<HybridInteger> Negate()
    {
        var result = IsZero
            ? this
            : new HybridInteger(Bank, ExponentWidth, -Sign, Cofactor, _exponents, _knowledge, _provenance);
        return Succeeded(
            "NEGATE",
            result,
            HybridDomain.BankNative,
            HybridCostLedger.Zero.Add(
                CostPhase.Native,
                new HybridCostVector(GateCost.Zero, MetadataReads: 1, MetadataWrites: 1)),
            Validity,
            "Sign-tag inversion");
    }

    public HybridResult<HybridInteger> SubtractPreservingValuations(HybridInteger other)
    {
        var negated = other.Negate();
        var added = AddPreservingValuations(negated.Value!);
        return new HybridResult<HybridInteger>(
            added.Value,
            added.Receipt with
            {
                Operation = "SUBTRACT_PRESERVE",
                Cost = added.Receipt.Cost + negated.Receipt.Cost,
            });
    }

    public HybridResult<HybridInteger> RefreshLane(int lane)
    {
        if (lane < 0 || lane >= LaneCount)
        {
            return Failed<HybridInteger>(
                "REFRESH_LANE",
                HybridFailure.InvalidLane,
                HybridDomain.Maintenance,
                HybridCostLedger.Zero,
                Validity,
                "Selected-prime valuation refresh");
        }

        if (IsZero || _knowledge[lane] == ValuationKnowledge.KnownExact)
        {
            return Succeeded(
                "REFRESH_LANE",
                this,
                HybridDomain.Maintenance,
                HybridCostLedger.Zero.Add(
                    CostPhase.Maintenance,
                    new HybridCostVector(GateCost.Zero, LaneReads: 1, MetadataReads: 1)),
                Validity,
                "Already exact; no refresh work required");
        }

        var prime = Bank[lane];
        var residual = Cofactor;
        var additional = BigInteger.Zero;
        long checks = 0;
        long divisions = 0;
        while (true)
        {
            checks++;
            if (residual % prime != BigInteger.Zero)
            {
                break;
            }

            residual /= prime;
            additional++;
            divisions++;
        }

        var maximum = (BigInteger.One << ExponentWidth) - BigInteger.One;
        if (additional > maximum)
        {
            var overflowCost = new HybridCostVector(
                GateCost.Zero,
                TrialRemainders: checks,
                FactorDivisions: divisions,
                LaneReads: 1,
                MetadataReads: 2);
            return Failed<HybridInteger>(
                "REFRESH_LANE",
                HybridFailure.ExponentOverflow,
                HybridDomain.Maintenance,
                HybridCostLedger.Zero.Add(CostPhase.Maintenance, overflowCost),
                Validity,
                "Refresh is transactional; the extracted increment itself exceeds the lane width");
        }

        var added = BinaryCircuit.Add(_exponents[lane], BinaryWord.FromUnsigned(additional, ExponentWidth));
        var cost = new HybridCostVector(
            added.Cost,
            TrialRemainders: checks,
            FactorDivisions: divisions,
            LaneReads: 1,
            LaneWrites: 1,
            KnowledgeTransitions: 1,
            MetadataReads: 2,
            MetadataWrites: 2);
        if (added.Carry == BitState.On)
        {
            return Failed<HybridInteger>(
                "REFRESH_LANE",
                HybridFailure.ExponentOverflow,
                HybridDomain.Maintenance,
                HybridCostLedger.Zero.Add(CostPhase.Maintenance, cost),
                Validity,
                "Refresh is transactional; overflow leaves the original value usable");
        }

        var words = CopyWords();
        var states = (ValuationKnowledge[])_knowledge.Clone();
        var sources = (LaneProvenance[])_provenance.Clone();
        words[lane] = added.Value;
        states[lane] = ValuationKnowledge.KnownExact;
        sources[lane] = LaneProvenance.Refresh;
        var result = new HybridInteger(Bank, ExponentWidth, Sign, residual, words, states, sources);
        return Succeeded(
            "REFRESH_LANE",
            result,
            HybridDomain.Maintenance,
            HybridCostLedger.Zero.Add(CostPhase.Maintenance, cost),
            Validity,
            "Trial-divide the exact cofactor by one selected bank prime only");
    }

    public HybridResult<HybridInteger> Normalize()
    {
        var current = this;
        var ledger = HybridCostLedger.Zero;
        for (var lane = 0; lane < LaneCount; lane++)
        {
            if (current._knowledge[lane] == ValuationKnowledge.KnownExact)
            {
                continue;
            }

            var refreshed = current.RefreshLane(lane);
            ledger += refreshed.Receipt.Cost;
            if (!refreshed.Receipt.Succeeded)
            {
                return new HybridResult<HybridInteger>(
                    null,
                    refreshed.Receipt with
                    {
                        Operation = "NORMALIZE",
                        Cost = ledger,
                        Scope = "Transactional all-unknown-lane refresh; original remains valid",
                    });
            }

            current = refreshed.Value!;
        }

        return Succeeded(
            "NORMALIZE",
            current,
            HybridDomain.Maintenance,
            ledger,
            Validity,
            "Refresh only lanes marked as certified lower bounds");
    }

    public HybridQueryResult<ValuationAnswer> Valuation(int lane)
    {
        if (lane < 0 || lane >= LaneCount)
        {
            return new HybridQueryResult<ValuationAnswer>(
                null,
                false,
                Receipt(
                    "VALUATION",
                    false,
                    HybridFailure.InvalidLane,
                    HybridDomain.BankNative,
                    HybridCostLedger.Zero,
                    Validity,
                    null,
                    "Bank-local valuation query"));
        }

        if (IsZero)
        {
            var infinity = new ValuationAnswer(BigInteger.Zero, true, ValuationResultKind.PositiveInfinity);
            return new HybridQueryResult<ValuationAnswer>(
                infinity,
                true,
                Receipt(
                    "VALUATION",
                    true,
                    HybridFailure.None,
                    HybridDomain.BankNative,
                    HybridCostLedger.Zero.Add(
                        CostPhase.Native,
                        new HybridCostVector(GateCost.Zero, MetadataReads: 1)),
                    Validity,
                    Validity,
                    "v_p(0) is reported as positive infinity, never as a finite exponent"));
        }

        var answer = new ValuationAnswer(_exponents[lane].ToUnsigned(), _knowledge[lane] == ValuationKnowledge.KnownExact);
        var cost = HybridCostLedger.Zero.Add(
            CostPhase.Native,
            new HybridCostVector(GateCost.Zero, LaneReads: 1, MetadataReads: 1));
        return new HybridQueryResult<ValuationAnswer>(
            answer,
            answer.IsExact,
            Receipt(
                "VALUATION",
                true,
                HybridFailure.None,
                HybridDomain.BankNative,
                cost,
                Validity,
                Validity,
                answer.IsExact ? "Exact bank-local valuation" : "Certified lower bound; exact valuation remains UNKNOWN"));
    }

    public HybridQueryResult<bool?> IsEven()
    {
        if (IsZero)
        {
            return KnownQuery("PARITY", true, HybridDomain.BankNative, "Zero is even by definition.");
        }

        var lane = Bank.IndexOf(2);
        if (lane < 0)
        {
            var even = Cofactor.IsEven;
            return new HybridQueryResult<bool?>(
                even,
                true,
                Receipt(
                    "PARITY",
                    true,
                    HybridFailure.None,
                    HybridDomain.CofactorArithmetic,
                    HybridCostLedger.Zero.Add(
                        CostPhase.Native,
                        new HybridCostVector(GateCost.Zero, BinaryOperandBits: 1, MetadataReads: 1)),
                    Validity,
                    Validity,
                    "No 2-lane: inspect the exact cofactor's low bit"));
        }

        var exponent = _exponents[lane].ToUnsigned();
        if (exponent > BigInteger.Zero)
        {
            return KnownQuery("PARITY", true, HybridDomain.BankNative, "Positive certified 2-valuation implies evenness.");
        }

        if (_knowledge[lane] == ValuationKnowledge.KnownExact)
        {
            return KnownQuery("PARITY", false, HybridDomain.BankNative, "Exact zero 2-valuation implies oddness.");
        }

        return new HybridQueryResult<bool?>(
            null,
            false,
            Receipt(
                "PARITY",
                true,
                HybridFailure.None,
                HybridDomain.BankNative,
                HybridCostLedger.Zero.Add(
                    CostPhase.Native,
                    new HybridCostVector(GateCost.Zero, LaneReads: 1, MetadataReads: 1)),
                Validity,
                Validity,
                "Zero is only a lower bound here; parity is explicitly UNKNOWN"));
    }

    public HybridResult<HybridInteger> Power(int exponent)
    {
        if (exponent < 0)
        {
            return Failed<HybridInteger>(
                "POWER",
                HybridFailure.NegativePower,
                HybridDomain.Mixed,
                HybridCostLedger.Zero,
                Validity,
                "Integer representation supports nonnegative powers only");
        }

        if (exponent == 0)
        {
            return Succeeded(
                "POWER",
                Identity(Bank, ExponentWidth),
                HybridDomain.BankNative,
                HybridCostLedger.Zero,
                Validity,
                "The zeroth power is the multiplicative identity, including 0^0 by this VM contract");
        }

        if (IsZero)
        {
            return Succeeded(
                "POWER",
                CreateZero(Bank, ExponentWidth, LaneProvenance.Power),
                HybridDomain.BankNative,
                HybridCostLedger.Zero.Add(
                    CostPhase.Native,
                    new HybridCostVector(GateCost.Zero, MetadataReads: 2, MetadataWrites: 2)),
                Validity,
                "A positive power of explicit zero remains explicit zero");
        }

        var scalar = new BigInteger(exponent);
        var maximum = (BigInteger.One << ExponentWidth) - BigInteger.One;
        var words = new BinaryWord[LaneCount];
        var gateCosts = new List<GateCost>(LaneCount);
        var overflowStates = new List<BitState>();
        if (scalar <= maximum)
        {
            var scalarWord = BinaryWord.FromUnsigned(scalar, ExponentWidth);
            for (var lane = 0; lane < LaneCount; lane++)
            {
                var multiplied = BinaryCircuit.Multiply(_exponents[lane], scalarWord);
                gateCosts.Add(multiplied.Cost);
                for (var bit = ExponentWidth; bit < multiplied.Value.Width; bit++)
                {
                    overflowStates.Add(multiplied.Value[bit]);
                }

                var product = multiplied.Value.ToUnsigned();
                if (product <= maximum)
                {
                    words[lane] = BinaryWord.FromUnsigned(product, ExponentWidth);
                }
            }
        }
        else
        {
            var zeroWord = BinaryWord.Zero(ExponentWidth);
            for (var lane = 0; lane < LaneCount; lane++)
            {
                var compared = BinaryCircuit.Compare(_exponents[lane], zeroWord);
                gateCosts.Add(compared.Cost);
                overflowStates.Add(compared.Greater);
                words[lane] = BinaryWord.Zero(ExponentWidth);
            }
        }

        var bankGates = AggregateOverflow(overflowStates, GateCost.Parallel(gateCosts));
        var cost = new HybridCostVector(
            bankGates,
            LaneReads: LaneCount,
            LaneWrites: LaneCount,
            MetadataReads: LaneCount + 2L,
            MetadataWrites: LaneCount + 2L);
        if (overflowStates.Any(state => state == BitState.On))
        {
            return Failed<HybridInteger>(
                "POWER",
                HybridFailure.ExponentOverflow,
                HybridDomain.BankNative,
                HybridCostLedger.Zero.Add(CostPhase.Native, cost),
                Validity,
                "Exponent-lane scaling overflowed before cofactor exponentiation; no truncated result is returned");
        }

        var powerModel = ModelSquareAndMultiply(Cofactor, exponent);
        cost += new HybridCostVector(
            GateCost.Zero,
            CofactorMultiplications: powerModel.Multiplications,
            ModeledBinaryNands: powerModel.ModeledNands,
            BinaryOperandBits: powerModel.OperandBits);
        var result = new HybridInteger(
            Bank,
            ExponentWidth,
            Sign < 0 && (exponent & 1) == 1 ? -1 : 1,
            BigInteger.Pow(Cofactor, exponent),
            words,
            _knowledge,
            Enumerable.Repeat(LaneProvenance.Power, LaneCount));
        return Succeeded(
            "POWER",
            result,
            HybridDomain.Mixed,
            HybridCostLedger.Zero.Add(CostPhase.Native, cost),
            Validity,
            "Bank exponents scale with explicit overflow reduction; cofactor work uses a changing-width square-and-multiply NAND proxy");
    }

    public HybridPayloadMetrics MeasurePayload()
    {
        var provenanceStates = Enum.GetValues<LaneProvenance>().Length;
        var provenanceBits = CeilingLog2(provenanceStates);
        return new HybridPayloadMetrics(
            SignAndZeroBits: 2,
            ExponentBits: checked((long)LaneCount * ExponentWidth),
            CofactorBits: IsZero ? 0 : BitLength(Cofactor),
            KnowledgeBits: LaneCount,
            ProvenanceBits: checked((long)LaneCount * provenanceBits),
            BankCatalogBits: Bank.CatalogPayloadBits);
    }

    public HybridQueryResult<BigInteger> Reconstruct()
    {
        if (IsZero)
        {
            return new HybridQueryResult<BigInteger>(
                BigInteger.Zero,
                true,
                Receipt(
                    "RECONSTRUCT",
                    true,
                    HybridFailure.None,
                    HybridDomain.Boundary,
                    HybridCostLedger.Zero,
                    Validity,
                    Validity,
                    "Explicit zero tag"));
        }

        var magnitude = Cofactor;
        long multiplications = 0;
        for (var lane = 0; lane < LaneCount; lane++)
        {
            magnitude *= PowCounted(Bank[lane], _exponents[lane].ToUnsigned(), ref multiplications);
            if (_exponents[lane].ToUnsigned() > BigInteger.Zero)
            {
                multiplications++;
            }
        }

        var value = Sign * magnitude;
        var cost = new HybridCostVector(
            GateCost.Zero,
            ReconstructionMultiplications: multiplications,
            BinaryOperandBits: BitLength(value),
            LaneReads: LaneCount,
            MetadataReads: LaneCount + 2L);
        return new HybridQueryResult<BigInteger>(
            value,
            true,
            Receipt(
                "RECONSTRUCT",
                true,
                HybridFailure.None,
                HybridDomain.Boundary,
                HybridCostLedger.Zero.Add(CostPhase.Egress, cost),
                Validity,
                Validity,
                "Exact ordinary signed magnitude; knowledge metadata does not affect value exactness"));
    }

    public bool Equals(HybridInteger? other) =>
        other is not null &&
        Bank.Equals(other.Bank) &&
        ExponentWidth == other.ExponentWidth &&
        Sign == other.Sign &&
        Cofactor == other.Cofactor &&
        _exponents.SequenceEqual(other._exponents) &&
        _knowledge.SequenceEqual(other._knowledge);

    public override bool Equals(object? obj) => obj is HybridInteger other && Equals(other);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Bank);
        hash.Add(ExponentWidth);
        hash.Add(Sign);
        hash.Add(Cofactor);
        foreach (var word in _exponents)
        {
            hash.Add(word);
        }

        foreach (var state in _knowledge)
        {
            hash.Add(state);
        }

        return hash.ToHashCode();
    }

    public override string ToString()
    {
        if (IsZero)
        {
            return "0";
        }

        var lanes = Enumerable.Range(0, LaneCount)
            .Where(lane => !_exponents[lane].ToUnsigned().IsZero || _knowledge[lane] != ValuationKnowledge.KnownExact)
            .Select(lane =>
            {
                var suffix = _knowledge[lane] == ValuationKnowledge.KnownExact ? string.Empty : "+?";
                return $"{Bank[lane]}^{_exponents[lane].ToUnsigned()}{suffix}";
            });
        return $"{(Sign < 0 ? "-" : string.Empty)}{Cofactor} * [{string.Join(", ", lanes)}]";
    }

    private bool Compatible(HybridInteger other, string operation, out HybridResult<HybridInteger>? failure)
    {
        ArgumentNullException.ThrowIfNull(other);
        if (!Bank.Equals(other.Bank))
        {
            failure = Failed<HybridInteger>(
                operation,
                HybridFailure.BankMismatch,
                HybridDomain.None,
                HybridCostLedger.Zero,
                Validity,
                "Operands require identical ordered bank primes; migrate explicitly first");
            return false;
        }

        if (ExponentWidth != other.ExponentWidth)
        {
            failure = Failed<HybridInteger>(
                operation,
                HybridFailure.ExponentWidthMismatch,
                HybridDomain.None,
                HybridCostLedger.Zero,
                Validity,
                "Operands require identical bounded exponent widths");
            return false;
        }

        failure = null;
        return true;
    }

    private static HybridInteger CreateZero(ValuationBank bank, int width, LaneProvenance provenance) =>
        new(
            bank,
            width,
            0,
            BigInteger.Zero,
            ZeroWords(bank.Count, width),
            Enumerable.Repeat(ValuationKnowledge.KnownExact, bank.Count),
            Enumerable.Repeat(provenance, bank.Count));

    private static IEnumerable<BinaryWord> ZeroWords(int count, int width) =>
        Enumerable.Range(0, count).Select(_ => BinaryWord.Zero(width));

    private BinaryWord[] CopyWords() => _exponents.Select(word => new BinaryWord(word.CopyBits())).ToArray();

    private HybridInteger CopyWithProvenance(LaneProvenance provenance) =>
        new(
            Bank,
            ExponentWidth,
            Sign,
            Cofactor,
            _exponents,
            _knowledge,
            Enumerable.Repeat(provenance, LaneCount));

    private int ValidateLane(int lane) => lane >= 0 && lane < LaneCount
        ? lane
        : throw new ArgumentOutOfRangeException(nameof(lane));

    private static HybridValidity MergeValidity(HybridValidity left, HybridValidity right) =>
        left == HybridValidity.Partial || right == HybridValidity.Partial
            ? HybridValidity.Partial
            : HybridValidity.Canonical;

    private static HybridCostVector MetadataCopyCost(HybridInteger value) =>
        new(
            GateCost.Zero,
            LaneReads: value.LaneCount,
            LaneWrites: value.LaneCount,
            MetadataReads: value.LaneCount + 2L,
            MetadataWrites: value.LaneCount + 2L);

    private HybridQueryResult<bool?> KnownQuery(string operation, bool value, HybridDomain domain, string scope) =>
        new(
            value,
            true,
            Receipt(
                operation,
                true,
                HybridFailure.None,
                domain,
                HybridCostLedger.Zero.Add(
                    CostPhase.Native,
                    new HybridCostVector(GateCost.Zero, LaneReads: 1, MetadataReads: 1)),
                Validity,
                Validity,
                scope));

    private static BigInteger PowCounted(int value, BigInteger exponent, ref long multiplicationCount)
    {
        var result = BigInteger.One;
        var factor = new BigInteger(value);
        var remaining = exponent;
        while (remaining > BigInteger.Zero)
        {
            if (!remaining.IsEven)
            {
                result *= factor;
                multiplicationCount++;
            }

            remaining >>= 1;
            if (remaining > BigInteger.Zero)
            {
                factor *= factor;
                multiplicationCount++;
            }
        }

        return result;
    }

    internal static long BitLength(BigInteger value)
    {
        value = BigInteger.Abs(value);
        if (value.IsZero)
        {
            return 1;
        }

        var bytes = value.ToByteArray(isUnsigned: true, isBigEndian: true);
        var first = bytes[0];
        var leading = 0;
        for (var mask = 0x80; (first & mask) == 0; mask >>= 1)
        {
            leading++;
        }

        return checked(bytes.LongLength * 8 - leading);
    }

    internal static long ModeledMultiplyNands(long width) => checked(32L * width * width);

    private static PowerCostModel ModelSquareAndMultiply(BigInteger value, int exponent)
    {
        var result = BigInteger.One;
        var factor = value;
        var remaining = exponent;
        long multiplications = 0;
        long modeledNands = 0;
        long operandBits = 0;
        while (remaining > 0)
        {
            if ((remaining & 1) == 1)
            {
                var leftBits = BitLength(result);
                var rightBits = BitLength(factor);
                modeledNands = checked(modeledNands + ModeledMultiplyNands(Math.Max(leftBits, rightBits)));
                operandBits = checked(operandBits + leftBits + rightBits);
                result *= factor;
                multiplications++;
            }

            remaining >>= 1;
            if (remaining > 0)
            {
                var bits = BitLength(factor);
                modeledNands = checked(modeledNands + ModeledMultiplyNands(bits));
                operandBits = checked(operandBits + bits * 2);
                factor *= factor;
                multiplications++;
            }
        }

        return new PowerCostModel(multiplications, modeledNands, operandBits);
    }

    private readonly record struct PowerCostModel(long Multiplications, long ModeledNands, long OperandBits);

    private static int CeilingLog2(int count)
    {
        if (count <= 1)
        {
            return 0;
        }

        var bits = 0;
        var value = count - 1;
        while (value > 0)
        {
            bits++;
            value >>= 1;
        }

        return bits;
    }

    private static GateCost AggregateOverflow(IReadOnlyList<BitState> states, GateCost laneCost)
    {
        if (states.Count == 0)
        {
            return laneCost;
        }

        var network = new GateNetwork();
        var signals = states.Select(state => network.Input(state, laneCost.CriticalPathDepth)).ToList();
        while (signals.Count > 1)
        {
            var next = new List<Signal>((signals.Count + 1) / 2);
            for (var index = 0; index < signals.Count; index += 2)
            {
                next.Add(index + 1 < signals.Count
                    ? network.Or(signals[index], signals[index + 1])
                    : signals[index]);
            }

            signals = next;
        }

        return new GateCost(
            checked(laneCost.NandEvaluations + network.Cost.NandEvaluations),
            network.Cost.CriticalPathDepth);
    }

    private static HybridResult<T> Failed<T>(
        string operation,
        HybridFailure failure,
        HybridDomain domain,
        HybridCostLedger cost,
        HybridValidity? before,
        string scope,
        string? detail = null) where T : class =>
        new(
            null,
            Receipt(operation, false, failure, domain, cost, before, null, scope, detail));

    private static HybridResult<HybridInteger> Succeeded(
        string operation,
        HybridInteger value,
        HybridDomain domain,
        HybridCostLedger cost,
        HybridValidity? before,
        string scope,
        string? detail = null) =>
        new(
            value,
            Receipt(operation, true, HybridFailure.None, domain, cost, before, value.Validity, scope, detail));

    private static HybridReceipt Receipt(
        string operation,
        bool succeeded,
        HybridFailure failure,
        HybridDomain domain,
        HybridCostLedger cost,
        HybridValidity? before,
        HybridValidity? after,
        string scope,
        string? detail = null) =>
        new(operation, succeeded, failure, domain, cost, before, after, scope, detail);
}
