using PrimeAxiom.Core.Substrate;

namespace PrimeAxiom.Core.Circuits;

public readonly record struct AddResult(BinaryWord Value, BitState Carry, GateCost Cost);

public readonly record struct SubtractResult(BinaryWord Value, BitState Borrow, GateCost Cost);

public readonly record struct CompareResult(BitState Less, BitState Equal, BitState Greater, GateCost Cost);

public readonly record struct WordResult(BinaryWord Value, GateCost Cost);

public static class BinaryCircuit
{
    public static (BitState Sum, BitState Carry, GateCost Cost) HalfAdd(BitState left, BitState right)
    {
        var network = new GateNetwork();
        var a = network.Input(left);
        var b = network.Input(right);
        var sum = network.Xor(a, b);
        var carry = network.And(a, b);
        return (sum.State, carry.State, network.Cost);
    }

    public static (BitState Sum, BitState Carry, GateCost Cost) FullAdd(
        BitState left,
        BitState right,
        BitState carryIn)
    {
        var network = new GateNetwork();
        var a = network.Input(left);
        var b = network.Input(right);
        var carry = network.Input(carryIn);
        var leftXorRight = network.Xor(a, b);
        var sum = network.Xor(leftXorRight, carry);
        var carryFromPair = network.And(a, b);
        var carryFromInput = network.And(leftXorRight, carry);
        var carryOut = network.Or(carryFromPair, carryFromInput);
        return (sum.State, carryOut.State, network.Cost);
    }

    public static AddResult Add(BinaryWord left, BinaryWord right, BitState carryIn = BitState.Off)
    {
        EnsureSameWidth(left, right);
        var network = new GateNetwork();
        var leftSignals = Enumerable.Range(0, left.Width)
            .Select(index => network.Input(left[index]))
            .ToArray();
        var rightSignals = Enumerable.Range(0, right.Width)
            .Select(index => network.Input(right[index]))
            .ToArray();
        var (sum, carry) = AddSignals(network, leftSignals, rightSignals, network.Input(carryIn));

        return new AddResult(new BinaryWord(sum.Select(signal => signal.State)), carry.State, network.Cost);
    }

    public static SubtractResult Subtract(BinaryWord left, BinaryWord right)
    {
        EnsureSameWidth(left, right);
        var network = new GateNetwork();
        var bits = new BitState[left.Width];
        var borrow = network.Input(BitState.Off);

        for (var index = 0; index < left.Width; index++)
        {
            var a = network.Input(left[index]);
            var b = network.Input(right[index]);
            var aXorB = network.Xor(a, b);
            bits[index] = network.Xor(aXorB, borrow).State;

            // borrow' = (!a & (b | borrow)) | (b & borrow)
            var notA = network.Not(a);
            var bOrBorrow = network.Or(b, borrow);
            var first = network.And(notA, bOrBorrow);
            var second = network.And(b, borrow);
            borrow = network.Or(first, second);
        }

        return new SubtractResult(new BinaryWord(bits), borrow.State, network.Cost);
    }

    public static CompareResult Compare(BinaryWord left, BinaryWord right)
    {
        EnsureSameWidth(left, right);
        var network = new GateNetwork();
        var equal = network.Input(BitState.On);
        var greater = network.Input(BitState.Off);
        var less = network.Input(BitState.Off);

        for (var index = left.Width - 1; index >= 0; index--)
        {
            var a = network.Input(left[index]);
            var b = network.Input(right[index]);
            var notA = network.Not(a);
            var notB = network.Not(b);
            var aGreaterB = network.And(a, notB);
            var aLessB = network.And(notA, b);
            greater = network.Or(greater, network.And(equal, aGreaterB));
            less = network.Or(less, network.And(equal, aLessB));
            equal = network.And(equal, network.Xnor(a, b));
        }

        return new CompareResult(less.State, equal.State, greater.State, network.Cost);
    }

    public static WordResult Min(BinaryWord left, BinaryWord right) => SelectByOrder(left, right, minimum: true);

    public static WordResult Max(BinaryWord left, BinaryWord right) => SelectByOrder(left, right, minimum: false);

    public static WordResult Multiply(BinaryWord left, BinaryWord right)
    {
        EnsureSameWidth(left, right);
        var outputWidth = checked(left.Width * 2);
        var network = new GateNetwork();
        var zero = network.Input(BitState.Off);
        var multiplicand = Enumerable.Range(0, left.Width)
            .Select(index => network.Input(left[index]))
            .ToArray();
        var multiplier = Enumerable.Range(0, right.Width)
            .Select(index => network.Input(right[index]))
            .ToArray();
        var accumulator = Enumerable.Repeat(zero, outputWidth).ToArray();

        for (var multiplierIndex = 0; multiplierIndex < right.Width; multiplierIndex++)
        {
            var partial = Enumerable.Repeat(zero, outputWidth).ToArray();
            for (var multiplicandIndex = 0; multiplicandIndex < left.Width; multiplicandIndex++)
            {
                var destination = multiplicandIndex + multiplierIndex;
                partial[destination] = network.And(multiplicand[multiplicandIndex], multiplier[multiplierIndex]);
            }

            (accumulator, _) = AddSignals(network, accumulator, partial, zero);
        }

        return new WordResult(
            new BinaryWord(accumulator.Select(signal => signal.State)),
            network.Cost);
    }

    public static WordResult Increment(BinaryWord value)
    {
        var one = BinaryWord.One(value.Width);
        var result = Add(value, one);
        return new WordResult(result.Value, result.Cost);
    }

    private static WordResult SelectByOrder(BinaryWord left, BinaryWord right, bool minimum)
    {
        EnsureSameWidth(left, right);
        var comparison = Compare(left, right);
        var network = new GateNetwork();
        var selectLeft = network.Input(minimum ? comparison.Less : comparison.Greater);
        var bits = new BitState[left.Width];
        for (var index = 0; index < left.Width; index++)
        {
            bits[index] = network.Mux(
                selectLeft,
                network.Input(left[index]),
                network.Input(right[index])).State;
        }

        return new WordResult(
            new BinaryWord(bits),
            new GateCost(
                comparison.Cost.NandEvaluations + network.Cost.NandEvaluations,
                comparison.Cost.CriticalPathDepth + network.Cost.CriticalPathDepth));
    }

    private static (Signal[] Sum, Signal Carry) AddSignals(
        GateNetwork network,
        IReadOnlyList<Signal> left,
        IReadOnlyList<Signal> right,
        Signal carryIn)
    {
        if (left.Count != right.Count)
        {
            throw new ArgumentException("Signal-vector operands must have identical widths.");
        }

        var sum = new Signal[left.Count];
        var carry = carryIn;
        for (var index = 0; index < left.Count; index++)
        {
            var leftXorRight = network.Xor(left[index], right[index]);
            sum[index] = network.Xor(leftXorRight, carry);
            var carryFromPair = network.And(left[index], right[index]);
            var carryFromInput = network.And(leftXorRight, carry);
            carry = network.Or(carryFromPair, carryFromInput);
        }

        return (sum, carry);
    }

    private static void EnsureSameWidth(BinaryWord left, BinaryWord right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);
        if (left.Width != right.Width)
        {
            throw new ArgumentException("Binary circuit operands must have identical widths.");
        }
    }
}
