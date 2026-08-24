namespace PrimeAxiom.Core.Substrate;

public enum ContactKind
{
    NormallyOpen,
    NormallyClosed,
}
/// <summary>
/// A deliberately idealized relay contact. Mechanical delay, bounce, coil
/// current, wear, and fan-out are outside this model and must not be inferred.
/// </summary>
public readonly record struct RelayContact(ContactKind Kind)
{
    public BitState Conducts(BitState coil) => Kind switch
    {
        ContactKind.NormallyOpen => coil,
        ContactKind.NormallyClosed => coil == BitState.On ? BitState.Off : BitState.On,
        _ => throw new ArgumentOutOfRangeException(nameof(coil)),
    };
}

public static class RelayNetwork
{
    public static BitState Series(params BitState[] contacts) =>
        contacts.All(value => value == BitState.On) ? BitState.On : BitState.Off;

    public static BitState Parallel(params BitState[] contacts) =>
        contacts.Any(value => value == BitState.On) ? BitState.On : BitState.Off;
}
