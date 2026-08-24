using System.Numerics;
using PrimeAxiom.Core.Circuits;
using PrimeAxiom.Core.Representations;

namespace PrimeAxiom.Core.Machine;

public abstract record PrimeInstruction;

public sealed record LoadMagnitude(int Destination, BigInteger Magnitude) : PrimeInstruction;

public sealed record LoadCertifiedCoordinates(
    int Destination,
    IReadOnlyList<BigInteger> Exponents,
    bool CertificateVerified) : PrimeInstruction;

public sealed record Compose(int Destination, int Left, int Right) : PrimeInstruction;

public sealed record Cancel(int Destination, int Dividend, int Divisor) : PrimeInstruction;

public sealed record Meet(int Destination, int Left, int Right) : PrimeInstruction;

public sealed record Join(int Destination, int Left, int Right) : PrimeInstruction;

public sealed record AddWithRefactor(int Destination, int Left, int Right) : PrimeInstruction;

public sealed record ProjectExponent(int Source, int Lane) : PrimeInstruction;

public sealed record ReconstructMagnitude(int Source) : PrimeInstruction;

public sealed record PrimeProgram(IReadOnlyList<PrimeInstruction> Instructions);

public sealed record VmStepReceipt(
    int ProgramCounter,
    string Opcode,
    bool Succeeded,
    string? Failure,
    CoordinateReceipt? CoordinateReceipt,
    BigInteger? ScalarOutput);

public sealed record VmRunReceipt(
    IReadOnlyList<VmStepReceipt> Steps,
    bool Completed,
    int? FailedAt,
    BigInteger? LastScalar);

/// <summary>
/// A deliberately small interpreter whose local instructions are justified by
/// fixed-basis exponent coordinates. LOAD_MAGNITUDE and ADD_REFACTOR expose the
/// nonlocal conversion path rather than treating it as a free primitive.
/// </summary>
public sealed class PrimeMachine
{
    private readonly PrimeCoordinates?[] _registers;

    public PrimeMachine(PrimeBasis basis, int exponentWidth, int registerCount = 8)
    {
        Basis = basis ?? throw new ArgumentNullException(nameof(basis));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(exponentWidth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(registerCount);

        ExponentWidth = exponentWidth;
        _registers = new PrimeCoordinates?[registerCount];
    }

    public PrimeBasis Basis { get; }

    public int ExponentWidth { get; }

    public int RegisterCount => _registers.Length;

    public BigInteger? LastScalar { get; private set; }

    public PrimeCoordinates? ReadRegister(int index)
    {
        ValidateRegister(index);
        return _registers[index];
    }

    public VmRunReceipt Run(PrimeProgram program, bool stopOnFailure = true)
    {
        ArgumentNullException.ThrowIfNull(program);
        LastScalar = null;
        var steps = new List<VmStepReceipt>(program.Instructions.Count);
        int? failedAt = null;
        for (var pc = 0; pc < program.Instructions.Count; pc++)
        {
            var receipt = Execute(pc, program.Instructions[pc]);
            steps.Add(receipt);
            if (!receipt.Succeeded)
            {
                failedAt ??= pc;
                if (stopOnFailure)
                {
                    break;
                }
            }
        }

        return new VmRunReceipt(steps, failedAt is null, failedAt, LastScalar);
    }

    private VmStepReceipt Execute(int pc, PrimeInstruction instruction)
    {
        ArgumentNullException.ThrowIfNull(instruction);
        try
        {
            return instruction switch
            {
                LoadMagnitude load => ExecuteLoadMagnitude(pc, load),
                LoadCertifiedCoordinates load => ExecuteLoadCoordinates(pc, load),
                Compose compose => ExecuteBinary(pc, "COMPOSE", compose.Destination, compose.Left, compose.Right,
                    (left, right) => left.Compose(right)),
                Cancel cancel => ExecuteBinary(pc, "CANCEL", cancel.Destination, cancel.Dividend, cancel.Divisor,
                    (left, right) => left.Cancel(right)),
                Meet meet => ExecuteBinary(pc, "MEET", meet.Destination, meet.Left, meet.Right,
                    (left, right) => left.GreatestCommonDivisor(right)),
                Join join => ExecuteBinary(pc, "JOIN", join.Destination, join.Left, join.Right,
                    (left, right) => left.LeastCommonMultiple(right)),
                AddWithRefactor add => ExecuteBinary(pc, "ADD_REFACTOR", add.Destination, add.Left, add.Right,
                    (left, right) => left.AddViaMagnitudeAndRefactor(right)),
                ProjectExponent project => ExecuteProject(pc, project),
                ReconstructMagnitude reconstruct => ExecuteReconstruct(pc, reconstruct),
                _ => Failure(pc, instruction.GetType().Name, "Unknown instruction."),
            };
        }
        catch (ArgumentOutOfRangeException exception)
        {
            return Failure(pc, instruction.GetType().Name, exception.Message);
        }
    }

    private VmStepReceipt ExecuteLoadMagnitude(int pc, LoadMagnitude instruction)
    {
        ValidateRegister(instruction.Destination);
        var encoded = PrimeCoordinates.Encode(instruction.Magnitude, Basis, ExponentWidth);
        _registers[instruction.Destination] = encoded.Value;

        return new VmStepReceipt(
            pc,
            "LOAD_MAGNITUDE",
            encoded.Receipt.Succeeded,
            encoded.Receipt.Succeeded ? null : encoded.Receipt.Failure.ToString(),
            encoded.Receipt,
            null);
    }

    private VmStepReceipt ExecuteLoadCoordinates(int pc, LoadCertifiedCoordinates instruction)
    {
        ValidateRegister(instruction.Destination);
        _registers[instruction.Destination] = null;
        if (!instruction.CertificateVerified)
        {
            return Failure(pc, "LOAD_CERTIFIED", "The factor certificate was not verified.");
        }

        if (instruction.Exponents.Count != Basis.Count)
        {
            return Failure(pc, "LOAD_CERTIFIED", "Coordinate count does not match the configured basis.");
        }

        BinaryWord[] lanes;
        try
        {
            lanes = instruction.Exponents
                .Select(exponent => BinaryWord.FromUnsigned(exponent, ExponentWidth))
                .ToArray();
        }
        catch (Exception exception) when (exception is OverflowException or ArgumentOutOfRangeException)
        {
            return Failure(pc, "LOAD_CERTIFIED", exception.Message);
        }

        _registers[instruction.Destination] = new PrimeCoordinates(Basis, lanes);
        var receipt = new CoordinateReceipt(
            "LOAD_CERTIFIED",
            true,
            CoordinateFailure.None,
            new CoordinateCost(Substrate.GateCost.Zero, LaneWrites: Basis.Count),
            UsedMagnitudeDomain: false,
            Scope: "Caller-supplied factor certificate verified outside this VM");
        return new VmStepReceipt(pc, "LOAD_CERTIFIED", true, null, receipt, null);
    }

    private VmStepReceipt ExecuteBinary(
        int pc,
        string opcode,
        int destination,
        int leftRegister,
        int rightRegister,
        Func<PrimeCoordinates, PrimeCoordinates, CoordinateResult> operation)
    {
        ValidateRegister(destination);
        PrimeCoordinates left;
        PrimeCoordinates right;
        try
        {
            left = RequireLoaded(leftRegister);
            right = RequireLoaded(rightRegister);
        }
        catch (ArgumentOutOfRangeException)
        {
            _registers[destination] = null;
            throw;
        }

        _registers[destination] = null;
        var result = operation(left, right);
        _registers[destination] = result.Value;

        return new VmStepReceipt(
            pc,
            opcode,
            result.Receipt.Succeeded,
            result.Receipt.Succeeded ? null : result.Receipt.Failure.ToString(),
            result.Receipt,
            null);
    }

    private VmStepReceipt ExecuteProject(int pc, ProjectExponent instruction)
    {
        var value = RequireLoaded(instruction.Source);
        if (instruction.Lane < 0 || instruction.Lane >= Basis.Count)
        {
            return Failure(pc, "PROJECT", "Lane index is outside the configured basis.");
        }

        LastScalar = value.ExponentAt(instruction.Lane).ToUnsigned();
        var receipt = new CoordinateReceipt(
            "PROJECT",
            true,
            CoordinateFailure.None,
            new CoordinateCost(Substrate.GateCost.Zero, LaneReads: 1),
            UsedMagnitudeDomain: false,
            Scope: "Direct exponent-lane read; address decode is not modeled");
        return new VmStepReceipt(pc, "PROJECT", true, null, receipt, LastScalar);
    }

    private VmStepReceipt ExecuteReconstruct(int pc, ReconstructMagnitude instruction)
    {
        var value = RequireLoaded(instruction.Source);
        var result = value.Reconstruct();
        LastScalar = result.Value;
        return new VmStepReceipt(pc, "RECONSTRUCT", true, null, result.Receipt, LastScalar);
    }

    private PrimeCoordinates RequireLoaded(int register)
    {
        ValidateRegister(register);
        return _registers[register] ?? throw new ArgumentOutOfRangeException(
            nameof(register),
            $"Register r{register} is not loaded.");
    }

    private void ValidateRegister(int register)
    {
        if (register < 0 || register >= RegisterCount)
        {
            throw new ArgumentOutOfRangeException(nameof(register));
        }
    }

    private static VmStepReceipt Failure(int pc, string opcode, string message) =>
        new(pc, opcode, false, message, null, null);
}
