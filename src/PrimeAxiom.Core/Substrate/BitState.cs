namespace PrimeAxiom.Core.Substrate;

/// <summary>
/// An abstract, readable two-state distinction. It is not a claim that every
/// physical computer must use voltage, electronics, or exactly two states.
/// </summary>
public enum BitState : byte
{
    Off = 0,
    On = 1,
}
public static class BitStateExtensions
{
    public static BitState FromBoolean(bool value) => value ? BitState.On : BitState.Off;

    public static bool ToBoolean(this BitState value) => value == BitState.On;

    public static char ToDigit(this BitState value) => value == BitState.On ? '1' : '0';
}

public readonly record struct StateTransition(BitState Before, BitState After)
{
    public bool Changed => Before != After;
}

public sealed class PrimitiveCell
{
    public PrimitiveCell(BitState initial = BitState.Off) => State = initial;

    public BitState State { get; private set; }

    public StateTransition Apply(BitState next)
    {
        var transition = new StateTransition(State, next);
        State = next;
        return transition;
    }
}
