using System.Numerics;
using PrimeAxiom.Core.Hybrid;

namespace PrimeAxiom.Core.Machine;

public abstract record HybridInstruction
{
    public abstract string Opcode { get; }

    public virtual int? Destination => null;
}

public sealed record HybridLoadBinary(int DestinationIndex, BigInteger Magnitude) : HybridInstruction
{
    public override string Opcode => "LOAD_BINARY";

    public override int? Destination => DestinationIndex;
}

public sealed record HybridLoadComponents(
    int DestinationIndex,
    int Sign,
    BigInteger Cofactor,
    IReadOnlyList<BigInteger> Exponents,
    IReadOnlyList<ValuationKnowledge>? Knowledge = null) : HybridInstruction
{
    public override string Opcode => "LOAD_COMPONENTS";

    public override int? Destination => DestinationIndex;
}

public sealed record HybridLoadClaimedMagnitude(
    int DestinationIndex,
    BigInteger ClaimedMagnitude,
    int Sign,
    BigInteger Cofactor,
    IReadOnlyList<BigInteger> Exponents,
    IReadOnlyList<ValuationKnowledge>? Knowledge = null) : HybridInstruction
{
    public override string Opcode => "LOAD_CLAIMED_MAGNITUDE";

    public override int? Destination => DestinationIndex;
}

public sealed record HybridMultiply(int DestinationIndex, int Left, int Right) : HybridInstruction
{
    public override string Opcode => "COMPOSE";

    public override int? Destination => DestinationIndex;
}

public sealed record HybridAddPreserve(int DestinationIndex, int Left, int Right) : HybridInstruction
{
    public override string Opcode => "ADD_PRESERVE";

    public override int? Destination => DestinationIndex;
}

public sealed record HybridExactDivide(int DestinationIndex, int Dividend, int Divisor) : HybridInstruction
{
    public override string Opcode => "EXACT_DIVIDE";

    public override int? Destination => DestinationIndex;
}

public sealed record HybridPower(int DestinationIndex, int Source, int Exponent) : HybridInstruction
{
    public override string Opcode => "POWER";

    public override int? Destination => DestinationIndex;
}

public sealed record HybridRefreshLane(int DestinationIndex, int Source, int Lane) : HybridInstruction
{
    public override string Opcode => "REFRESH_LANE";

    public override int? Destination => DestinationIndex;
}

public sealed record HybridNormalize(int DestinationIndex, int Source) : HybridInstruction
{
    public override string Opcode => "NORMALIZE";

    public override int? Destination => DestinationIndex;
}

public sealed record HybridMigrateBank(
    int DestinationIndex,
    int Source,
    ValuationBank TargetBank,
    int? TargetExponentWidth = null) : HybridInstruction
{
    public override string Opcode => "MIGRATE_BANK";

    public override int? Destination => DestinationIndex;
}

public sealed record HybridReconstruct(int Source) : HybridInstruction
{
    public override string Opcode => "RECONSTRUCT";
}

public sealed record HybridReadValuation(int Source, int Lane) : HybridInstruction
{
    public override string Opcode => "VALUATION";
}

public sealed record HybridProgram(IReadOnlyList<HybridInstruction> Instructions);

public sealed record HybridTraceEntry(
    int InstructionIndex,
    string Opcode,
    int? Destination,
    bool DestinationValidAfter,
    HybridReceipt Receipt);

public sealed record HybridMachineRunReceipt(
    bool Completed,
    int InstructionsExecuted,
    HybridCostLedger TotalCost,
    IReadOnlyList<HybridTraceEntry> Trace,
    BigInteger? LastScalar,
    ValuationAnswer? LastValuation,
    string FailurePolicy);

/// <summary>
/// A small machine for the hybrid experiment. Register writes are atomic. A
/// failed producer invalidates its destination so an older value cannot be
/// consumed as if the failed instruction had succeeded.
/// </summary>
public sealed class HybridMachine
{
    private readonly Dictionary<int, HybridInteger> _registers = new();
    private readonly HashSet<int> _invalidRegisters = new();

    public HybridMachine(ValuationBank bank, int exponentWidth)
    {
        ArgumentNullException.ThrowIfNull(bank);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(exponentWidth);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(exponentWidth, HybridInteger.MaximumExponentWidth);
        Bank = bank;
        ExponentWidth = exponentWidth;
    }

    public ValuationBank Bank { get; }

    public int ExponentWidth { get; }

    public bool IsRegisterValid(int register) => _registers.ContainsKey(register) && !_invalidRegisters.Contains(register);

    public HybridInteger? ReadRegister(int register) => IsRegisterValid(register) ? _registers[register] : null;

    public HybridMachineRunReceipt Run(HybridProgram program)
    {
        ArgumentNullException.ThrowIfNull(program);
        if (program.Instructions is null)
        {
            var receipt = InvalidInstructionReceipt("PROGRAM", "Instruction list is null.");
            return new HybridMachineRunReceipt(
                false,
                0,
                HybridCostLedger.Zero,
                new[] { new HybridTraceEntry(0, "PROGRAM", null, false, receipt) },
                null,
                null,
                "HALT_ON_FAILURE; malformed programs do not enter execution");
        }

        var trace = new List<HybridTraceEntry>(program.Instructions.Count);
        var total = HybridCostLedger.Zero;
        BigInteger? lastScalar = null;
        ValuationAnswer? lastValuation = null;

        for (var index = 0; index < program.Instructions.Count; index++)
        {
            var instruction = program.Instructions[index];
            if (instruction is null)
            {
                var nullReceipt = InvalidInstructionReceipt("<NULL>", "Program contains a null instruction.");
                trace.Add(new HybridTraceEntry(index, "<NULL>", null, false, nullReceipt));
                return new HybridMachineRunReceipt(
                    false,
                    index + 1,
                    total,
                    trace,
                    lastScalar,
                    lastValuation,
                    "HALT_ON_FAILURE; failed destination is invalidated atomically; source registers are unchanged");
            }

            var outcome = Execute(instruction, ref lastScalar, ref lastValuation);
            total += outcome.Receipt.Cost;
            var destinationValid = instruction.Destination is null || IsRegisterValid(instruction.Destination.Value);
            trace.Add(new HybridTraceEntry(
                index,
                instruction.Opcode,
                instruction.Destination,
                destinationValid,
                outcome.Receipt));
            if (!outcome.Receipt.Succeeded)
            {
                return new HybridMachineRunReceipt(
                    false,
                    index + 1,
                    total,
                    trace,
                    lastScalar,
                    lastValuation,
                    "HALT_ON_FAILURE; failed destination is invalidated atomically; source registers are unchanged");
            }
        }

        return new HybridMachineRunReceipt(
            true,
            program.Instructions.Count,
            total,
            trace,
            lastScalar,
            lastValuation,
            "HALT_ON_FAILURE; failed destination is invalidated atomically; source registers are unchanged");
    }

    private MachineOutcome Execute(
        HybridInstruction instruction,
        ref BigInteger? lastScalar,
        ref ValuationAnswer? lastValuation)
    {
        try
        {
            return ExecuteChecked(instruction, ref lastScalar, ref lastValuation);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or OverflowException)
        {
            if (instruction.Destination is int destination)
            {
                Invalidate(destination);
            }

            return new MachineOutcome(InvalidInstructionReceipt(instruction.Opcode, exception.Message));
        }
    }

    private MachineOutcome ExecuteChecked(
        HybridInstruction instruction,
        ref BigInteger? lastScalar,
        ref ValuationAnswer? lastValuation)
    {
        switch (instruction)
        {
            case HybridLoadBinary load:
                return Commit(load.DestinationIndex, HybridInteger.FromBinary(load.Magnitude, Bank, ExponentWidth));

            case HybridLoadComponents load:
                return Commit(
                    load.DestinationIndex,
                    HybridInteger.FromStructured(
                        load.Sign,
                        load.Cofactor,
                        load.Exponents,
                        Bank,
                        ExponentWidth,
                        load.Knowledge));

            case HybridLoadClaimedMagnitude load:
                return Commit(
                    load.DestinationIndex,
                    HybridInteger.FromClaimedMagnitude(
                        load.ClaimedMagnitude,
                        load.Sign,
                        load.Cofactor,
                        load.Exponents,
                        Bank,
                        ExponentWidth,
                        load.Knowledge));

            case HybridMultiply compose:
                return ExecuteBinary(
                    compose.DestinationIndex,
                    compose.Left,
                    compose.Right,
                    "COMPOSE",
                    (left, right) => left.Multiply(right));

            case HybridAddPreserve add:
                return ExecuteBinary(
                    add.DestinationIndex,
                    add.Left,
                    add.Right,
                    "ADD_PRESERVE",
                    (left, right) => left.AddPreservingValuations(right));

            case HybridExactDivide divide:
                return ExecuteBinary(
                    divide.DestinationIndex,
                    divide.Dividend,
                    divide.Divisor,
                    "EXACT_DIVIDE",
                    (left, right) => left.ExactDivide(right));

            case HybridPower power:
                return ExecuteUnary(power.DestinationIndex, power.Source, "POWER", value => value.Power(power.Exponent));

            case HybridRefreshLane refresh:
                return ExecuteUnary(
                    refresh.DestinationIndex,
                    refresh.Source,
                    "REFRESH_LANE",
                    value => value.RefreshLane(refresh.Lane));

            case HybridNormalize normalize:
                return ExecuteUnary(normalize.DestinationIndex, normalize.Source, "NORMALIZE", value => value.Normalize());

            case HybridMigrateBank migrate:
                return ExecuteUnary(
                    migrate.DestinationIndex,
                    migrate.Source,
                    "MIGRATE_BANK",
                    value => value.MigrateBank(migrate.TargetBank, migrate.TargetExponentWidth));

            case HybridReconstruct reconstruct:
                lastScalar = null;
                if (!TryRead(reconstruct.Source, out var source, out var receipt, "RECONSTRUCT"))
                {
                    return new MachineOutcome(receipt!);
                }

                var magnitude = source!.Reconstruct();
                lastScalar = magnitude.Value;
                return new MachineOutcome(magnitude.Receipt);

            case HybridReadValuation valuation:
                lastValuation = null;
                if (!TryRead(valuation.Source, out var valuationSource, out var valuationFailure, "VALUATION"))
                {
                    return new MachineOutcome(valuationFailure!);
                }

                var answer = valuationSource!.Valuation(valuation.Lane);
                lastValuation = answer.Value;
                return new MachineOutcome(answer.Receipt);

            default:
                if (instruction.Destination is int destination)
                {
                    Invalidate(destination);
                }

                return new MachineOutcome(InvalidInstructionReceipt(instruction.Opcode, "Instruction type is not recognized."));
        }
    }

    private MachineOutcome ExecuteUnary(
        int destination,
        int source,
        string operation,
        Func<HybridInteger, HybridResult<HybridInteger>> execute)
    {
        if (!TryRead(source, out var value, out var failure, operation))
        {
            Invalidate(destination);
            return new MachineOutcome(failure!);
        }

        return Commit(destination, execute(value!));
    }

    private MachineOutcome ExecuteBinary(
        int destination,
        int leftRegister,
        int rightRegister,
        string operation,
        Func<HybridInteger, HybridInteger, HybridResult<HybridInteger>> execute)
    {
        if (!TryRead(leftRegister, out var left, out var leftFailure, operation))
        {
            Invalidate(destination);
            return new MachineOutcome(leftFailure!);
        }

        if (!TryRead(rightRegister, out var right, out var rightFailure, operation))
        {
            Invalidate(destination);
            return new MachineOutcome(rightFailure!);
        }

        return Commit(destination, execute(left!, right!));
    }

    private MachineOutcome Commit(int destination, HybridResult<HybridInteger> result)
    {
        if (!result.Receipt.Succeeded || result.Value is null)
        {
            Invalidate(destination);
            return new MachineOutcome(result.Receipt);
        }

        _registers[destination] = result.Value;
        _invalidRegisters.Remove(destination);
        return new MachineOutcome(result.Receipt);
    }

    private bool TryRead(int register, out HybridInteger? value, out HybridReceipt? failure, string operation)
    {
        if (!IsRegisterValid(register))
        {
            value = null;
            failure = InvalidSourceReceipt(
                operation,
                $"Source register {register} is uninitialized or was invalidated by a failed producer.");
            return false;
        }

        value = _registers[register];
        failure = null;
        return true;
    }

    private void Invalidate(int register)
    {
        _registers.Remove(register);
        _invalidRegisters.Add(register);
    }

    private static HybridReceipt InvalidSourceReceipt(string operation, string detail) =>
        new(
            operation,
            false,
            HybridFailure.InvalidRegister,
            HybridDomain.None,
            HybridCostLedger.Zero,
            null,
            null,
            "No instruction work performed because a source register was invalid",
            detail);

    private static HybridReceipt InvalidInstructionReceipt(string operation, string detail) =>
        new(
            operation,
            false,
            HybridFailure.InvalidInstruction,
            HybridDomain.None,
            HybridCostLedger.Zero,
            null,
            null,
            "Malformed instruction was rejected without producing a value",
            detail);

    private sealed record MachineOutcome(HybridReceipt Receipt);
}
