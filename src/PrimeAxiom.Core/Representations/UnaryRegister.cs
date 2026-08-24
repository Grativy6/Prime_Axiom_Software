using PrimeAxiom.Core.Substrate;

namespace PrimeAxiom.Core.Representations;

/// <summary>
/// Quantity as occupied marks. Position exists, but positional place value does
/// not: the represented quantity is the count of On cells.
/// </summary>
public sealed class UnaryRegister
{
    private readonly PrimitiveCell[] _cells;

    public UnaryRegister(int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);

        _cells = Enumerable.Range(0, capacity).Select(_ => new PrimitiveCell()).ToArray();
    }

    public int Capacity => _cells.Length;

    public int Count => _cells.Count(cell => cell.State == BitState.On);

    public IReadOnlyList<BitState> Marks => _cells.Select(cell => cell.State).ToArray();

    public StateTransition Increment()
    {
        var next = Array.FindIndex(_cells, cell => cell.State == BitState.Off);
        if (next < 0)
        {
            throw new OverflowException("The unary register is full.");
        }

        return _cells[next].Apply(BitState.On);
    }

    public StateTransition Decrement()
    {
        var previous = Array.FindLastIndex(_cells, cell => cell.State == BitState.On);
        if (previous < 0)
        {
            throw new InvalidOperationException("The unary register is empty.");
        }

        return _cells[previous].Apply(BitState.Off);
    }
}
