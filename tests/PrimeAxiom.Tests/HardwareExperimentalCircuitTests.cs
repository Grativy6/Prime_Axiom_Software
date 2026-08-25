using PrimeAxiom.Core.Hardware;
using PrimeAxiom.Core.Substrate;
using Xunit.Sdk;

namespace PrimeAxiom.Tests;

public sealed class HardwareExperimentalCircuitTests
{
    private static readonly int[] Widths = [4, 6, 8];

    [Fact]
    public void DeclaredCircuitsExposeExactFrozenGeometryAndMeasuredNandGraphs()
    {
        var expectedCaps = new Dictionary<int, int[]>
        {
            [4] = [3, 2, 1, 1],
            [6] = [5, 3, 2, 2],
            [8] = [7, 5, 3, 2],
        };
        var expectedBinaryWidths = new Dictionary<int, int[]>
        {
            [4] = [2, 2, 1, 1],
            [6] = [3, 2, 2, 2],
            [8] = [3, 3, 2, 2],
        };

        foreach (var width in Widths)
        {
            var circuits = AllCoreCircuits(width).ToArray();
            foreach (var circuit in circuits)
            {
                Assert.Equal("STRUCTURAL_DECLARED", circuit.EvidenceClass);
                Assert.Equal(width, circuit.Ports.Width);
                Assert.Equal(expectedCaps[width], circuit.Ports.Lanes.Select(lane => lane.Cap));
                Assert.Equal(ValuationHardwareDomain.S4, circuit.Ports.Lanes.Select(lane => lane.Prime));
                Assert.Equal(CombinationalLoopStatus.Acyclic, circuit.Netlist.Metrics.CombinationalLoopStatus);
                Assert.True(circuit.Netlist.Metrics.Nand2Static > 0);
                Assert.Equal(0, circuit.Netlist.Metrics.DffStatic);
                Assert.Equal(0, circuit.Netlist.Metrics.StateBits);
                Assert.True(circuit.Netlist.Metrics.CrossLaneConnections > 0);
                Assert.All(
                    circuit.Netlist.Nodes.Where(node => node.Kind == NandNodeKind.Nand2),
                    node => Assert.NotNull(node.LeftId));
                AssertPortMetadataMatchesGraph(circuit);
                for (var lane = 0; lane < circuit.Ports.Lanes.Count; lane++)
                {
                    Assert.Contains(
                        circuit.Netlist.Nodes,
                        node => node.Region.Split('/').Contains($"lane:{lane}", StringComparer.Ordinal));
                }

                if (circuit.Ports.Encoding == ExperimentalValuationEncoding.BinaryExponent)
                {
                    Assert.Equal(
                        expectedBinaryWidths[width],
                        circuit.Ports.Lanes.Select(lane => lane.PayloadWidth));
                }
                else
                {
                    Assert.Equal(
                        expectedCaps[width],
                        circuit.Ports.Lanes.Select(lane => lane.PayloadWidth));
                }
            }
        }
    }

    [Fact]
    public void BinaryExponentCircuitsExhaustEveryLegalVectorPair()
    {
        foreach (var width in Widths)
        {
            var states = StructuralStates(width);
            var declared = new Dictionary<ExperimentalValuationOperation, DeclaredExperimentalCircuit>
            {
                [ExperimentalValuationOperation.Compose] =
                    ExperimentalHardware.BuildBinaryExponentCompose(width),
                [ExperimentalValuationOperation.Cancel] =
                    ExperimentalHardware.BuildBinaryExponentCancel(width),
                [ExperimentalValuationOperation.Meet] =
                    ExperimentalHardware.BuildBinaryExponentMeet(width),
                [ExperimentalValuationOperation.Join] =
                    ExperimentalHardware.BuildBinaryExponentJoin(width),
                [ExperimentalValuationOperation.Divides] =
                    ExperimentalHardware.BuildBinaryExponentDivides(width),
            };

            Parallel.For(0, states.Count, leftIndex =>
            {
                var evaluators = declared.ToDictionary(
                    pair => pair.Key,
                    pair => new FastNandEvaluator(pair.Value.Netlist));
                var left = states[leftIndex];
                for (var rightIndex = 0; rightIndex < states.Count; rightIndex++)
                {
                    var right = states[rightIndex];
                    foreach (var pair in declared)
                    {
                        var evaluator = evaluators[pair.Key];
                        evaluator.Reset();
                        SetOperand(evaluator, pair.Value.Ports, isLeft: true, left);
                        SetOperand(evaluator, pair.Value.Ports, isLeft: false, right);
                        evaluator.Run();
                        CheckOperation(
                            evaluator,
                            pair.Value.Ports,
                            pair.Key,
                            left,
                            right,
                            $"BINEXP W={width} op={pair.Key} left={left} right={right}");
                    }
                }
            });
        }
    }

    [Fact]
    public void ThermometerCircuitsExhaustEveryLegalVectorPair()
    {
        foreach (var width in Widths)
        {
            var states = StructuralStates(width);
            var declared = new Dictionary<ExperimentalValuationOperation, DeclaredExperimentalCircuit>
            {
                [ExperimentalValuationOperation.Compose] =
                    ExperimentalHardware.BuildThermometerCompose(width),
                [ExperimentalValuationOperation.Meet] =
                    ExperimentalHardware.BuildThermometerMeet(width),
                [ExperimentalValuationOperation.Join] =
                    ExperimentalHardware.BuildThermometerJoin(width),
                [ExperimentalValuationOperation.Divides] =
                    ExperimentalHardware.BuildThermometerDivides(width),
            };

            Parallel.For(0, states.Count, leftIndex =>
            {
                var evaluators = declared.ToDictionary(
                    pair => pair.Key,
                    pair => new FastNandEvaluator(pair.Value.Netlist));
                var left = states[leftIndex];
                for (var rightIndex = 0; rightIndex < states.Count; rightIndex++)
                {
                    var right = states[rightIndex];
                    foreach (var pair in declared)
                    {
                        var evaluator = evaluators[pair.Key];
                        evaluator.Reset();
                        SetOperand(evaluator, pair.Value.Ports, isLeft: true, left);
                        SetOperand(evaluator, pair.Value.Ports, isLeft: false, right);
                        evaluator.Run();
                        CheckOperation(
                            evaluator,
                            pair.Value.Ports,
                            pair.Key,
                            left,
                            right,
                            $"THERM W={width} op={pair.Key} left={left} right={right}");
                    }
                }
            });
        }
    }

    [Fact]
    public void ThresholdQueriesExhaustEveryLegalStateLaneAndThreshold()
    {
        foreach (var width in Widths)
        {
            var states = StructuralStates(width);
            var domain = ValuationHardwareDomain.ForWidth(width);
            for (var lane = 0; lane < domain.LaneCount; lane++)
            {
                for (var exponent = 1; exponent <= domain.CapAt(lane); exponent++)
                {
                    var circuit = ExperimentalHardware.BuildThermometerThresholdQuery(
                        width,
                        domain.PrimeAt(lane),
                        exponent);
                    var evaluator = new FastNandEvaluator(circuit.Netlist);
                    foreach (var state in states)
                    {
                        evaluator.Reset();
                        SetOperand(evaluator, circuit.Ports, isLeft: true, state);
                        evaluator.Run();
                        var context = $"W={width} p={domain.PrimeAt(lane)} k={exponent} state={state}";
                        Require(!evaluator.Read(circuit.Ports.RejectOutput!), context + " reject");
                        Require(evaluator.Read(circuit.Ports.AcceptedOutput!), context + " accepted");
                        Require(
                            evaluator.Read(circuit.Ports.PredicateOutput!) ==
                            (state.IsZero || state.Exponents[lane] >= exponent),
                            context + " predicate");
                        Require(evaluator.Read(circuit.Ports.ExactOutput!), context + " exact");
                    }
                }
            }
        }
    }

    [Fact]
    public void SaturationMalformedStatesAndAtomicRejectionAreExplicit()
    {
        const int width = 6;
        var domain = ValuationHardwareDomain.ForWidth(width);
        var identity = new TestState(false, new int[domain.LaneCount]);
        var saturatedTwo = new TestState(false, [domain.CapAt(0), 0, 0, 0]);
        var zeroState = new TestState(true, new int[domain.LaneCount]);

        var binaryCompose = ExperimentalHardware.BuildBinaryExponentCompose(width);
        var binaryComposeEvaluator = new FastNandEvaluator(binaryCompose.Netlist);
        binaryComposeEvaluator.Reset();
        SetOperand(binaryComposeEvaluator, binaryCompose.Ports, true, saturatedTwo, saturatedLane: 0);
        SetOperand(binaryComposeEvaluator, binaryCompose.Ports, false, identity);
        binaryComposeEvaluator.Run();
        Require(!binaryComposeEvaluator.Read(binaryCompose.Ports.RejectOutput!), "binary saturated compose rejected");
        Require(
            binaryComposeEvaluator.Read(binaryCompose.Ports.Lanes[0].ResultSaturationOutput!),
            "binary saturation did not propagate");
        Require(
            ReadLane(
                binaryComposeEvaluator,
                binaryCompose.Ports.Lanes[0],
                binaryCompose.Ports.Encoding) == domain.CapAt(0),
            "binary saturated compose did not retain cap payload");

        binaryComposeEvaluator.Reset();
        SetOperand(binaryComposeEvaluator, binaryCompose.Ports, true, saturatedTwo, saturatedLane: 0);
        SetOperand(binaryComposeEvaluator, binaryCompose.Ports, false, zeroState);
        binaryComposeEvaluator.Run();
        Require(binaryComposeEvaluator.Read(binaryCompose.Ports.ResultZeroOutput!), "zero did not absorb compose");
        Require(
            !binaryComposeEvaluator.Read(binaryCompose.Ports.Lanes[0].ResultSaturationOutput!),
            "zero compose leaked saturation");

        foreach (var circuit in new[]
                 {
                     ExperimentalHardware.BuildBinaryExponentCancel(width),
                     ExperimentalHardware.BuildBinaryExponentMeet(width),
                     ExperimentalHardware.BuildBinaryExponentJoin(width),
                     ExperimentalHardware.BuildBinaryExponentDivides(width),
                 })
        {
            var evaluator = new FastNandEvaluator(circuit.Netlist);
            evaluator.Reset();
            SetOperand(evaluator, circuit.Ports, true, saturatedTwo, saturatedLane: 0);
            SetOperand(evaluator, circuit.Ports, false, identity);
            evaluator.Run();
            AssertRejectedAndCleared(evaluator, circuit.Ports, "binary saturated input");
        }

        binaryComposeEvaluator.Reset();
        SetOperand(binaryComposeEvaluator, binaryCompose.Ports, true, identity);
        SetOperand(binaryComposeEvaluator, binaryCompose.Ports, false, identity);
        binaryComposeEvaluator.Set(binaryCompose.Ports.Lanes[0].LeftPayloadInputs[1], true);
        binaryComposeEvaluator.Set(binaryCompose.Ports.Lanes[0].LeftPayloadInputs[2], true);
        binaryComposeEvaluator.Run();
        AssertRejectedAndCleared(binaryComposeEvaluator, binaryCompose.Ports, "binary exponent above cap");

        var binaryCancel = ExperimentalHardware.BuildBinaryExponentCancel(width);
        var binaryCancelEvaluator = new FastNandEvaluator(binaryCancel.Netlist);
        binaryCancelEvaluator.Reset();
        SetOperand(binaryCancelEvaluator, binaryCancel.Ports, true, identity);
        SetOperand(binaryCancelEvaluator, binaryCancel.Ports, false, new TestState(false, [1, 0, 0, 0]));
        binaryCancelEvaluator.Run();
        AssertRejectedAndCleared(binaryCancelEvaluator, binaryCancel.Ports, "binary cancellation underflow");

        var validator = ExperimentalHardware.BuildThermometerCanonicalValidator(width);
        var validatorEvaluator = new FastNandEvaluator(validator.Netlist);
        validatorEvaluator.Reset();
        SetOperand(validatorEvaluator, validator.Ports, true, identity);
        validatorEvaluator.Run();
        Require(validatorEvaluator.Read(validator.Ports.CanonicalOutput!), "canonical identity rejected");

        validatorEvaluator.Reset();
        SetOperand(validatorEvaluator, validator.Ports, true, identity);
        validatorEvaluator.Set(validator.Ports.Lanes[0].LeftPayloadInputs[1], true);
        validatorEvaluator.Run();
        Require(!validatorEvaluator.Read(validator.Ports.CanonicalOutput!), "thermometer 01 rise accepted");

        validatorEvaluator.Reset();
        SetOperand(validatorEvaluator, validator.Ports, true, zeroState);
        validatorEvaluator.Set(validator.Ports.Lanes[0].LeftPayloadInputs[0], true);
        validatorEvaluator.Run();
        Require(!validatorEvaluator.Read(validator.Ports.CanonicalOutput!), "zero payload accepted");

        validatorEvaluator.Reset();
        SetOperand(validatorEvaluator, validator.Ports, true, identity);
        validatorEvaluator.Set(validator.Ports.Lanes[0].LeftSaturationInput, true);
        validatorEvaluator.Run();
        Require(!validatorEvaluator.Read(validator.Ports.CanonicalOutput!), "partial saturated lane accepted");

        var thermCompose = ExperimentalHardware.BuildThermometerCompose(width);
        var thermComposeEvaluator = new FastNandEvaluator(thermCompose.Netlist);
        thermComposeEvaluator.Reset();
        SetOperand(thermComposeEvaluator, thermCompose.Ports, true, saturatedTwo, saturatedLane: 0);
        SetOperand(thermComposeEvaluator, thermCompose.Ports, false, identity);
        thermComposeEvaluator.Run();
        Require(!thermComposeEvaluator.Read(thermCompose.Ports.RejectOutput!), "therm saturated compose rejected");
        Require(
            thermComposeEvaluator.Read(thermCompose.Ports.Lanes[0].ResultSaturationOutput!),
            "therm saturation did not propagate");

        foreach (var circuit in new[]
                 {
                     ExperimentalHardware.BuildThermometerMeet(width),
                     ExperimentalHardware.BuildThermometerJoin(width),
                     ExperimentalHardware.BuildThermometerDivides(width),
                 })
        {
            var evaluator = new FastNandEvaluator(circuit.Netlist);
            evaluator.Reset();
            SetOperand(evaluator, circuit.Ports, true, saturatedTwo, saturatedLane: 0);
            SetOperand(evaluator, circuit.Ports, false, identity);
            evaluator.Run();
            AssertRejectedAndCleared(evaluator, circuit.Ports, "therm saturated input");
        }

        thermComposeEvaluator.Reset();
        SetOperand(thermComposeEvaluator, thermCompose.Ports, true, identity);
        SetOperand(thermComposeEvaluator, thermCompose.Ports, false, identity);
        thermComposeEvaluator.Set(thermCompose.Ports.Lanes[0].LeftPayloadInputs[1], true);
        thermComposeEvaluator.Run();
        AssertRejectedAndCleared(thermComposeEvaluator, thermCompose.Ports, "malformed thermometer compose");

        var query = ExperimentalHardware.BuildThermometerThresholdQuery(width, 2, domain.CapAt(0));
        var queryEvaluator = new FastNandEvaluator(query.Netlist);
        queryEvaluator.Reset();
        SetOperand(queryEvaluator, query.Ports, true, saturatedTwo, saturatedLane: 0);
        queryEvaluator.Run();
        Require(queryEvaluator.Read(query.Ports.PredicateOutput!), "saturated lower-bound query lost true fact");
        Require(!queryEvaluator.Read(query.Ports.ExactOutput!), "saturated query incorrectly exact");

        queryEvaluator.Reset();
        SetOperand(queryEvaluator, query.Ports, true, identity);
        queryEvaluator.Set(query.Ports.Lanes[0].LeftPayloadInputs[1], true);
        queryEvaluator.Run();
        AssertRejectedAndCleared(queryEvaluator, query.Ports, "malformed threshold query");
        Require(!queryEvaluator.Read(query.Ports.ExactOutput!), "malformed query retained exact status");
    }

    [Fact]
    public void NativeFunctionalUnitMatchesEveryOperationAndRejectsUnknownOpcodes()
    {
        foreach (var width in Widths)
        {
            var allStates = StructuralStates(width);
            var states = width == 4 ? allStates : FunctionalUnitBoundaryStates(width);
            var circuit = ExperimentalHardware.BuildBinaryExponentFunctionalUnit(width);
            var evaluator = new FastNandEvaluator(circuit.Netlist);
            foreach (var left in states)
            {
                foreach (var right in states)
                {
                    for (var opcode = 0; opcode < 8; opcode++)
                    {
                        evaluator.Reset();
                        SetOperand(evaluator, circuit.Ports, true, left);
                        SetOperand(evaluator, circuit.Ports, false, right);
                        SetUnsigned(evaluator, circuit.Ports.OpcodeInputs, opcode);
                        evaluator.Run();
                        var context = $"FU W={width} opcode={opcode} left={left} right={right}";
                        if (opcode <= (int)BinaryExponentFuOperation.Divides)
                        {
                            CheckOperation(
                                evaluator,
                                circuit.Ports,
                                (ExperimentalValuationOperation)opcode,
                                left,
                                right,
                                context);
                        }
                        else
                        {
                            AssertRejectedAndCleared(evaluator, circuit.Ports, context);
                        }
                    }
                }
            }
        }
    }

    [Fact]
    public void FastExhaustiveEvaluatorAgreesWithOfficialNetlistEvaluation()
    {
        foreach (var circuit in AllCoreCircuits(4))
        {
            var state = new TestState(false, [2, 1, 0, 1]);
            var other = new TestState(false, [1, 0, 1, 0]);
            var fast = new FastNandEvaluator(circuit.Netlist);
            fast.Reset();
            SetOperand(fast, circuit.Ports, true, state);
            if (circuit.Ports.RightZeroInput is not null)
            {
                SetOperand(fast, circuit.Ports, false, other);
            }

            if (circuit.Ports.OpcodeInputs.Count > 0)
            {
                SetUnsigned(fast, circuit.Ports.OpcodeInputs, (int)BinaryExponentFuOperation.Compose);
            }

            fast.Run();
            var official = circuit.Netlist.Evaluate(fast.InputStates());
            foreach (var output in circuit.Netlist.NamedOutputs)
            {
                Assert.Equal(
                    official.Outputs[output.Name] == BitState.On,
                    fast.Read(output.Name));
            }
        }
    }

    private static IEnumerable<DeclaredExperimentalCircuit> AllCoreCircuits(int width)
    {
        yield return ExperimentalHardware.BuildBinaryExponentCompose(width);
        yield return ExperimentalHardware.BuildBinaryExponentCancel(width);
        yield return ExperimentalHardware.BuildBinaryExponentMeet(width);
        yield return ExperimentalHardware.BuildBinaryExponentJoin(width);
        yield return ExperimentalHardware.BuildBinaryExponentDivides(width);
        yield return ExperimentalHardware.BuildBinaryExponentFunctionalUnit(width);
        yield return ExperimentalHardware.BuildThermometerCompose(width);
        yield return ExperimentalHardware.BuildThermometerMeet(width);
        yield return ExperimentalHardware.BuildThermometerJoin(width);
        yield return ExperimentalHardware.BuildThermometerDivides(width);
        yield return ExperimentalHardware.BuildThermometerCanonicalValidator(width);
        yield return ExperimentalHardware.BuildThermometerThresholdQuery(width, 2, 1);
    }

    private static void AssertPortMetadataMatchesGraph(DeclaredExperimentalCircuit circuit)
    {
        var expectedInputs = new HashSet<string>(StringComparer.Ordinal)
        {
            circuit.Ports.LeftZeroInput,
        };
        if (circuit.Ports.RightZeroInput is not null)
        {
            expectedInputs.Add(circuit.Ports.RightZeroInput);
        }

        foreach (var opcode in circuit.Ports.OpcodeInputs)
        {
            expectedInputs.Add(opcode);
        }

        foreach (var lane in circuit.Ports.Lanes)
        {
            expectedInputs.Add(lane.LeftSaturationInput);
            expectedInputs.UnionWith(lane.LeftPayloadInputs);
            if (lane.RightSaturationInput is not null)
            {
                expectedInputs.Add(lane.RightSaturationInput);
                expectedInputs.UnionWith(lane.RightPayloadInputs);
            }
        }

        var actualInputs = circuit.Netlist.Nodes
            .Where(node => node.Kind == NandNodeKind.Input)
            .Select(node => node.Name)
            .ToHashSet(StringComparer.Ordinal);
        Assert.True(expectedInputs.SetEquals(actualInputs));

        var expectedOutputs = new HashSet<string>(StringComparer.Ordinal);
        AddIfPresent(expectedOutputs, circuit.Ports.ResultZeroOutput);
        AddIfPresent(expectedOutputs, circuit.Ports.RejectOutput);
        AddIfPresent(expectedOutputs, circuit.Ports.AcceptedOutput);
        AddIfPresent(expectedOutputs, circuit.Ports.PredicateOutput);
        AddIfPresent(expectedOutputs, circuit.Ports.ExactOutput);
        AddIfPresent(expectedOutputs, circuit.Ports.CanonicalOutput);
        foreach (var lane in circuit.Ports.Lanes)
        {
            expectedOutputs.UnionWith(lane.ResultPayloadOutputs);
            AddIfPresent(expectedOutputs, lane.ResultSaturationOutput);
        }

        var actualOutputs = circuit.Netlist.NamedOutputs
            .Select(output => output.Name)
            .ToHashSet(StringComparer.Ordinal);
        Assert.True(expectedOutputs.SetEquals(actualOutputs));
    }

    private static void AddIfPresent(HashSet<string> values, string? value)
    {
        if (value is not null)
        {
            values.Add(value);
        }
    }

    private static void CheckOperation(
        FastNandEvaluator evaluator,
        ExperimentalPortLayout ports,
        ExperimentalValuationOperation operation,
        TestState left,
        TestState right,
        string context)
    {
        var caps = ports.Lanes.Select(lane => lane.Cap).ToArray();
        var expected = Oracle(operation, left, right, caps);
        Require(evaluator.Read(ports.RejectOutput!) == expected.Reject, context + " reject");
        Require(evaluator.Read(ports.AcceptedOutput!) != expected.Reject, context + " accepted");
        if (ports.PredicateOutput is not null)
        {
            Require(evaluator.Read(ports.PredicateOutput) == expected.Predicate, context + " predicate");
        }

        if (ports.ResultZeroOutput is null)
        {
            return;
        }

        Require(evaluator.Read(ports.ResultZeroOutput) == expected.IsZero, context + " zero");
        for (var lane = 0; lane < ports.Lanes.Count; lane++)
        {
            Require(
                ReadLane(evaluator, ports.Lanes[lane], ports.Encoding) == expected.Exponents[lane],
                context + $" lane={lane} payload");
            Require(
                evaluator.Read(ports.Lanes[lane].ResultSaturationOutput!) == expected.Saturated[lane],
                context + $" lane={lane} saturation");
        }
    }

    private static OracleResult Oracle(
        ExperimentalValuationOperation operation,
        TestState left,
        TestState right,
        IReadOnlyList<int> caps)
    {
        var exponents = new int[caps.Count];
        var saturated = new bool[caps.Count];
        var reject = false;
        var isZero = false;
        var predicate = false;
        switch (operation)
        {
            case ExperimentalValuationOperation.Compose:
                isZero = left.IsZero || right.IsZero;
                if (!isZero)
                {
                    for (var lane = 0; lane < caps.Count; lane++)
                    {
                        var sum = left.Exponents[lane] + right.Exponents[lane];
                        saturated[lane] = sum > caps[lane];
                        exponents[lane] = Math.Min(sum, caps[lane]);
                    }
                }

                break;
            case ExperimentalValuationOperation.Cancel:
                reject = right.IsZero ||
                         (!left.IsZero && Enumerable.Range(0, caps.Count)
                             .Any(lane => left.Exponents[lane] < right.Exponents[lane]));
                isZero = !reject && left.IsZero;
                if (!reject && !isZero)
                {
                    for (var lane = 0; lane < caps.Count; lane++)
                    {
                        exponents[lane] = left.Exponents[lane] - right.Exponents[lane];
                    }
                }

                break;
            case ExperimentalValuationOperation.Meet:
                isZero = left.IsZero && right.IsZero;
                if (!isZero)
                {
                    for (var lane = 0; lane < caps.Count; lane++)
                    {
                        exponents[lane] = left.IsZero
                            ? right.Exponents[lane]
                            : right.IsZero
                                ? left.Exponents[lane]
                                : Math.Min(left.Exponents[lane], right.Exponents[lane]);
                    }
                }

                break;
            case ExperimentalValuationOperation.Join:
                isZero = left.IsZero || right.IsZero;
                if (!isZero)
                {
                    for (var lane = 0; lane < caps.Count; lane++)
                    {
                        exponents[lane] = Math.Max(left.Exponents[lane], right.Exponents[lane]);
                    }
                }

                break;
            case ExperimentalValuationOperation.Divides:
                predicate = left.IsZero
                    ? right.IsZero
                    : right.IsZero || Enumerable.Range(0, caps.Count)
                        .All(lane => left.Exponents[lane] <= right.Exponents[lane]);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(operation));
        }

        return new OracleResult(reject, isZero, exponents, saturated, predicate);
    }

    private static int ReadLane(
        FastNandEvaluator evaluator,
        ExperimentalLanePortLayout lane,
        ExperimentalValuationEncoding encoding)
    {
        if (encoding == ExperimentalValuationEncoding.Thermometer)
        {
            return lane.ResultPayloadOutputs.Count(name => evaluator.Read(name));
        }

        var value = 0;
        for (var bit = 0; bit < lane.ResultPayloadOutputs.Count; bit++)
        {
            if (evaluator.Read(lane.ResultPayloadOutputs[bit]))
            {
                value |= 1 << bit;
            }
        }

        return value;
    }

    private static void AssertRejectedAndCleared(
        FastNandEvaluator evaluator,
        ExperimentalPortLayout ports,
        string context)
    {
        Require(evaluator.Read(ports.RejectOutput!), context + " reject not set");
        Require(!evaluator.Read(ports.AcceptedOutput!), context + " accepted remained set");
        if (ports.PredicateOutput is not null)
        {
            Require(!evaluator.Read(ports.PredicateOutput), context + " predicate not cleared");
        }

        if (ports.ResultZeroOutput is null)
        {
            return;
        }

        Require(!evaluator.Read(ports.ResultZeroOutput), context + " zero tag not cleared");
        foreach (var lane in ports.Lanes)
        {
            Require(
                lane.ResultPayloadOutputs.All(name => !evaluator.Read(name)),
                context + $" lane {lane.Lane} payload not cleared");
            Require(
                !evaluator.Read(lane.ResultSaturationOutput!),
                context + $" lane {lane.Lane} saturation not cleared");
        }
    }

    private static void SetOperand(
        FastNandEvaluator evaluator,
        ExperimentalPortLayout ports,
        bool isLeft,
        TestState state,
        int? saturatedLane = null)
    {
        evaluator.Set(isLeft ? ports.LeftZeroInput : ports.RightZeroInput!, state.IsZero);
        for (var lane = 0; lane < ports.Lanes.Count; lane++)
        {
            var layout = ports.Lanes[lane];
            var names = isLeft ? layout.LeftPayloadInputs : layout.RightPayloadInputs;
            if (ports.Encoding == ExperimentalValuationEncoding.BinaryExponent)
            {
                SetUnsigned(evaluator, names, state.Exponents[lane]);
            }
            else
            {
                for (var threshold = 1; threshold <= names.Count; threshold++)
                {
                    evaluator.Set(names[threshold - 1], state.Exponents[lane] >= threshold);
                }
            }

            evaluator.Set(
                isLeft ? layout.LeftSaturationInput : layout.RightSaturationInput!,
                saturatedLane == lane);
        }
    }

    private static void SetUnsigned(
        FastNandEvaluator evaluator,
        IReadOnlyList<string> names,
        int value)
    {
        for (var bit = 0; bit < names.Count; bit++)
        {
            evaluator.Set(names[bit], ((value >> bit) & 1) != 0);
        }
    }

    private static List<TestState> StructuralStates(int width)
    {
        var domain = ValuationHardwareDomain.ForWidth(width);
        var states = new List<TestState>
        {
            new(true, new int[domain.LaneCount]),
        };
        foreach (var exponents in ExponentVectors(domain))
        {
            states.Add(new TestState(false, exponents));
        }

        return states;
    }

    private static List<TestState> FunctionalUnitBoundaryStates(int width)
    {
        var domain = ValuationHardwareDomain.ForWidth(width);
        var states = new List<TestState>
        {
            new(true, new int[domain.LaneCount]),
            new(false, new int[domain.LaneCount]),
            new(false, domain.Caps.ToArray()),
        };
        for (var lane = 0; lane < domain.LaneCount; lane++)
        {
            var exponents = new int[domain.LaneCount];
            exponents[lane] = domain.CapAt(lane);
            states.Add(new TestState(false, exponents));
        }

        return states;
    }

    private static IEnumerable<int[]> ExponentVectors(ValuationHardwareDomain domain)
    {
        var current = new int[domain.LaneCount];
        return Visit(0);

        IEnumerable<int[]> Visit(int lane)
        {
            if (lane == domain.LaneCount)
            {
                yield return (int[])current.Clone();
                yield break;
            }

            for (var exponent = 0; exponent <= domain.CapAt(lane); exponent++)
            {
                current[lane] = exponent;
                foreach (var vector in Visit(lane + 1))
                {
                    yield return vector;
                }
            }
        }
    }

    private static void Require(bool condition, string context)
    {
        if (!condition)
        {
            throw new XunitException(context);
        }
    }

    private sealed record TestState(bool IsZero, int[] Exponents)
    {
        public override string ToString() =>
            IsZero ? "zero" : $"[{string.Join(',', Exponents)}]";
    }

    private sealed record OracleResult(
        bool Reject,
        bool IsZero,
        int[] Exponents,
        bool[] Saturated,
        bool Predicate);

    /// <summary>
    /// Allocation-light evaluator used only to make the frozen exhaustive
    /// domains tractable. It executes the public graph node-by-node and its
    /// sole combinational operation is !(left &amp;&amp; right). A separate test
    /// differentially checks it against NandNetlist.Evaluate.
    /// </summary>
    private sealed class FastNandEvaluator
    {
        private readonly NandNetlist _netlist;
        private readonly Dictionary<string, int> _inputs;
        private readonly Dictionary<string, int> _outputs;
        private readonly bool[] _values;

        public FastNandEvaluator(NandNetlist netlist)
        {
            _netlist = netlist;
            _inputs = netlist.Nodes
                .Where(node => node.Kind == NandNodeKind.Input)
                .ToDictionary(node => node.Name, node => node.Id, StringComparer.Ordinal);
            _outputs = netlist.NamedOutputs
                .ToDictionary(output => output.Name, output => output.NodeId, StringComparer.Ordinal);
            _values = new bool[netlist.Nodes.Count];
        }

        public void Reset() => Array.Clear(_values);

        public void Set(string name, bool value) => _values[_inputs[name]] = value;

        public bool Read(string name) => _values[_outputs[name]];

        public void Run()
        {
            foreach (var nodeId in _netlist.TopologicalNodeIds)
            {
                var node = _netlist.Nodes[nodeId];
                _values[nodeId] = node.Kind switch
                {
                    NandNodeKind.Input => _values[nodeId],
                    NandNodeKind.Constant or NandNodeKind.State => node.InitialValue == BitState.On,
                    NandNodeKind.Nand2 =>
                        !(_values[node.LeftId!.Value] && _values[node.RightId!.Value]),
                    _ => throw new InvalidOperationException($"Unknown NAND node kind {node.Kind}."),
                };
            }
        }

        public Dictionary<string, BitState> InputStates() =>
            _inputs.ToDictionary(
                pair => pair.Key,
                pair => _values[pair.Value] ? BitState.On : BitState.Off,
                StringComparer.Ordinal);
    }
}
