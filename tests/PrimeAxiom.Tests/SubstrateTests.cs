using PrimeAxiom.Core.Substrate;

namespace PrimeAxiom.Tests;

public sealed class SubstrateTests
{
    [Fact]
    public void BitStateConversionsAndPrimitiveCellTransitionsAreExact()
    {
        Assert.Equal(BitState.Off, BitStateExtensions.FromBoolean(false));
        Assert.Equal(BitState.On, BitStateExtensions.FromBoolean(true));
        Assert.False(BitState.Off.ToBoolean());
        Assert.True(BitState.On.ToBoolean());
        Assert.Equal('0', BitState.Off.ToDigit());
        Assert.Equal('1', BitState.On.ToDigit());

        var cell = new PrimitiveCell();
        Assert.Equal(BitState.Off, cell.State);

        var rising = cell.Apply(BitState.On);
        Assert.Equal(new StateTransition(BitState.Off, BitState.On), rising);
        Assert.True(rising.Changed);
        Assert.Equal(BitState.On, cell.State);

        var held = cell.Apply(BitState.On);
        Assert.Equal(new StateTransition(BitState.On, BitState.On), held);
        Assert.False(held.Changed);

        var falling = cell.Apply(BitState.Off);
        Assert.Equal(new StateTransition(BitState.On, BitState.Off), falling);
        Assert.True(falling.Changed);
        Assert.Equal(BitState.Off, cell.State);
    }

    [Theory]
    [InlineData(BitState.Off, BitState.Off, BitState.On)]
    [InlineData(BitState.Off, BitState.On, BitState.On)]
    [InlineData(BitState.On, BitState.Off, BitState.On)]
    [InlineData(BitState.On, BitState.On, BitState.Off)]
    public void NandGateHasTheCompleteTruthTable(
        BitState left,
        BitState right,
        BitState expected)
    {
        var network = new GateNetwork();

        var output = network.Nand(network.Input(left), network.Input(right));

        Assert.Equal(expected, output.State);
        Assert.Equal(1, output.Depth);
        Assert.Equal(new GateCost(1, 1), network.Cost);
    }

    [Fact]
    public void DerivedGatesHaveCompleteTruthTablesAndDeclaredNandCosts()
    {
        foreach (var left in AllStates)
        {
            var not = Evaluate(network => network.Not(network.Input(left)));
            Assert.Equal(State(!left.ToBoolean()), not.Output.State);
            Assert.Equal(new GateCost(1, 1), not.Cost);

            foreach (var right in AllStates)
            {
                var and = Evaluate(network => network.And(network.Input(left), network.Input(right)));
                Assert.Equal(State(left.ToBoolean() && right.ToBoolean()), and.Output.State);
                Assert.Equal(new GateCost(2, 2), and.Cost);

                var or = Evaluate(network => network.Or(network.Input(left), network.Input(right)));
                Assert.Equal(State(left.ToBoolean() || right.ToBoolean()), or.Output.State);
                Assert.Equal(new GateCost(3, 2), or.Cost);

                var xor = Evaluate(network => network.Xor(network.Input(left), network.Input(right)));
                Assert.Equal(State(left.ToBoolean() ^ right.ToBoolean()), xor.Output.State);
                Assert.Equal(new GateCost(4, 3), xor.Cost);

                var xnor = Evaluate(network => network.Xnor(network.Input(left), network.Input(right)));
                Assert.Equal(State(left == right), xnor.Output.State);
                Assert.Equal(new GateCost(5, 4), xnor.Cost);

                foreach (var select in AllStates)
                {
                    var mux = Evaluate(network => network.Mux(
                        network.Input(select),
                        network.Input(left),
                        network.Input(right)));
                    Assert.Equal(select == BitState.On ? left : right, mux.Output.State);
                    Assert.Equal(new GateCost(4, 3), mux.Cost);
                }
            }
        }
    }

    [Fact]
    public void GateDepthIncludesInputArrivalAndCostCombinatorsPreserveMeaning()
    {
        var network = new GateNetwork();
        var output = network.Nand(
            network.Input(BitState.On, arrivalDepth: 4),
            network.Input(BitState.On, arrivalDepth: 7));

        Assert.Equal(BitState.Off, output.State);
        Assert.Equal(8, output.Depth);
        Assert.Equal(new GateCost(1, 8), network.Cost);

        Assert.Equal(
            new GateCost(7, 8),
            GateCost.Sequential(new GateCost(2, 3), new GateCost(5, 5)));
        Assert.Equal(
            new GateCost(10, 7),
            GateCost.Parallel(new[]
            {
                new GateCost(2, 3),
                new GateCost(5, 7),
                new GateCost(3, 2),
            }));
        Assert.Equal(GateCost.Zero, GateCost.Parallel(Array.Empty<GateCost>()));
    }

    [Fact]
    public void RelayContactsAndNetworksHaveCompleteIdealizedTruthTables()
    {
        var normallyOpen = new RelayContact(ContactKind.NormallyOpen);
        var normallyClosed = new RelayContact(ContactKind.NormallyClosed);

        Assert.Equal(BitState.Off, normallyOpen.Conducts(BitState.Off));
        Assert.Equal(BitState.On, normallyOpen.Conducts(BitState.On));
        Assert.Equal(BitState.On, normallyClosed.Conducts(BitState.Off));
        Assert.Equal(BitState.Off, normallyClosed.Conducts(BitState.On));

        foreach (var left in AllStates)
        {
            foreach (var right in AllStates)
            {
                Assert.Equal(
                    State(left == BitState.On && right == BitState.On),
                    RelayNetwork.Series(left, right));
                Assert.Equal(
                    State(left == BitState.On || right == BitState.On),
                    RelayNetwork.Parallel(left, right));
            }
        }

        Assert.Equal(BitState.On, RelayNetwork.Series());
        Assert.Equal(BitState.Off, RelayNetwork.Parallel());
    }

    private static IReadOnlyList<BitState> AllStates { get; } =
        new[] { BitState.Off, BitState.On };

    private static BitState State(bool value) => BitStateExtensions.FromBoolean(value);

    private static (Signal Output, GateCost Cost) Evaluate(Func<GateNetwork, Signal> operation)
    {
        var network = new GateNetwork();
        var output = operation(network);
        return (output, network.Cost);
    }
}
