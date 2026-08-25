using PrimeAxiom.Core.Hardware;
using PrimeAxiom.Core.Substrate;

namespace PrimeAxiom.Tests;

public sealed class HardwareBaselineTests
{
    private static readonly BitState[] States = [BitState.Off, BitState.On];
    private static readonly string[] CompareOutputNames = ["less", "equal", "greater"];

    [Fact]
    public void StaticNetlistHasStableIdentityMetricsAndSettledTransitions()
    {
        var first = BaselineHardware.BuildXorGate();
        var second = BaselineHardware.BuildXorGate();

        Assert.Equal(first.Nodes, second.Nodes);
        Assert.Equal(first.NamedOutputs, second.NamedOutputs);
        Assert.Equal(first.DffBoundaries, second.DffBoundaries);
        Assert.Equal(Enumerable.Range(0, first.Nodes.Count), first.Nodes.Select(node => node.Id));
        Assert.Equal(first.Nodes.Count, first.Nodes.Select(node => node.Name).Distinct().Count());

        Assert.Equal(4, first.Metrics.Nand2Static);
        Assert.Equal(0, first.Metrics.DffStatic);
        Assert.Equal(2, first.Metrics.InputBits);
        Assert.Equal(1, first.Metrics.OutputBits);
        Assert.Equal(3, first.Metrics.PortBits);
        Assert.Equal(6, first.Metrics.WireBits);
        Assert.Equal(8, first.Metrics.NandInputPinConnections);
        Assert.Equal(9, first.Metrics.TotalNetSinks);
        Assert.Equal(first.Metrics.TotalNetSinks, first.Metrics.ConnectionsStatic);
        Assert.Equal(2, first.Metrics.MaximumFanout);
        Assert.Equal(3, first.Metrics.UnitNandCriticalDepth);
        Assert.True(first.Metrics.CrossRegionConnections > 0);
        Assert.Equal(CombinationalLoopStatus.Acyclic, first.Metrics.CombinationalLoopStatus);

        var allOff = first.Evaluate(BinaryInputs(a: false, b: false), compareWithAllOff: true);
        Assert.Equal(BitState.Off, allOff.Outputs["y"]);
        Assert.Equal(first.Metrics.Nand2Static, allOff.NandEvaluations);
        Assert.Equal(first.Metrics.Nand2Static, allOff.GateOutputs.Count);
        Assert.Equal(
            first.TopologicalNodeIds.Where(id => first.Nodes[id].Kind == NandNodeKind.Nand2),
            allOff.GateOutputs.Select(output => output.NodeId));
        Assert.Equal(3, allOff.NandOutputTransitions);
        Assert.Equal(0, allOff.InputTransitions);

        var rightOn = first.Evaluate(BinaryInputs(a: false, b: true), previous: allOff);
        Assert.Equal(BitState.On, rightOn.Outputs["y"]);
        Assert.Equal(2, rightOn.NandOutputTransitions);
        Assert.Equal(1, rightOn.InputTransitions);
        Assert.Equal(
            rightOn.SettledTransitions.OrderBy(transition => transition.NodeId),
            rightOn.SettledTransitions);
    }

    [Fact]
    public void NetlistRejectsDuplicateDriversMissingDriversCyclesAndDuplicateDffDrivers()
    {
        Assert.Throws<ArgumentException>(() => new NandNetlist(
            "duplicate",
            [
                new NandNodeSpec(0, "same", NandNodeKind.Input, "input"),
                new NandNodeSpec(1, "same", NandNodeKind.Input, "input"),
            ],
            [new NandOutputSpec("y", 0, "output")]));

        Assert.Throws<ArgumentException>(() => new NandNetlist(
            "missing",
            [
                new NandNodeSpec(0, "a", NandNodeKind.Input, "input"),
                new NandNodeSpec(1, "g", NandNodeKind.Nand2, "logic", 0, 99),
            ],
            [new NandOutputSpec("y", 1, "output")]));

        Assert.Throws<ArgumentException>(() => new NandNetlist(
            "cycle",
            [
                new NandNodeSpec(0, "a", NandNodeKind.Input, "input"),
                new NandNodeSpec(1, "g1", NandNodeKind.Nand2, "logic", 2, 0),
                new NandNodeSpec(2, "g2", NandNodeKind.Nand2, "logic", 1, 0),
            ],
            [new NandOutputSpec("y", 1, "output")]));

        Assert.Throws<ArgumentException>(() => new NandNetlist(
            "duplicate-dff",
            [
                new NandNodeSpec(0, "d", NandNodeKind.Input, "input"),
                new NandNodeSpec(1, "q", NandNodeKind.State, "state", InitialValue: BitState.Off),
            ],
            [new NandOutputSpec("q", 1, "output")],
            [
                new DffBoundarySpec(0, "first", 0, 1, "state"),
                new DffBoundarySpec(1, "second", 0, 1, "state"),
            ]));
    }

    [Fact]
    public void StaticMetricsCountCrossRegionAndCrossLaneSinksByPin()
    {
        var builder = new NandNetlistBuilder("cross-lane");
        var left = builder.Input("left", "datapath/lane:0");
        var right = builder.Input("right", "datapath/lane:1");
        var gate = builder.Nand("gate", left, right, "datapath/lane:1");
        builder.Output("y", gate, "ports/lane:0");
        var netlist = builder.Build();

        Assert.Equal(1, netlist.Metrics.Nand2Static);
        Assert.Equal(2, netlist.Metrics.NandInputPinConnections);
        Assert.Equal(3, netlist.Metrics.TotalNetSinks);
        Assert.Equal(2, netlist.Metrics.CrossRegionConnections);
        Assert.Equal(2, netlist.Metrics.CrossLaneConnections);
    }

    [Fact]
    public void EvaluationRejectsMissingUnknownUndefinedAndForeignTransitionInputs()
    {
        var and = BaselineHardware.BuildAndGate();
        Assert.Throws<ArgumentException>(() => and.Evaluate(
            new Dictionary<string, BitState> { ["a"] = BitState.Off }));
        Assert.Throws<ArgumentException>(() => and.Evaluate(
            new Dictionary<string, BitState>
            {
                ["a"] = BitState.Off,
                ["b"] = BitState.Off,
                ["extra"] = BitState.Off,
            }));
        Assert.Throws<ArgumentOutOfRangeException>(() => and.Evaluate(
            new Dictionary<string, BitState>
            {
                ["a"] = (BitState)2,
                ["b"] = BitState.Off,
            }));

        var previous = and.Evaluate(BinaryInputs(a: false, b: false));
        Assert.Throws<ArgumentException>(() =>
            BaselineHardware.BuildAndGate().Evaluate(BinaryInputs(a: true, b: true), previous: previous));
        Assert.Throws<ArgumentException>(() =>
            and.Evaluate(BinaryInputs(a: true, b: true), previous: previous, compareWithAllOff: true));
    }

    [Fact]
    public void DerivedGatesHaveCompleteTruthTablesAndExactStaticCosts()
    {
        var not = BaselineHardware.BuildNotGate();
        Assert.Equal((1, 1), (not.Metrics.Nand2Static, not.Metrics.UnitNandCriticalDepth));
        foreach (var value in States)
        {
            Assert.Equal(State(!value.ToBoolean()), not.Evaluate(UnaryInput(value)).Outputs["y"]);
        }

        var gates = new[]
        {
            (Netlist: BaselineHardware.BuildAndGate(), Nands: 2, Depth: 2,
                Expected: (Func<bool, bool, bool>)((left, right) => left && right)),
            (Netlist: BaselineHardware.BuildOrGate(), Nands: 3, Depth: 2,
                Expected: (Func<bool, bool, bool>)((left, right) => left || right)),
            (Netlist: BaselineHardware.BuildXorGate(), Nands: 4, Depth: 3,
                Expected: (Func<bool, bool, bool>)((left, right) => left ^ right)),
            (Netlist: BaselineHardware.BuildXnorGate(), Nands: 5, Depth: 4,
                Expected: (Func<bool, bool, bool>)((left, right) => left == right)),
        };

        foreach (var gate in gates)
        {
            Assert.Equal(gate.Nands, gate.Netlist.Metrics.Nand2Static);
            Assert.Equal(gate.Depth, gate.Netlist.Metrics.UnitNandCriticalDepth);
            foreach (var left in States)
            {
                foreach (var right in States)
                {
                    var evaluated = gate.Netlist.Evaluate(BinaryInputs(
                        left.ToBoolean(),
                        right.ToBoolean()));
                    Assert.Equal(
                        State(gate.Expected(left.ToBoolean(), right.ToBoolean())),
                        evaluated.Outputs["y"]);
                }
            }
        }

        var mux = BaselineHardware.BuildMuxGate();
        Assert.Equal(4, mux.Metrics.Nand2Static);
        Assert.Equal(3, mux.Metrics.UnitNandCriticalDepth);
        foreach (var select in States)
        {
            foreach (var whenOn in States)
            {
                foreach (var whenOff in States)
                {
                    var inputs = new Dictionary<string, BitState>(StringComparer.Ordinal)
                    {
                        ["select"] = select,
                        ["when_on"] = whenOn,
                        ["when_off"] = whenOff,
                    };
                    Assert.Equal(
                        select == BitState.On ? whenOn : whenOff,
                        mux.Evaluate(inputs).Outputs["y"]);
                }
            }
        }
    }

    [Fact]
    public void HalfAndFullAddersHaveCompleteTruthTablesAndExactStaticCosts()
    {
        var half = BaselineHardware.BuildHalfAdder();
        Assert.Equal(6, half.Metrics.Nand2Static);
        Assert.Equal(3, half.Metrics.UnitNandCriticalDepth);
        foreach (var left in States)
        {
            foreach (var right in States)
            {
                var expected = ToInt(left) + ToInt(right);
                var evaluated = half.Evaluate(BinaryInputs(left.ToBoolean(), right.ToBoolean()));
                Assert.Equal(State((expected & 1) != 0), evaluated.Outputs["sum"]);
                Assert.Equal(State(expected >= 2), evaluated.Outputs["carry"]);
            }
        }

        var full = BaselineHardware.BuildFullAdder();
        Assert.Equal(15, full.Metrics.Nand2Static);
        Assert.Equal(7, full.Metrics.UnitNandCriticalDepth);
        foreach (var left in States)
        {
            foreach (var right in States)
            {
                foreach (var carry in States)
                {
                    var inputs = BinaryInputs(left.ToBoolean(), right.ToBoolean());
                    inputs["carry_in"] = carry;
                    var expected = ToInt(left) + ToInt(right) + ToInt(carry);
                    var evaluated = full.Evaluate(inputs);
                    Assert.Equal(State((expected & 1) != 0), evaluated.Outputs["sum"]);
                    Assert.Equal(State(expected >= 2), evaluated.Outputs["carry_out"]);
                }
            }
        }
    }

    [Theory]
    [InlineData(4)]
    [InlineData(6)]
    [InlineData(8)]
    public void RippleAddSubtractAndCompareAreExhaustivelyEquivalent(int width)
    {
        var add = BaselineHardware.BuildRippleAdder(width);
        var subtract = BaselineHardware.BuildRippleSubtractor(width);
        var compare = BaselineHardware.BuildComparator(width);
        Assert.Equal(15 * width, add.Metrics.Nand2Static);
        Assert.Equal((4 * width) + 3, add.Metrics.UnitNandCriticalDepth);
        Assert.Equal(19 * width, subtract.Metrics.Nand2Static);
        Assert.Equal(6 * width, subtract.Metrics.UnitNandCriticalDepth);
        Assert.Equal(23 * width, compare.Metrics.Nand2Static);
        Assert.Equal((2 * width) + 6, compare.Metrics.UnitNandCriticalDepth);

        var limit = 1 << width;
        var mask = limit - 1;
        for (var left = 0; left < limit; left++)
        {
            for (var right = 0; right < limit; right++)
            {
                foreach (var carry in States)
                {
                    var inputs = WordInputs(width, left, right);
                    inputs["carry_in"] = carry;
                    var evaluated = add.Evaluate(inputs);
                    var expected = left + right + ToInt(carry);
                    Assert.Equal(expected & mask, ReadWord(evaluated, "sum", width));
                    Assert.Equal(State(expected >= limit), evaluated.Outputs["carry_out"]);
                }

                var subtractEvaluation = subtract.Evaluate(WordInputs(width, left, right));
                Assert.Equal((left - right) & mask, ReadWord(subtractEvaluation, "difference", width));
                Assert.Equal(State(left < right), subtractEvaluation.Outputs["borrow_out"]);

                var compareEvaluation = compare.Evaluate(WordInputs(width, left, right));
                Assert.Equal(State(left < right), compareEvaluation.Outputs["less"]);
                Assert.Equal(State(left == right), compareEvaluation.Outputs["equal"]);
                Assert.Equal(State(left > right), compareEvaluation.Outputs["greater"]);
                Assert.Equal(
                    1,
                    CompareOutputNames.Count(name => compareEvaluation.Outputs[name] == BitState.On));
            }
        }
    }

    [Theory]
    [InlineData(4)]
    [InlineData(6)]
    [InlineData(8)]
    public void ShiftAddMultiplierIsExhaustivelyEquivalentWithFullProduct(int width)
    {
        var multiplier = BaselineHardware.BuildShiftAddMultiplier(width);
        Assert.Equal(32 * width * width, multiplier.Metrics.Nand2Static);
        Assert.Equal((14 * width) - 1, multiplier.Metrics.UnitNandCriticalDepth);
        Assert.Equal(width * 2, multiplier.Metrics.OutputBits);

        var limit = 1 << width;
        for (var left = 0; left < limit; left++)
        {
            for (var right = 0; right < limit; right++)
            {
                var evaluated = multiplier.Evaluate(WordInputs(width, left, right));
                Assert.Equal(left * right, ReadWord(evaluated, "product", width * 2));
                Assert.Equal(multiplier.Metrics.Nand2Static, evaluated.NandEvaluations);
            }
        }
    }

    [Theory]
    [InlineData(4)]
    [InlineData(6)]
    [InlineData(8)]
    public void FunctionalUnitSelectsEveryOperation(int width)
    {
        var unit = BaselineHardware.BuildFunctionalUnit(width);
        Assert.Equal(width * 2, unit.Metrics.OutputBits);
        var limit = 1 << width;
        var mask = limit - 1;
        var operands = width == 4
            ? Enumerable.Range(0, limit).ToArray()
            : BoundaryOperands(width);

        foreach (var left in operands)
        {
            foreach (var right in operands)
            {
                foreach (var operation in Enum.GetValues<BaselineFuOperation>())
                {
                    var inputs = WordInputs(width, left, right);
                    inputs["opcode[0]"] = State((((int)operation) & 1) != 0);
                    inputs["opcode[1]"] = State((((int)operation) & 2) != 0);
                    var evaluated = unit.Evaluate(inputs);
                    var expected = operation switch
                    {
                        BaselineFuOperation.Add => left + right,
                        BaselineFuOperation.Subtract =>
                            ((left - right) & mask) | (left < right ? 1 << width : 0),
                        BaselineFuOperation.Multiply => left * right,
                        BaselineFuOperation.Compare =>
                            left < right ? 1 : left == right ? 2 : 4,
                        _ => throw new InvalidOperationException(),
                    };
                    Assert.Equal(expected, ReadWord(evaluated, "result", width * 2));
                }
            }
        }
    }

    [Theory]
    [InlineData(4)]
    [InlineData(6)]
    [InlineData(8)]
    public void RegisteredFunctionalUnitChargesEqualOperandAndResultBoundaries(int width)
    {
        var unit = BaselineHardware.BuildRegisteredFunctionalUnit(width);
        Assert.Equal((4 * width) + 2, unit.Metrics.DffStatic);
        Assert.Equal(unit.Metrics.DffStatic, unit.Metrics.StateBits);
        Assert.Equal((2 * width) + 2, unit.Metrics.InputBits);
        Assert.Equal(2 * width, unit.Metrics.OutputBits);
        Assert.Equal(Enumerable.Range(0, unit.DffBoundaries.Count), unit.DffBoundaries.Select(dff => dff.Id));

        var left = (1 << width) - 3;
        var right = 3;
        var inputs = RegisteredFuInputs(width, 0, 0, BaselineFuOperation.Add);
        var state = RegisteredFuState(width, left, right, BaselineFuOperation.Multiply);
        var evaluated = unit.Evaluate(inputs, state);
        Assert.Equal(left * right, ReadNextStateWord(evaluated, "result_q", width * 2));
        Assert.Equal(BitState.Off, evaluated.Outputs["result[0]"]);

        for (var index = 0; index < width; index++)
        {
            Assert.Equal(BitState.Off, evaluated.DffNextStates[$"a_q[{index}]"]);
            Assert.Equal(BitState.Off, evaluated.DffNextStates[$"b_q[{index}]"]);
        }
    }

    [Theory]
    [InlineData(4)]
    [InlineData(6)]
    [InlineData(8)]
    public void RegisterBoundaryHoldsOrCapturesAtomically(int width)
    {
        var register = BaselineHardware.BuildRegisterBoundary(width);
        Assert.Equal(width, register.Metrics.DffStatic);
        Assert.Equal(width, register.Metrics.StateBits);
        Assert.Equal(4 * width, register.Metrics.Nand2Static);

        var before = (1 << width) - 3;
        var data = 5;
        var held = register.Evaluate(
            RegisterInputs(width, data, enabled: false),
            StateWord("q", width, before));
        Assert.Equal(before, ReadWord(held, "q", width));
        Assert.Equal(before, ReadNextStateWord(held, "q", width));

        var captured = register.Evaluate(
            RegisterInputs(width, data, enabled: true),
            StateWord("q", width, before));
        Assert.Equal(before, ReadWord(captured, "q", width));
        Assert.Equal(data, ReadNextStateWord(captured, "q", width));
    }

    [Theory]
    [InlineData(4)]
    [InlineData(6)]
    [InlineData(8)]
    public void CounterBoundaryTraversesEveryStateAndReportsOnlyEnabledWrap(int width)
    {
        var counter = BaselineHardware.BuildCounterBoundary(width);
        Assert.Equal(width, counter.Metrics.DffStatic);
        Assert.Equal(width, counter.Metrics.StateBits);
        Assert.Equal((19 * width) + 2, counter.Metrics.Nand2Static);

        var limit = 1 << width;
        var state = StateWord("q", width, 0);
        NandEvaluation? previous = null;
        for (var expected = 0; expected < limit; expected++)
        {
            var evaluated = counter.Evaluate(
                new Dictionary<string, BitState> { ["enable"] = BitState.On },
                state,
                previous: previous);
            Assert.Equal(expected, ReadWord(evaluated, "q", width));
            Assert.Equal((expected + 1) & (limit - 1), ReadNextStateWord(evaluated, "q", width));
            Assert.Equal(State(expected == limit - 1), evaluated.Outputs["overflow"]);
            Assert.Equal(
                expected == 0 ? 0 : CountSetBits(expected ^ (expected - 1)),
                evaluated.StateBitTransitions);
            state = evaluated.DffNextStates.ToDictionary(pair => pair.Key, pair => pair.Value);
            previous = evaluated;
        }

        Assert.Equal(0, ReadStateWord(state, "q", width));
        var disabled = counter.Evaluate(
            new Dictionary<string, BitState> { ["enable"] = BitState.Off },
            StateWord("q", width, limit - 1));
        Assert.Equal(limit - 1, ReadNextStateWord(disabled, "q", width));
        Assert.Equal(BitState.Off, disabled.Outputs["overflow"]);
    }

    [Fact]
    public void WordHardwareRejectsWidthsOutsideFrozenBuild002Set()
    {
        foreach (var width in new[] { -1, 0, 1, 3, 5, 7, 9 })
        {
            Assert.False(BaselineHardware.IsSupportedWidth(width));
            Assert.Throws<ArgumentOutOfRangeException>(() => BaselineHardware.BuildRippleAdder(width));
            Assert.Throws<ArgumentOutOfRangeException>(() => BaselineHardware.BuildRippleSubtractor(width));
            Assert.Throws<ArgumentOutOfRangeException>(() => BaselineHardware.BuildComparator(width));
            Assert.Throws<ArgumentOutOfRangeException>(() => BaselineHardware.BuildShiftAddMultiplier(width));
            Assert.Throws<ArgumentOutOfRangeException>(() => BaselineHardware.BuildFunctionalUnit(width));
            Assert.Throws<ArgumentOutOfRangeException>(() => BaselineHardware.BuildRegisteredFunctionalUnit(width));
            Assert.Throws<ArgumentOutOfRangeException>(() => BaselineHardware.BuildRegisterBoundary(width));
            Assert.Throws<ArgumentOutOfRangeException>(() => BaselineHardware.BuildCounterBoundary(width));
        }

        Assert.True(BaselineHardware.IsSupportedWidth(4));
        Assert.True(BaselineHardware.IsSupportedWidth(6));
        Assert.True(BaselineHardware.IsSupportedWidth(8));
    }

    private static Dictionary<string, BitState> UnaryInput(BitState value) =>
        new(StringComparer.Ordinal) { ["a"] = value };

    private static Dictionary<string, BitState> BinaryInputs(bool a, bool b) =>
        new(StringComparer.Ordinal)
        {
            ["a"] = State(a),
            ["b"] = State(b),
        };

    private static Dictionary<string, BitState> WordInputs(int width, int left, int right)
    {
        var inputs = new Dictionary<string, BitState>(StringComparer.Ordinal);
        AddWord(inputs, "a", width, left);
        AddWord(inputs, "b", width, right);
        return inputs;
    }

    private static Dictionary<string, BitState> RegisteredFuInputs(
        int width,
        int left,
        int right,
        BaselineFuOperation operation)
    {
        var inputs = new Dictionary<string, BitState>(StringComparer.Ordinal);
        AddWord(inputs, "a_in", width, left);
        AddWord(inputs, "b_in", width, right);
        AddWord(inputs, "opcode_in", 2, (int)operation);
        return inputs;
    }

    private static Dictionary<string, BitState> RegisteredFuState(
        int width,
        int left,
        int right,
        BaselineFuOperation operation)
    {
        var state = new Dictionary<string, BitState>(StringComparer.Ordinal);
        AddWord(state, "a_q", width, left);
        AddWord(state, "b_q", width, right);
        AddWord(state, "opcode_q", 2, (int)operation);
        AddWord(state, "result_q", width * 2, 0);
        return state;
    }

    private static Dictionary<string, BitState> RegisterInputs(int width, int data, bool enabled)
    {
        var inputs = new Dictionary<string, BitState>(StringComparer.Ordinal)
        {
            ["enable"] = State(enabled),
        };
        AddWord(inputs, "data", width, data);
        return inputs;
    }

    private static Dictionary<string, BitState> StateWord(string prefix, int width, int value)
    {
        var state = new Dictionary<string, BitState>(StringComparer.Ordinal);
        AddWord(state, prefix, width, value);
        return state;
    }

    private static void AddWord(
        Dictionary<string, BitState> destination,
        string prefix,
        int width,
        int value)
    {
        for (var index = 0; index < width; index++)
        {
            destination.Add($"{prefix}[{index}]", State(((value >> index) & 1) != 0));
        }
    }

    private static int ReadWord(NandEvaluation evaluation, string prefix, int width)
    {
        var value = 0;
        for (var index = 0; index < width; index++)
        {
            if (evaluation.Outputs[$"{prefix}[{index}]"] == BitState.On)
            {
                value |= 1 << index;
            }
        }

        return value;
    }

    private static int ReadNextStateWord(NandEvaluation evaluation, string prefix, int width)
    {
        var value = 0;
        for (var index = 0; index < width; index++)
        {
            if (evaluation.DffNextStates[$"{prefix}[{index}]"] == BitState.On)
            {
                value |= 1 << index;
            }
        }

        return value;
    }

    private static int ReadStateWord(
        IReadOnlyDictionary<string, BitState> state,
        string prefix,
        int width)
    {
        var value = 0;
        for (var index = 0; index < width; index++)
        {
            if (state[$"{prefix}[{index}]"] == BitState.On)
            {
                value |= 1 << index;
            }
        }

        return value;
    }

    private static int[] BoundaryOperands(int width) =>
        new[]
        {
            0,
            1,
            2,
            3,
            (1 << (width - 1)) - 1,
            1 << (width - 1),
            (1 << width) - 2,
            (1 << width) - 1,
        }.Distinct().ToArray();

    private static BitState State(bool value) =>
        value ? BitState.On : BitState.Off;

    private static int ToInt(BitState value) => value == BitState.On ? 1 : 0;

    private static int CountSetBits(int value)
    {
        var count = 0;
        while (value != 0)
        {
            count += value & 1;
            value >>= 1;
        }

        return count;
    }
}
