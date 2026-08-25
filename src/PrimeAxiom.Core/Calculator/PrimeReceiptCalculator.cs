using System.Globalization;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;

namespace PrimeAxiom.Core.Calculator;

public enum PrimeReceiptStatus
{
    ExactZero,
    ExactUnit,
    ExactFactorization,
    PartialBudget,
}

public enum PrimeFactorProofKind
{
    RadixPrime,
    OrderedTrialDivision,
    TerminalResidualBound,
    InheritedFromParentReceipts,
}

public enum PrimeReceiptOrigin
{
    DiscoveredFromMagnitude,
    ComposedFromReceipts,
    FactoredAfterMagnitudeAddition,
}

public sealed record PrimeReceiptPolicy
{
    public const long DefaultMaxOddCandidates = 1_000_000;

    public PrimeReceiptPolicy(long maxOddCandidates = DefaultMaxOddCandidates)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maxOddCandidates);
        MaxOddCandidates = maxOddCandidates;
    }

    public long MaxOddCandidates { get; }
}

public sealed record PrimePowerReceipt(
    string PrimeDecimal,
    int Exponent,
    PrimeFactorProofKind ProofKind);

public sealed record PrimeReceiptWork(
    long RadixExtractions,
    long OddCandidatesExamined,
    long RemainderChecks,
    long ExactFactorDivisions)
{
    public static PrimeReceiptWork Zero { get; } = new(0, 0, 0, 0);

    public static PrimeReceiptWork operator +(PrimeReceiptWork left, PrimeReceiptWork right) =>
        new(
            checked(left.RadixExtractions + right.RadixExtractions),
            checked(left.OddCandidatesExamined + right.OddCandidatesExamined),
            checked(left.RemainderChecks + right.RemainderChecks),
            checked(left.ExactFactorDivisions + right.ExactFactorDivisions));
}

public sealed class PrimeReceipt
{
    internal PrimeReceipt(
        string schema,
        string protocolId,
        string receiptId,
        string algorithm,
        PrimeReceiptOrigin origin,
        IReadOnlyList<string> parentReceiptIds,
        PrimeReceiptPolicy? policy,
        string canonicalInputDecimal,
        string inputSha256,
        int sign,
        long absoluteBitLength,
        PrimeReceiptStatus status,
        IReadOnlyList<PrimePowerReceipt> primePowers,
        string unresolvedCofactorDecimal,
        string testedThroughOddCandidateDecimal,
        bool reconstructionVerified,
        bool? magnitudeIsPrime,
        bool? integerIsPrime,
        PrimeReceiptWork work,
        string structure,
        string claimCeiling)
    {
        Schema = schema;
        ProtocolId = protocolId;
        ReceiptId = receiptId;
        Algorithm = algorithm;
        Origin = origin;
        ParentReceiptIds = parentReceiptIds;
        Policy = policy;
        CanonicalInputDecimal = canonicalInputDecimal;
        InputSha256 = inputSha256;
        Sign = sign;
        AbsoluteBitLength = absoluteBitLength;
        Status = status;
        PrimePowers = primePowers;
        UnresolvedCofactorDecimal = unresolvedCofactorDecimal;
        TestedThroughOddCandidateDecimal = testedThroughOddCandidateDecimal;
        ReconstructionVerified = reconstructionVerified;
        MagnitudeIsPrime = magnitudeIsPrime;
        IntegerIsPrime = integerIsPrime;
        Work = work;
        Structure = structure;
        ClaimCeiling = claimCeiling;
    }

    public string Schema { get; }
    public string ProtocolId { get; }
    public string ReceiptId { get; }
    public string Algorithm { get; }
    public PrimeReceiptOrigin Origin { get; }
    public IReadOnlyList<string> ParentReceiptIds { get; }
    public PrimeReceiptPolicy? Policy { get; }
    public string CanonicalInputDecimal { get; }
    public string InputSha256 { get; }
    public int Sign { get; }
    public long AbsoluteBitLength { get; }
    public PrimeReceiptStatus Status { get; }
    public IReadOnlyList<PrimePowerReceipt> PrimePowers { get; }
    public string UnresolvedCofactorDecimal { get; }
    public string TestedThroughOddCandidateDecimal { get; }
    public bool ReconstructionVerified { get; }
    public bool? MagnitudeIsPrime { get; }
    public bool? IntegerIsPrime { get; }
    public PrimeReceiptWork Work { get; }
    public string Structure { get; }
    public string ClaimCeiling { get; }
}

/// <summary>
/// Deterministic, budgeted prime-structure exposure at an explicit exact-integer boundary.
/// This is factor discovery and receipt construction, not a gate-level arithmetic claim.
/// </summary>
public static class PrimeReceiptCalculator
{
    public const string Schema = "prime-axiom-prime-receipt-v1";
    public const string ProtocolId = "PAS-BUILD003-PRIME-RECEIPT-0001";
    public const string TrialDivisionAlgorithm = "ORDERED_TRIAL_DIVISION_V1";
    public const string CompositionAlgorithm = "EXPONENT_MAP_COMPOSITION_V1";
    public const int DefaultCliMaxDecimalDigits = 4_096;

    private const string CompleteClaimCeiling =
        "Exact for this input under deterministic ordered trial division; this is not a succinct primality certificate or a general factoring-performance claim.";

    private const string PartialClaimCeiling =
        "Reported prime powers and reconstruction are exact; the residual may be prime or composite and carries no primality claim.";

    public static PrimeReceipt Analyze(
        BigInteger value,
        PrimeReceiptPolicy? policy = null) =>
        AnalyzeCore(
            value,
            policy,
            PrimeReceiptOrigin.DiscoveredFromMagnitude,
            Array.Empty<string>());

    internal static PrimeReceipt AnalyzeAfterMagnitudeAddition(
        BigInteger value,
        PrimeReceiptPolicy policy,
        IReadOnlyList<PrimeReceipt> parents)
    {
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(parents);
        var frozenParents = parents.ToArray();
        if (frozenParents.Length != 2 || frozenParents.Any(parent => parent is null || !VerifyIntegrity(parent)))
        {
            throw new ArgumentException(
                "A derived addition receipt requires exactly two integrity-verified parent receipts.",
                nameof(parents));
        }

        return AnalyzeCore(
            value,
            policy,
            PrimeReceiptOrigin.FactoredAfterMagnitudeAddition,
            frozenParents.Select(parent => parent.ReceiptId).ToArray());
    }

    private static PrimeReceipt AnalyzeCore(
        BigInteger value,
        PrimeReceiptPolicy? policy,
        PrimeReceiptOrigin origin,
        IReadOnlyList<string> parentReceiptIds)
    {
        policy ??= new PrimeReceiptPolicy();
        var canonicalInput = value.ToString(CultureInfo.InvariantCulture);
        var inputSha256 = Sha256(canonicalInput);
        var sign = value.Sign;
        var magnitude = BigInteger.Abs(value);
        var bitLength = magnitude.IsZero ? 0 : magnitude.GetBitLength();

        if (magnitude.IsZero)
        {
            return CreateReceipt(
                TrialDivisionAlgorithm,
                origin,
                parentReceiptIds,
                policy,
                canonicalInput,
                inputSha256,
                sign,
                bitLength,
                PrimeReceiptStatus.ExactZero,
                Array.Empty<PrimePowerReceipt>(),
                BigInteger.Zero,
                BigInteger.One,
                reconstructionVerified: true,
                magnitudeIsPrime: false,
                integerIsPrime: false,
                PrimeReceiptWork.Zero,
                "0",
                CompleteClaimCeiling);
        }

        if (magnitude.IsOne)
        {
            return CreateReceipt(
                TrialDivisionAlgorithm,
                origin,
                parentReceiptIds,
                policy,
                canonicalInput,
                inputSha256,
                sign,
                bitLength,
                PrimeReceiptStatus.ExactUnit,
                Array.Empty<PrimePowerReceipt>(),
                BigInteger.One,
                BigInteger.One,
                reconstructionVerified: true,
                magnitudeIsPrime: false,
                integerIsPrime: false,
                PrimeReceiptWork.Zero,
                sign < 0 ? "-1" : "1",
                CompleteClaimCeiling);
        }

        var primePowers = new List<PrimePowerReceipt>();
        var remaining = magnitude;
        long radixExtractions = 0;
        while (remaining.IsEven)
        {
            remaining >>= 1;
            radixExtractions++;
        }

        if (radixExtractions > 0)
        {
            primePowers.Add(new PrimePowerReceipt(
                "2",
                checked((int)radixExtractions),
                PrimeFactorProofKind.RadixPrime));
        }

        long oddCandidatesExamined = 0;
        long remainderChecks = 0;
        long exactFactorDivisions = 0;
        var candidate = new BigInteger(3);
        var testedThrough = BigInteger.One;
        var budgetEnded = false;

        while (remaining > BigInteger.One && candidate <= remaining / candidate)
        {
            if (oddCandidatesExamined >= policy.MaxOddCandidates)
            {
                budgetEnded = true;
                break;
            }

            oddCandidatesExamined++;
            testedThrough = candidate;
            remainderChecks++;
            if (remaining % candidate != BigInteger.Zero)
            {
                candidate += 2;
                continue;
            }

            var exponent = 0;
            do
            {
                remaining /= candidate;
                exactFactorDivisions++;
                exponent++;
                if (remaining.IsOne)
                {
                    break;
                }

                remainderChecks++;
            }
            while (remaining % candidate == BigInteger.Zero);

            primePowers.Add(new PrimePowerReceipt(
                candidate.ToString(CultureInfo.InvariantCulture),
                exponent,
                PrimeFactorProofKind.OrderedTrialDivision));
            candidate += 2;
        }

        if (!budgetEnded && remaining > BigInteger.One)
        {
            primePowers.Add(new PrimePowerReceipt(
                remaining.ToString(CultureInfo.InvariantCulture),
                1,
                PrimeFactorProofKind.TerminalResidualBound));
            remaining = BigInteger.One;
        }

        var status = budgetEnded ? PrimeReceiptStatus.PartialBudget : PrimeReceiptStatus.ExactFactorization;
        var reconstructedMagnitude = ReconstructMagnitude(primePowers, remaining);
        var reconstructionVerified = reconstructedMagnitude == magnitude;
        bool? magnitudeIsPrime = status == PrimeReceiptStatus.ExactFactorization
            ? primePowers.Count == 1 && primePowers[0].Exponent == 1
            : null;
        bool? integerIsPrime = sign < 0
            ? false
            : magnitudeIsPrime;
        var work = new PrimeReceiptWork(
            radixExtractions,
            oddCandidatesExamined,
            remainderChecks,
            exactFactorDivisions);
        var structure = FormatStructure(sign, primePowers, remaining, status);

        return CreateReceipt(
            TrialDivisionAlgorithm,
            origin,
            parentReceiptIds,
            policy,
            canonicalInput,
            inputSha256,
            sign,
            bitLength,
            status,
            primePowers,
            remaining,
            testedThrough,
            reconstructionVerified,
            magnitudeIsPrime,
            integerIsPrime,
            work,
            structure,
            status == PrimeReceiptStatus.PartialBudget ? PartialClaimCeiling : CompleteClaimCeiling);
    }

    public static PrimeReceipt ComposeExact(IReadOnlyList<PrimeReceipt> inputs)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        var frozenInputs = inputs.ToArray();
        if (frozenInputs.Length == 0)
        {
            throw new ArgumentException("At least one receipt is required.", nameof(inputs));
        }

        foreach (var receipt in frozenInputs)
        {
            ArgumentNullException.ThrowIfNull(receipt);
            if (!VerifyIntegrity(receipt) || receipt.Status == PrimeReceiptStatus.PartialBudget)
            {
                throw new ArgumentException(
                    "Composition requires exact receipts whose semantic integrity hashes and reconstruction invariants verify.",
                    nameof(inputs));
            }
        }

        var parents = frozenInputs.Select(receipt => receipt.ReceiptId).ToArray();
        if (frozenInputs.Any(receipt => receipt.Status == PrimeReceiptStatus.ExactZero))
        {
            return CreateReceipt(
                CompositionAlgorithm,
                PrimeReceiptOrigin.ComposedFromReceipts,
                parents,
                null,
                "0",
                Sha256("0"),
                0,
                0,
                PrimeReceiptStatus.ExactZero,
                Array.Empty<PrimePowerReceipt>(),
                BigInteger.Zero,
                BigInteger.One,
                reconstructionVerified: true,
                magnitudeIsPrime: false,
                integerIsPrime: false,
                PrimeReceiptWork.Zero,
                "0",
                "Exact structural zero derived from reconstruction-verified parent receipts; acquisition costs remain in the parents.");
        }

        var sign = 1;
        var merged = new SortedDictionary<BigInteger, int>();
        foreach (var receipt in frozenInputs)
        {
            sign *= receipt.Sign;
            foreach (var factor in receipt.PrimePowers)
            {
                var prime = BigInteger.Parse(factor.PrimeDecimal, CultureInfo.InvariantCulture);
                merged.TryGetValue(prime, out var previous);
                merged[prime] = checked(previous + factor.Exponent);
            }
        }

        var primePowers = merged
            .Select(pair => new PrimePowerReceipt(
                pair.Key.ToString(CultureInfo.InvariantCulture),
                pair.Value,
                PrimeFactorProofKind.InheritedFromParentReceipts))
            .ToArray();
        var magnitude = ReconstructMagnitude(primePowers, BigInteger.One);
        var value = sign < 0 ? -magnitude : magnitude;
        var canonicalInput = value.ToString(CultureInfo.InvariantCulture);
        var magnitudeIsPrime = primePowers.Length == 1 && primePowers[0].Exponent == 1;
        var outputStatus = magnitude.IsOne
            ? PrimeReceiptStatus.ExactUnit
            : PrimeReceiptStatus.ExactFactorization;

        return CreateReceipt(
            CompositionAlgorithm,
            PrimeReceiptOrigin.ComposedFromReceipts,
            parents,
            null,
            canonicalInput,
            Sha256(canonicalInput),
            value.Sign,
            magnitude.GetBitLength(),
            outputStatus,
            primePowers,
            BigInteger.One,
            BigInteger.One,
            reconstructionVerified: true,
            magnitudeIsPrime,
            value.Sign > 0 && magnitudeIsPrime,
            PrimeReceiptWork.Zero,
            FormatStructure(value.Sign, primePowers, BigInteger.One, outputStatus),
            "Exact exponent-map composition from reconstruction-verified parent receipts; parent acquisition and required magnitude reconstruction are not free.");
    }

    public static BigInteger Reconstruct(PrimeReceipt receipt)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        if (receipt.Status == PrimeReceiptStatus.ExactZero)
        {
            return BigInteger.Zero;
        }

        var magnitude = ReconstructMagnitude(
            receipt.PrimePowers,
            BigInteger.Parse(receipt.UnresolvedCofactorDecimal, CultureInfo.InvariantCulture));
        return receipt.Sign < 0 ? -magnitude : magnitude;
    }

    /// <summary>
    /// Replays the receipt's canonical encoding and internal arithmetic invariants.
    /// This detects accidental or post-construction mutation; it is not a signature,
    /// an authenticity guarantee, or an independently succinct primality proof.
    /// </summary>
    public static bool VerifyIntegrity(PrimeReceipt receipt)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        try
        {
            if (!string.Equals(receipt.Schema, Schema, StringComparison.Ordinal) ||
                !string.Equals(receipt.ProtocolId, ProtocolId, StringComparison.Ordinal) ||
                receipt.ParentReceiptIds is null ||
                receipt.PrimePowers is null ||
                receipt.Work is null ||
                receipt.CanonicalInputDecimal is null ||
                receipt.InputSha256 is null ||
                receipt.ReceiptId is null ||
                receipt.Algorithm is null ||
                receipt.UnresolvedCofactorDecimal is null ||
                receipt.TestedThroughOddCandidateDecimal is null ||
                receipt.Structure is null ||
                receipt.ClaimCeiling is null)
            {
                return false;
            }

            var maxDigits = receipt.CanonicalInputDecimal.Length;
            if (!TryParseCanonicalInteger(
                    receipt.CanonicalInputDecimal,
                    Math.Max(1, maxDigits),
                    out var value,
                    out _))
            {
                return false;
            }

            if (!string.Equals(receipt.InputSha256, Sha256(receipt.CanonicalInputDecimal), StringComparison.Ordinal) ||
                receipt.Sign != value.Sign ||
                receipt.AbsoluteBitLength != (value.IsZero ? 0 : BigInteger.Abs(value).GetBitLength()) ||
                receipt.Work.RadixExtractions < 0 ||
                receipt.Work.OddCandidatesExamined < 0 ||
                receipt.Work.RemainderChecks < 0 ||
                receipt.Work.ExactFactorDivisions < 0 ||
                receipt.ParentReceiptIds.Any(parent => !IsSha256(parent)))
            {
                return false;
            }

            if (!BigInteger.TryParse(
                    receipt.UnresolvedCofactorDecimal,
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out var unresolved) ||
                unresolved < BigInteger.Zero ||
                !BigInteger.TryParse(
                    receipt.TestedThroughOddCandidateDecimal,
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out var testedThrough) ||
                testedThrough < BigInteger.One)
            {
                return false;
            }

            var previousPrime = BigInteger.One;
            foreach (var factor in receipt.PrimePowers)
            {
                if (factor is null ||
                    !BigInteger.TryParse(
                        factor.PrimeDecimal,
                        NumberStyles.None,
                        CultureInfo.InvariantCulture,
                        out var prime) ||
                    prime <= previousPrime ||
                    factor.Exponent <= 0)
                {
                    return false;
                }

                previousPrime = prime;
            }

            if (!StatusInvariantsHold(receipt, value, unresolved) ||
                !receipt.ReconstructionVerified ||
                Reconstruct(receipt) != value ||
                !string.Equals(
                    receipt.Structure,
                    FormatStructure(receipt.Sign, receipt.PrimePowers, unresolved, receipt.Status),
                    StringComparison.Ordinal))
            {
                return false;
            }

            var expectedId = ComputeReceiptId(
                receipt.Algorithm,
                receipt.Origin,
                receipt.ParentReceiptIds,
                receipt.Policy,
                receipt.CanonicalInputDecimal,
                receipt.InputSha256,
                receipt.Sign,
                receipt.AbsoluteBitLength,
                receipt.Status,
                receipt.PrimePowers,
                receipt.UnresolvedCofactorDecimal,
                receipt.TestedThroughOddCandidateDecimal,
                receipt.ReconstructionVerified,
                receipt.MagnitudeIsPrime,
                receipt.IntegerIsPrime,
                receipt.Work,
                receipt.Structure,
                receipt.ClaimCeiling);
            return string.Equals(receipt.ReceiptId, expectedId, StringComparison.Ordinal);
        }
        catch (Exception exception) when (
            exception is ArgumentException or ArithmeticException or FormatException or OverflowException)
        {
            return false;
        }
    }

    public static bool TryParseCanonicalInteger(
        string? text,
        int maxDecimalDigits,
        out BigInteger value,
        out string error)
    {
        value = BigInteger.Zero;
        error = string.Empty;
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxDecimalDigits);

        if (string.IsNullOrEmpty(text))
        {
            error = "The integer is empty.";
            return false;
        }

        var offset = text[0] == '-' ? 1 : 0;
        if (offset == text.Length)
        {
            error = "A minus sign must be followed by digits.";
            return false;
        }

        var digitCount = text.Length - offset;
        if (digitCount > maxDecimalDigits)
        {
            error = $"The integer exceeds the {maxDecimalDigits.ToString(CultureInfo.InvariantCulture)}-digit CLI limit.";
            return false;
        }

        if (digitCount > 1 && text[offset] == '0')
        {
            error = "Leading zeroes are not canonical.";
            return false;
        }

        if (offset == 1 && digitCount == 1 && text[1] == '0')
        {
            error = "Negative zero is not canonical.";
            return false;
        }

        for (var index = offset; index < text.Length; index++)
        {
            if (text[index] is < '0' or > '9')
            {
                error = "Only canonical base-10 digits with an optional leading minus sign are accepted.";
                return false;
            }
        }

        if (!BigInteger.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out value))
        {
            error = "The integer could not be parsed.";
            return false;
        }

        return true;
    }

    private static PrimeReceipt CreateReceipt(
        string algorithm,
        PrimeReceiptOrigin origin,
        IReadOnlyList<string> parentReceiptIds,
        PrimeReceiptPolicy? policy,
        string canonicalInput,
        string inputSha256,
        int sign,
        long bitLength,
        PrimeReceiptStatus status,
        IReadOnlyList<PrimePowerReceipt> primePowers,
        BigInteger unresolvedCofactor,
        BigInteger testedThrough,
        bool reconstructionVerified,
        bool? magnitudeIsPrime,
        bool? integerIsPrime,
        PrimeReceiptWork work,
        string structure,
        string claimCeiling)
    {
        var frozenParents = Array.AsReadOnly(parentReceiptIds.ToArray());
        var frozenPrimePowers = Array.AsReadOnly(primePowers.ToArray());
        var unresolved = unresolvedCofactor.ToString(CultureInfo.InvariantCulture);
        var tested = testedThrough.ToString(CultureInfo.InvariantCulture);
        var receiptId = ComputeReceiptId(
            algorithm,
            origin,
            frozenParents,
            policy,
            canonicalInput,
            inputSha256,
            sign,
            bitLength,
            status,
            frozenPrimePowers,
            unresolved,
            tested,
            reconstructionVerified,
            magnitudeIsPrime,
            integerIsPrime,
            work,
            structure,
            claimCeiling);
        return new PrimeReceipt(
            Schema,
            ProtocolId,
            receiptId,
            algorithm,
            origin,
            frozenParents,
            policy,
            canonicalInput,
            inputSha256,
            sign,
            bitLength,
            status,
            frozenPrimePowers,
            unresolved,
            tested,
            reconstructionVerified,
            magnitudeIsPrime,
            integerIsPrime,
            work,
            structure,
            claimCeiling);
    }

    private static string ComputeReceiptId(
        string algorithm,
        PrimeReceiptOrigin origin,
        IReadOnlyList<string> parentReceiptIds,
        PrimeReceiptPolicy? policy,
        string canonicalInput,
        string inputSha256,
        int sign,
        long bitLength,
        PrimeReceiptStatus status,
        IReadOnlyList<PrimePowerReceipt> primePowers,
        string unresolved,
        string testedThrough,
        bool reconstructionVerified,
        bool? magnitudeIsPrime,
        bool? integerIsPrime,
        PrimeReceiptWork work,
        string structure,
        string claimCeiling)
    {
        var fields = new List<string>
        {
            Schema,
            ProtocolId,
            algorithm,
            origin.ToString(),
            policy?.MaxOddCandidates.ToString(CultureInfo.InvariantCulture) ?? "none",
            canonicalInput,
            inputSha256,
            sign.ToString(CultureInfo.InvariantCulture),
            bitLength.ToString(CultureInfo.InvariantCulture),
            status.ToString(),
            unresolved,
            testedThrough,
            reconstructionVerified.ToString(CultureInfo.InvariantCulture),
            magnitudeIsPrime?.ToString(CultureInfo.InvariantCulture) ?? "unknown",
            integerIsPrime?.ToString(CultureInfo.InvariantCulture) ?? "unknown",
            work.RadixExtractions.ToString(CultureInfo.InvariantCulture),
            work.OddCandidatesExamined.ToString(CultureInfo.InvariantCulture),
            work.RemainderChecks.ToString(CultureInfo.InvariantCulture),
            work.ExactFactorDivisions.ToString(CultureInfo.InvariantCulture),
            structure,
            claimCeiling,
            parentReceiptIds.Count.ToString(CultureInfo.InvariantCulture),
        };
        fields.AddRange(parentReceiptIds);
        fields.Add(primePowers.Count.ToString(CultureInfo.InvariantCulture));
        foreach (var factor in primePowers)
        {
            fields.Add(factor.PrimeDecimal);
            fields.Add(factor.Exponent.ToString(CultureInfo.InvariantCulture));
            fields.Add(factor.ProofKind.ToString());
        }

        var canonical = new StringBuilder();
        foreach (var field in fields)
        {
            canonical.Append(field.Length.ToString(CultureInfo.InvariantCulture));
            canonical.Append(':');
            canonical.Append(field);
        }

        return Sha256(canonical.ToString());
    }

    private static bool StatusInvariantsHold(
        PrimeReceipt receipt,
        BigInteger value,
        BigInteger unresolved)
    {
        var magnitude = BigInteger.Abs(value);
        var exactPrimeClaim = receipt.PrimePowers.Count == 1 && receipt.PrimePowers[0].Exponent == 1;
        var proofKindsMatchAlgorithm = receipt.Algorithm switch
        {
            TrialDivisionAlgorithm => receipt.PrimePowers.All(factor =>
                factor.ProofKind is PrimeFactorProofKind.RadixPrime or
                    PrimeFactorProofKind.OrderedTrialDivision or
                    PrimeFactorProofKind.TerminalResidualBound),
            CompositionAlgorithm => receipt.Policy is null && receipt.PrimePowers.All(factor =>
                factor.ProofKind == PrimeFactorProofKind.InheritedFromParentReceipts),
            _ => false,
        };
        var provenanceMatchesAlgorithm = receipt.Algorithm switch
        {
            TrialDivisionAlgorithm =>
                receipt.Policy is not null &&
                (receipt.Origin == PrimeReceiptOrigin.DiscoveredFromMagnitude && receipt.ParentReceiptIds.Count == 0 ||
                 receipt.Origin == PrimeReceiptOrigin.FactoredAfterMagnitudeAddition && receipt.ParentReceiptIds.Count == 2),
            CompositionAlgorithm =>
                receipt.Policy is null &&
                receipt.Origin == PrimeReceiptOrigin.ComposedFromReceipts &&
                receipt.ParentReceiptIds.Count > 0 &&
                receipt.Work == PrimeReceiptWork.Zero,
            _ => false,
        };
        var radixFactors = receipt.PrimePowers.Where(factor => factor.ProofKind == PrimeFactorProofKind.RadixPrime).ToArray();
        var terminalFactors = receipt.PrimePowers.Where(factor => factor.ProofKind == PrimeFactorProofKind.TerminalResidualBound).ToArray();
        var proofAccountingMatches = receipt.Algorithm == CompositionAlgorithm ||
            (radixFactors.Length <= 1 &&
             (radixFactors.Length == 0
                 ? receipt.Work.RadixExtractions == 0
                 : radixFactors[0].PrimeDecimal == "2" && radixFactors[0].Exponent == receipt.Work.RadixExtractions) &&
             terminalFactors.Length <= 1 &&
             (terminalFactors.Length == 0 || ReferenceEquals(terminalFactors[0], receipt.PrimePowers[^1])) &&
             receipt.PrimePowers
                 .Where(factor => factor.ProofKind == PrimeFactorProofKind.OrderedTrialDivision)
                 .Sum(factor => (long)factor.Exponent) == receipt.Work.ExactFactorDivisions);
        if (!proofKindsMatchAlgorithm || !provenanceMatchesAlgorithm || !proofAccountingMatches)
        {
            return false;
        }

        return receipt.Status switch
        {
            PrimeReceiptStatus.ExactZero =>
                magnitude.IsZero &&
                receipt.Sign == 0 &&
                receipt.PrimePowers.Count == 0 &&
                unresolved.IsZero &&
                receipt.MagnitudeIsPrime == false &&
                receipt.IntegerIsPrime == false,
            PrimeReceiptStatus.ExactUnit =>
                magnitude.IsOne &&
                receipt.PrimePowers.Count == 0 &&
                unresolved.IsOne &&
                receipt.MagnitudeIsPrime == false &&
                receipt.IntegerIsPrime == false,
            PrimeReceiptStatus.ExactFactorization =>
                magnitude > BigInteger.One &&
                receipt.PrimePowers.Count > 0 &&
                unresolved.IsOne &&
                receipt.MagnitudeIsPrime == exactPrimeClaim &&
                receipt.IntegerIsPrime == (value.Sign > 0 && exactPrimeClaim),
            PrimeReceiptStatus.PartialBudget =>
                receipt.Algorithm == TrialDivisionAlgorithm &&
                receipt.Policy is not null &&
                magnitude > BigInteger.One &&
                unresolved > BigInteger.One &&
                receipt.MagnitudeIsPrime is null &&
                receipt.IntegerIsPrime == (value.Sign < 0 ? false : null),
            _ => false,
        };
    }

    private static bool IsSha256(string? value) =>
        value is not null && value.Length == 64 && value.All(character =>
            character is >= '0' and <= '9' or >= 'A' and <= 'F');

    private static BigInteger ReconstructMagnitude(
        IReadOnlyList<PrimePowerReceipt> primePowers,
        BigInteger unresolvedCofactor)
    {
        var magnitude = unresolvedCofactor;
        foreach (var factor in primePowers)
        {
            var prime = BigInteger.Parse(factor.PrimeDecimal, CultureInfo.InvariantCulture);
            magnitude *= BigInteger.Pow(prime, factor.Exponent);
        }

        return magnitude;
    }

    private static string FormatStructure(
        int sign,
        IReadOnlyList<PrimePowerReceipt> primePowers,
        BigInteger unresolvedCofactor,
        PrimeReceiptStatus status)
    {
        if (status == PrimeReceiptStatus.ExactZero)
        {
            return "0";
        }

        if (status == PrimeReceiptStatus.ExactUnit)
        {
            return sign < 0 ? "-1" : "1";
        }

        var terms = primePowers
            .Select(factor => factor.Exponent == 1
                ? factor.PrimeDecimal
                : $"{factor.PrimeDecimal}^{factor.Exponent.ToString(CultureInfo.InvariantCulture)}")
            .ToList();
        if (status == PrimeReceiptStatus.PartialBudget)
        {
            terms.Add($"UNRESOLVED({unresolvedCofactor.ToString(CultureInfo.InvariantCulture)})");
        }

        var body = terms.Count == 0 ? "1" : string.Join(" * ", terms);
        return sign < 0 ? $"-({body})" : body;
    }

    private static string Sha256(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
