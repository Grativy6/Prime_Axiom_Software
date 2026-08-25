using System.Reflection;

namespace PrimeAxiom.Tests;

public sealed class Build002ProtocolTests
{
    [Fact]
    public void FrozenSeedAndSplitMixVectorAreStable()
    {
        var assembly = Assembly.Load("PrimeAxiom.Cli");
        var protocol = assembly.GetType("PrimeAxiom.Cli.Build002Protocol", throwOnError: true)!;
        var deriveSeed = protocol.GetMethod("DeriveSeed", BindingFlags.Public | BindingFlags.Static)!;
        var seed = (ulong)deriveSeed.Invoke(null, [4, "B", 0])!;

        Assert.Equal(0xCE7A_6855_4D84_CE19UL, seed);

        var generatorType = assembly.GetType("PrimeAxiom.Cli.SplitMix64", throwOnError: true)!;
        var generator = Activator.CreateInstance(
            generatorType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: [seed],
            culture: null)!;
        var next = generatorType.GetMethod("NextUInt64", BindingFlags.Instance | BindingFlags.Public)!;
        Assert.Equal(0x56BE_7269_4C75_63F6UL, (ulong)next.Invoke(generator, null)!);
    }
}
