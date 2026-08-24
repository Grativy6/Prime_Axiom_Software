using PrimeAxiom.Core.Substrate;

namespace PrimeAxiom.Core.Circuits;

public enum LatchStatus
{
    Stable,
    ForbiddenInput,
    DidNotSettle,
}

public readonly record struct LatchResult(
    BitState Q,
    BitState QBar,
    LatchStatus Status,
    int PropagationRounds,
    GateCost Cost);

/// <summary>
/// Unit-delay, synchronous-round simulation of a cross-coupled NAND SR latch.
/// It exposes the forbidden input instead of inventing a stable physical result.
/// </summary>
public sealed class SrNandLatch
{
    public SrNandLatch(BitState initial = BitState.Off)
    {
        Q = initial;
        QBar = initial == BitState.On ? BitState.Off : BitState.On;
    }

    public BitState Q { get; private set; }

    public BitState QBar { get; private set; }

    public LatchResult Apply(BitState setBar, BitState resetBar, int maximumRounds = 16)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumRounds);

        if (setBar == BitState.Off && resetBar == BitState.Off)
        {
            // Both outputs are driven high in the idealized NAND model. Release
            // from this condition is not resolved by the abstraction.
            Q = BitState.On;
            QBar = BitState.On;
            return new LatchResult(Q, QBar, LatchStatus.ForbiddenInput, 1, new GateCost(2, 1));
        }

        long evaluations = 0;
        for (var round = 1; round <= maximumRounds; round++)
        {
            var nextQ = BitStateExtensions.FromBoolean(
                !(setBar.ToBoolean() && QBar.ToBoolean()));
            var nextQBar = BitStateExtensions.FromBoolean(
                !(resetBar.ToBoolean() && Q.ToBoolean()));
            evaluations += 2;

            if (nextQ == Q && nextQBar == QBar && nextQ != nextQBar)
            {
                return new LatchResult(Q, QBar, LatchStatus.Stable, round, new GateCost(evaluations, round));
            }

            Q = nextQ;
            QBar = nextQBar;
        }

        return new LatchResult(
            Q,
            QBar,
            LatchStatus.DidNotSettle,
            maximumRounds,
            new GateCost(evaluations, maximumRounds));
    }
}

public sealed class GatedDLatch
{
    private readonly SrNandLatch _storage;

    public GatedDLatch(BitState initial = BitState.Off) => _storage = new SrNandLatch(initial);

    public BitState Q => _storage.Q;

    public LatchResult Apply(BitState data, BitState enable)
    {
        var network = new GateNetwork();
        var d = network.Input(data);
        var en = network.Input(enable);
        var setBar = network.Nand(d, en);
        var resetBar = network.Nand(network.Not(d), en);
        var stored = _storage.Apply(setBar.State, resetBar.State);
        return stored with
        {
            Cost = new GateCost(
                network.Cost.NandEvaluations + stored.Cost.NandEvaluations,
                network.Cost.CriticalPathDepth + stored.Cost.CriticalPathDepth),
        };
    }
}

public sealed class BinaryRegister
{
    private readonly GatedDLatch[] _cells;

    public BinaryRegister(int width) =>
        _cells = Enumerable.Range(0, width > 0 ? width : throw new ArgumentOutOfRangeException(nameof(width)))
            .Select(_ => new GatedDLatch())
            .ToArray();

    public int Width => _cells.Length;

    public BinaryWord Read() => new(_cells.Select(cell => cell.Q));

    public GateCost Write(BinaryWord value, BitState enable = BitState.On)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.Width != Width)
        {
            throw new ArgumentException("Register and input widths must match.", nameof(value));
        }

        return GateCost.Parallel(_cells.Select((cell, index) => cell.Apply(value[index], enable).Cost));
    }
}

public readonly record struct CounterTick(BinaryWord Before, BinaryWord After, BitState Overflow, GateCost Cost);

public sealed class BinaryCounter
{
    private readonly BinaryRegister _register;

    public BinaryCounter(int width) => _register = new BinaryRegister(width);

    public BinaryWord Read() => _register.Read();

    public CounterTick Tick()
    {
        var before = Read();
        var addition = BinaryCircuit.Add(before, BinaryWord.One(before.Width));
        var writeCost = _register.Write(addition.Value);
        return new CounterTick(
            before,
            Read(),
            addition.Carry,
            GateCost.Sequential(addition.Cost, writeCost));
    }
}
