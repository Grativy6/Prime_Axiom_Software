using System.Numerics;
using PrimeAxiom.Core.Hybrid;
using PrimeAxiom.Core.Machine;

namespace PrimeAxiom.Tests;

public sealed class HybridMachineAndSerializationTests
{
    private static readonly int[] PrimeTwo = [2];
    private static readonly int[] PrimesTwoThree = [2, 3];

    private sealed record UnknownProducer(int DestinationIndex) : HybridInstruction
    {
        public override string Opcode => "UNKNOWN_PRODUCER";

        public override int? Destination => DestinationIndex;
    }

    public static TheoryData<string> MalformedJsonPayloads => new()
    {
        "not-json",
        "[]",
        "{}",
        """{"schema":"wrong","bank":[2],"exponentWidth":4,"sign":1,"cofactor":"1","exponents":["0"],"knowledge":["KnownExact"]}""",
        """{"schema":"prime-axiom-hybrid-integer-v1","schema":"prime-axiom-hybrid-integer-v1","bank":[2],"exponentWidth":4,"sign":1,"cofactor":"1","exponents":["0"],"knowledge":["KnownExact"]}""",
        """{"schema":"prime-axiom-hybrid-integer-v1","bank":[2],"exponentWidth":4,"sign":1,"cofactor":"1","exponents":["0"],"knowledge":["KnownExact"],"extra":0}""",
        """{"schema":"prime-axiom-hybrid-integer-v1","bank":[2,4],"exponentWidth":4,"sign":1,"cofactor":"1","exponents":["0","0"],"knowledge":["KnownExact","KnownExact"]}""",
        """{"schema":"prime-axiom-hybrid-integer-v1","bank":[2],"exponentWidth":4,"sign":1,"cofactor":"01","exponents":["0"],"knowledge":["KnownExact"]}""",
        """{"schema":"prime-axiom-hybrid-integer-v1","bank":[2],"exponentWidth":4,"sign":1,"cofactor":"1","exponents":["00"],"knowledge":["KnownExact"]}""",
        """{"schema":"prime-axiom-hybrid-integer-v1","bank":[2],"exponentWidth":4,"sign":1,"cofactor":"1","exponents":[],"knowledge":["KnownExact"]}""",
        """{"schema":"prime-axiom-hybrid-integer-v1","bank":[2],"exponentWidth":4,"sign":1,"cofactor":"1","exponents":["0"],"knowledge":["unknown"]}""",
        """{"schema":"prime-axiom-hybrid-integer-v1","bank":[2],"exponentWidth":4,"sign":1,"cofactor":"2","exponents":["0"],"knowledge":["KnownExact"]}""",
        """{"schema":"prime-axiom-hybrid-integer-v1","bank":[2],"exponentWidth":2,"sign":1,"cofactor":"1","exponents":["4"],"knowledge":["KnownExact"]}""",
        """{"schema":"prime-axiom-hybrid-integer-v1","bank":[2],"exponentWidth":4,"sign":1,"cofactor":"1","exponents":["0"],"knowledge":["KnownExact"],}""",
    };

    [Theory]
    [MemberData(nameof(MalformedJsonPayloads))]
    public void DeserializeRejectsMalformedOrNonExecutableJson(string payload)
    {
        var result = HybridInteger.Deserialize(payload);

        Assert.False(result.Receipt.Succeeded);
        Assert.Equal("DESERIALIZE", result.Receipt.Operation);
        Assert.Equal(HybridFailure.InvalidSerialization, result.Receipt.Failure);
        Assert.Null(result.Value);
    }

    [Fact]
    public void DeserializeRejectsNullAndOversizedInputsBeforeCreatingAValue()
    {
        var nullResult = HybridInteger.Deserialize(null!);
        Assert.False(nullResult.Receipt.Succeeded);
        Assert.Equal(HybridFailure.InvalidSerialization, nullResult.Receipt.Failure);
        Assert.Null(nullResult.Value);

        var oversized = HybridInteger.Deserialize(new string(' ', 1_048_577));
        Assert.False(oversized.Receipt.Succeeded);
        Assert.Equal(HybridFailure.InvalidSerialization, oversized.Receipt.Failure);
        Assert.Null(oversized.Value);
        Assert.Equal(1_048_577, oversized.Receipt.Cost.Ingress.SerializedBytes);
    }

    [Fact]
    public void SerializationIsDeterministicAndRoundTripsPartialKnowledgeExactly()
    {
        var bank = new ValuationBank(PrimesTwoThree);
        var structured = HybridInteger.FromStructured(
            sign: -1,
            cofactor: 10,
            exponents: new BigInteger[] { 1, 2 },
            bank,
            exponentWidth: 8,
            knowledge: new[] { ValuationKnowledge.CertifiedLowerBound, ValuationKnowledge.KnownExact });
        Assert.True(structured.Receipt.Succeeded, structured.Receipt.Detail);
        var value = Assert.IsType<HybridInteger>(structured.Value);

        const string expected = """{"schema":"prime-axiom-hybrid-integer-v1","bank":[2,3],"exponentWidth":8,"sign":-1,"cofactor":"10","exponents":["1","2"],"knowledge":["CertifiedLowerBound","KnownExact"]}""";
        var first = value.Serialize();
        var second = value.Serialize();

        Assert.Equal(expected, first.Data);
        Assert.Equal(first.Data, second.Data);
        Assert.Equal(first.Receipt.Cost.Egress.SerializedBytes, second.Receipt.Cost.Egress.SerializedBytes);
        Assert.DoesNotContain("provenance", first.Data, StringComparison.OrdinalIgnoreCase);

        var decoded = HybridInteger.Deserialize(first.Data);
        Assert.True(decoded.Receipt.Succeeded, decoded.Receipt.Detail);
        var roundTrip = Assert.IsType<HybridInteger>(decoded.Value);
        Assert.Equal(value, roundTrip);
        Assert.Equal(new BigInteger(-180), roundTrip.Reconstruct().Value);
        Assert.Equal(HybridValidity.Partial, roundTrip.Validity);
        Assert.Equal(first.Data, roundTrip.Serialize().Data);
    }

    [Fact]
    public void ClaimedMagnitudeIngressRequiresExactSignedEquality()
    {
        var bank = new ValuationBank(PrimesTwoThree);
        var accepted = HybridInteger.FromClaimedMagnitude(
            claimedMagnitude: -72,
            sign: -1,
            cofactor: 1,
            exponents: new BigInteger[] { 3, 2 },
            bank,
            exponentWidth: 8);

        Assert.True(accepted.Receipt.Succeeded, accepted.Receipt.Detail);
        Assert.Equal(HybridFailure.None, accepted.Receipt.Failure);
        Assert.Equal(new BigInteger(-72), accepted.Value!.Reconstruct().Value);
        Assert.True(accepted.Receipt.Cost.Ingress.ReconstructionMultiplications > 0);

        var rejected = HybridInteger.FromClaimedMagnitude(
            claimedMagnitude: 72,
            sign: -1,
            cofactor: 1,
            exponents: new BigInteger[] { 3, 2 },
            bank,
            exponentWidth: 8);

        Assert.False(rejected.Receipt.Succeeded);
        Assert.Equal("CLAIMED_MAGNITUDE_INGRESS", rejected.Receipt.Operation);
        Assert.Equal(HybridFailure.ClaimedMagnitudeMismatch, rejected.Receipt.Failure);
        Assert.Null(rejected.Value);
    }

    [Fact]
    public void FailedProducerInvalidatesItsOldDestinationButPreservesSources()
    {
        var machine = new HybridMachine(new ValuationBank(PrimeTwo), exponentWidth: 2);
        var run = machine.Run(new HybridProgram(new HybridInstruction[]
        {
            new HybridLoadBinary(0, 8),
            new HybridLoadBinary(1, 2),
            new HybridLoadBinary(2, 3),
            new HybridMultiply(2, 0, 1),
        }));

        Assert.False(run.Completed);
        Assert.Equal(4, run.InstructionsExecuted);
        Assert.Equal(4, run.Trace.Count);
        Assert.Equal(HybridFailure.ExponentOverflow, run.Trace[3].Receipt.Failure);
        Assert.Equal(2, run.Trace[3].Destination);
        Assert.False(run.Trace[3].DestinationValidAfter);
        Assert.Contains("invalidated", run.FailurePolicy, StringComparison.OrdinalIgnoreCase);

        Assert.True(machine.IsRegisterValid(0));
        Assert.True(machine.IsRegisterValid(1));
        Assert.Equal(new BigInteger(8), machine.ReadRegister(0)!.Reconstruct().Value);
        Assert.Equal(new BigInteger(2), machine.ReadRegister(1)!.Reconstruct().Value);
        Assert.False(machine.IsRegisterValid(2));
        Assert.Null(machine.ReadRegister(2));

        var recovery = machine.Run(new HybridProgram(new HybridInstruction[]
        {
            new HybridLoadBinary(2, 4),
        }));
        Assert.True(recovery.Completed);
        Assert.True(machine.IsRegisterValid(2));
        Assert.Equal(new BigInteger(4), machine.ReadRegister(2)!.Reconstruct().Value);
    }

    [Fact]
    public void FailedClaimedLoadInvalidatesAPreviouslyValidDestination()
    {
        var machine = new HybridMachine(new ValuationBank(PrimesTwoThree), exponentWidth: 8);
        var run = machine.Run(new HybridProgram(new HybridInstruction[]
        {
            new HybridLoadBinary(0, 12),
            new HybridLoadClaimedMagnitude(
                DestinationIndex: 0,
                ClaimedMagnitude: 13,
                Sign: 1,
                Cofactor: 1,
                Exponents: new BigInteger[] { 2, 1 }),
        }));

        Assert.False(run.Completed);
        Assert.Equal(HybridFailure.ClaimedMagnitudeMismatch, run.Trace[1].Receipt.Failure);
        Assert.False(run.Trace[1].DestinationValidAfter);
        Assert.False(machine.IsRegisterValid(0));
        Assert.Null(machine.ReadRegister(0));
    }

    [Fact]
    public void UnknownProducerIsAnInvalidInstructionAndInvalidatesItsDestination()
    {
        var machine = new HybridMachine(new ValuationBank(PrimeTwo), exponentWidth: 8);
        var run = machine.Run(new HybridProgram(new HybridInstruction[]
        {
            new HybridLoadBinary(7, 123),
            new UnknownProducer(7),
        }));

        Assert.False(run.Completed);
        Assert.Equal(HybridFailure.InvalidInstruction, run.Trace[1].Receipt.Failure);
        Assert.Equal("UNKNOWN_PRODUCER", run.Trace[1].Receipt.Operation);
        Assert.Equal(7, run.Trace[1].Destination);
        Assert.False(run.Trace[1].DestinationValidAfter);
        Assert.False(machine.IsRegisterValid(7));
        Assert.Null(machine.ReadRegister(7));
    }

    [Fact]
    public void AliasedFailedProducerInvalidatesTheDestinationSourceAndPreservesOtherSources()
    {
        var machine = new HybridMachine(new ValuationBank(PrimeTwo), exponentWidth: 2);
        var run = machine.Run(new HybridProgram(new HybridInstruction[]
        {
            new HybridLoadBinary(0, 8),
            new HybridLoadBinary(1, 2),
            new HybridMultiply(0, 0, 1),
        }));

        Assert.False(run.Completed);
        Assert.Equal(HybridFailure.ExponentOverflow, run.Trace[2].Receipt.Failure);
        Assert.Equal(0, run.Trace[2].Destination);
        Assert.False(run.Trace[2].DestinationValidAfter);
        Assert.False(machine.IsRegisterValid(0));
        Assert.Null(machine.ReadRegister(0));
        Assert.True(machine.IsRegisterValid(1));
        Assert.Equal(new BigInteger(2), machine.ReadRegister(1)!.Reconstruct().Value);
    }
}
