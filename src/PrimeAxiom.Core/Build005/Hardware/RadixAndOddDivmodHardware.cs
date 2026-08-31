using System.Collections.ObjectModel;
using PrimeAxiom.Core.Hardware;
using PrimeAxiom.Core.Substrate;

namespace PrimeAxiom.Core.Build005.Hardware;

public sealed record PrimeCatalogueEvaluationReceipt(
    uint PrimeIndex,
    uint Divisor,
    bool Valid,
    bool IsTwo,
    bool IsOdd);

public sealed record DeclaredPrimeCatalogueCircuit(
    int Width,
    NandNetlist Netlist)
{
    public NandStaticMetrics Metrics => Netlist.Metrics;

    public PrimeCatalogueEvaluationReceipt Evaluate(uint primeIndex)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(
            primeIndex,
            1U << Build005HardwareDomain.PrimeIndexWidth);

        var inputs = Build005HardwareBits.InputWord(
            "prime_index",
            Build005HardwareDomain.PrimeIndexWidth,
            primeIndex);
        var evaluated = Netlist.Evaluate(inputs);
        return new PrimeCatalogueEvaluationReceipt(
            primeIndex,
            Build005HardwareBits.ReadWord(evaluated.Outputs, "divisor", Width),
            Build005HardwareBits.ReadFlag(evaluated.Outputs, "valid"),
            Build005HardwareBits.ReadFlag(evaluated.Outputs, "is_two"),
            Build005HardwareBits.ReadFlag(evaluated.Outputs, "is_odd"));
    }
}

public sealed record CtzEvaluationReceipt(
    int Width,
    uint Input,
    int Count,
    bool Zero,
    int NandEvaluations,
    int NandOutputTransitions)
{
    public const string EvidenceClass = "STRUCTURAL_DECLARED_EVALUATION";
}

public sealed record DeclaredCtzCircuit(
    int Width,
    int CountWidth,
    NandNetlist Netlist)
{
    public NandStaticMetrics Metrics => Netlist.Metrics;

    public CtzEvaluationReceipt Evaluate(uint value, bool compareWithAllOff = false)
    {
        RadixAwareValuationHardware.ValidateOperand(Width, value, nameof(value));
        var inputs = Build005HardwareBits.InputWord("value", Width, value);
        var evaluation = Netlist.Evaluate(inputs, compareWithAllOff: compareWithAllOff);
        return new CtzEvaluationReceipt(
            Width,
            value,
            checked((int)Build005HardwareBits.ReadWord(evaluation.Outputs, "count", CountWidth)),
            Build005HardwareBits.ReadFlag(evaluation.Outputs, "zero"),
            evaluation.NandEvaluations,
            evaluation.NandOutputTransitions);
    }
}

public sealed record OddDivmodRunReceipt(
    int Width,
    uint Dividend,
    uint Divisor,
    uint Quotient,
    uint Remainder,
    bool Exact,
    bool Rejected,
    int ClockCycles,
    int CombinationalEvaluations,
    long NandEvaluations,
    long NandOutputTransitions,
    long StateBitTransitions)
{
    public const string EvidenceClass = "STRUCTURAL_DECLARED_SEQUENTIAL_EVALUATION";
}

public sealed record DeclaredOddDivmodMachine(
    int Width,
    int CountWidth,
    NandNetlist Netlist)
{
    public NandStaticMetrics Metrics => Netlist.Metrics;

    /// <summary>
    /// Clocks the declared NAND/DFF machine. One load clock is followed by W
    /// restoring steps for a valid odd divisor. A final combinational
    /// observation is counted as an evaluation but not as an architectural
    /// clock. Even and zero divisors are rejected on the load clock.
    /// </summary>
    public OddDivmodRunReceipt Run(uint dividend, uint divisor)
    {
        RadixAwareValuationHardware.ValidateOperand(Width, dividend, nameof(dividend));
        RadixAwareValuationHardware.ValidateOperand(Width, divisor, nameof(divisor));

        var loadInputs = CreateInputs(load: true, dividend, divisor);
        var evaluation = Netlist.Evaluate(loadInputs, compareWithAllOff: true);
        IReadOnlyDictionary<string, BitState> state = evaluation.DffNextStates;
        var clocks = 1;
        var evaluations = 1;
        long nandEvaluations = evaluation.NandEvaluations;
        long nandTransitions = evaluation.NandOutputTransitions;
        long stateTransitions = evaluation.StateBitTransitions;

        var rejected = Build005HardwareBits.ReadFlag(state, "rejected_q");
        if (!rejected)
        {
            var stepInputs = CreateInputs(load: false, 0, 0);
            for (var cycle = 0; cycle < Width; cycle++)
            {
                var next = Netlist.Evaluate(stepInputs, state, evaluation);
                state = next.DffNextStates;
                evaluation = next;
                clocks++;
                evaluations++;
                nandEvaluations += next.NandEvaluations;
                nandTransitions += next.NandOutputTransitions;
                stateTransitions += next.StateBitTransitions;
            }
        }

        var observation = Netlist.Evaluate(CreateInputs(load: false, 0, 0), state, evaluation);
        evaluations++;
        nandEvaluations += observation.NandEvaluations;
        nandTransitions += observation.NandOutputTransitions;
        stateTransitions += observation.StateBitTransitions;

        return new OddDivmodRunReceipt(
            Width,
            dividend,
            divisor,
            Build005HardwareBits.ReadWord(observation.Outputs, "quotient", Width),
            Build005HardwareBits.ReadWord(observation.Outputs, "remainder", Width),
            Build005HardwareBits.ReadFlag(observation.Outputs, "exact"),
            Build005HardwareBits.ReadFlag(observation.Outputs, "rejected"),
            clocks,
            evaluations,
            nandEvaluations,
            nandTransitions,
            stateTransitions);
    }

    private Dictionary<string, BitState> CreateInputs(bool load, uint dividend, uint divisor)
    {
        var inputs = new Dictionary<string, BitState>(StringComparer.Ordinal)
        {
            ["load"] = load ? BitState.On : BitState.Off,
        };
        Build005HardwareBits.WriteWord(inputs, "dividend_in", Width, dividend);
        Build005HardwareBits.WriteWord(inputs, "divisor_in", Width, divisor);
        return inputs;
    }
}

public sealed record DeclaredRadixAwareValuationService(
    int Width,
    int CacheCapacity,
    DeclaredPrimeCatalogueCircuit Catalogue,
    DeclaredCtzCircuit Ctz,
    DeclaredOddDivmodMachine OddDivmod,
    DeclaredFrontierCacheCircuit Cache,
    CompositionalServiceCost Cost);

/// <summary>
/// A family shares one CTZ construction and one odd-DIVMOD construction across
/// K=0/1/2/4 cache variants. The service cost is an exact sum of separately
/// declared component cells and state; it is deliberately not represented as
/// one integrated critical path or routed netlist.
/// </summary>
public sealed record DeclaredRadixAwareValuationFamily(
    int Width,
    DeclaredPrimeCatalogueCircuit Catalogue,
    DeclaredCtzCircuit Ctz,
    DeclaredOddDivmodMachine OddDivmod,
    IReadOnlyDictionary<int, DeclaredRadixAwareValuationService> Services);

public static class RadixAwareValuationHardware
{
    public static DeclaredPrimeCatalogueCircuit BuildPrimeCatalogueSelector(int width)
    {
        Build005HardwareDomain.ValidateWidth(width);
        var builder = new NandNetlistBuilder($"B005.PRIME_CATALOGUE.W{width}");
        var index = Build005HardwareBits.Inputs(
            builder,
            "prime_index",
            Build005HardwareDomain.PrimeIndexWidth,
            "ports/input/prime_index");
        var zero = builder.Constant("const.zero", BitState.Off, "configuration/constants");
        var matches = new NandSignal[Build005HardwareDomain.PrimeCatalogue.Count];
        var selected = Enumerable.Repeat(zero, width).ToArray();
        for (var catalogueIndex = matches.Length - 1; catalogueIndex >= 0; catalogueIndex--)
        {
            var encodedIndex = Build005HardwareBits.ConstantWord(
                builder,
                $"catalogue[{catalogueIndex}].index",
                Build005HardwareDomain.PrimeIndexWidth,
                checked((uint)catalogueIndex),
                $"configuration/catalogue/entry:{catalogueIndex}/index");
            matches[catalogueIndex] = Build005HardwareBits.EqualWord(
                builder,
                index,
                encodedIndex,
                $"catalogue[{catalogueIndex}].match",
                $"catalogue/entry:{catalogueIndex}/match");
            var divisor = Build005HardwareBits.ConstantWord(
                builder,
                $"catalogue[{catalogueIndex}].divisor",
                width,
                checked((uint)Build005HardwareDomain.PrimeCatalogue[catalogueIndex]),
                $"configuration/catalogue/entry:{catalogueIndex}/divisor");
            selected = Build005HardwareBits.MuxWord(
                builder,
                matches[catalogueIndex],
                divisor,
                selected,
                $"catalogue[{catalogueIndex}].select",
                "catalogue/select");
        }

        var valid = Build005HardwareBits.ReduceOr(
            builder,
            matches,
            "catalogue.valid",
            "catalogue/status");
        var notTwo = NandLogic.Not(builder, matches[0], "catalogue.not_two", "catalogue/status");
        var isOdd = NandLogic.And(
            builder,
            valid,
            notTwo,
            "catalogue.is_odd",
            "catalogue/status");
        Build005HardwareBits.Outputs(builder, "divisor", selected, "ports/output/divisor");
        builder.Output("valid", valid, "ports/output/status");
        builder.Output("is_two", matches[0], "ports/output/status");
        builder.Output("is_odd", isOdd, "ports/output/status");
        return new DeclaredPrimeCatalogueCircuit(width, builder.Build());
    }

    public static DeclaredCtzCircuit BuildCtz(int width)
    {
        Build005HardwareDomain.ValidateWidth(width);
        var countWidth = Build005HardwareDomain.ExponentWidth(width);
        var builder = new NandNetlistBuilder($"B005.CTZ.W{width}");
        var value = Build005HardwareBits.Inputs(
            builder,
            "value",
            width,
            "ports/input/value");
        var zero = builder.Constant("const.zero", BitState.Off, "configuration/constants");
        var prefixZero = builder.Constant(
            "ctz.prefix.initial",
            BitState.On,
            "radix/ctz/prefix/initial");
        var count = Enumerable.Repeat(zero, countWidth).ToArray();

        for (var index = 0; index < width; index++)
        {
            var notBit = NandLogic.Not(
                builder,
                value[index],
                $"ctz.bit[{index}].not",
                $"radix/ctz/bit:{index}");
            prefixZero = NandLogic.And(
                builder,
                prefixZero,
                notBit,
                $"ctz.bit[{index}].prefix_zero",
                $"radix/ctz/bit:{index}");
            var addend = Enumerable.Repeat(zero, countWidth).ToArray();
            addend[0] = prefixZero;
            count = NandLogic.AddWord(
                builder,
                count,
                addend,
                zero,
                $"ctz.bit[{index}].increment",
                $"radix/ctz/bit:{index}/increment").Value;
        }

        Build005HardwareBits.Outputs(builder, "count", count, "ports/output/count");
        builder.Output("zero", prefixZero, "ports/output/status");
        return new DeclaredCtzCircuit(width, countWidth, builder.Build());
    }

    public static DeclaredOddDivmodMachine BuildOddDivmodMachine(int width)
    {
        Build005HardwareDomain.ValidateWidth(width);
        var countWidth = Build005HardwareDomain.ExponentWidth(width);
        var builder = new NandNetlistBuilder($"B005.ODD_DIVMOD_STEP.W{width}");
        var load = builder.Input("load", "ports/input/control");
        var dividendInput = Build005HardwareBits.Inputs(
            builder,
            "dividend_in",
            width,
            "ports/input/dividend");
        var divisorInput = Build005HardwareBits.Inputs(
            builder,
            "divisor_in",
            width,
            "ports/input/divisor");

        var dividendState = Build005HardwareBits.States(
            builder,
            "dividend_q",
            width,
            "state/divider/dividend");
        var divisorState = Build005HardwareBits.States(
            builder,
            "divisor_q",
            width,
            "state/divider/divisor");
        var remainderState = Build005HardwareBits.States(
            builder,
            "remainder_q",
            width + 1,
            "state/divider/remainder");
        var quotientState = Build005HardwareBits.States(
            builder,
            "quotient_q",
            width,
            "state/divider/quotient");
        var countState = Build005HardwareBits.States(
            builder,
            "count_q",
            countWidth,
            "state/divider/count");
        var runningState = builder.State("running_q", BitState.Off, "state/divider/control");
        var doneState = builder.State("done_q", BitState.Off, "state/divider/control");
        var rejectedState = builder.State("rejected_q", BitState.Off, "state/divider/control");

        var zero = builder.Constant("const.zero", BitState.Off, "configuration/constants");
        var one = builder.Constant("const.one", BitState.On, "configuration/constants");
        var zeroRemainder = Enumerable.Repeat(zero, width + 1).ToArray();
        var zeroWord = Enumerable.Repeat(zero, width).ToArray();
        var countLoad = Build005HardwareBits.ConstantWord(
            builder,
            "count.load",
            countWidth,
            checked((uint)width),
            "configuration/count");
        var countOne = Build005HardwareBits.ConstantWord(
            builder,
            "count.one",
            countWidth,
            1,
            "configuration/count");

        var inputNonzero = Build005HardwareBits.ReduceOr(
            builder,
            divisorInput,
            "load.divisor_nonzero",
            "divider/load/validation");
        var inputAccepted = NandLogic.And(
            builder,
            inputNonzero,
            divisorInput[0],
            "load.odd_nonzero",
            "divider/load/validation");
        var inputRejected = NandLogic.Not(
            builder,
            inputAccepted,
            "load.rejected",
            "divider/load/validation");

        var extendedDivisor = new NandSignal[width + 1];
        Array.Copy(divisorState, extendedDivisor, width);
        extendedDivisor[width] = zero;
        var shiftedRemainder = new NandSignal[width + 1];
        shiftedRemainder[0] = dividendState[width - 1];
        for (var index = 1; index < shiftedRemainder.Length; index++)
        {
            shiftedRemainder[index] = remainderState[index - 1];
        }

        var difference = NandLogic.SubtractWord(
            builder,
            shiftedRemainder,
            extendedDivisor,
            zero,
            "step.subtract",
            "divider/step/subtract");
        var noBorrow = NandLogic.Not(
            builder,
            difference.Status,
            "step.no_borrow",
            "divider/step/control");
        var executedRemainder = Build005HardwareBits.MuxWord(
            builder,
            noBorrow,
            difference.Value,
            shiftedRemainder,
            "step.restore",
            "divider/step/remainder");

        var shiftedDividend = new NandSignal[width];
        shiftedDividend[0] = zero;
        var shiftedQuotient = new NandSignal[width];
        shiftedQuotient[0] = noBorrow;
        for (var index = 1; index < width; index++)
        {
            shiftedDividend[index] = dividendState[index - 1];
            shiftedQuotient[index] = quotientState[index - 1];
        }

        var countIsOne = Build005HardwareBits.EqualWord(
            builder,
            countState,
            countOne,
            "step.count_is_one",
            "divider/step/control/count");
        var last = NandLogic.And(
            builder,
            runningState,
            countIsOne,
            "step.last",
            "divider/step/control");
        var decremented = NandLogic.SubtractWord(
            builder,
            countState,
            countOne,
            zero,
            "step.decrement",
            "divider/step/count").Value;

        var activeDividend = Build005HardwareBits.MuxWord(
            builder,
            runningState,
            shiftedDividend,
            dividendState,
            "step.active_dividend",
            "divider/step/hold/dividend");
        var activeRemainder = Build005HardwareBits.MuxWord(
            builder,
            runningState,
            executedRemainder,
            remainderState,
            "step.active_remainder",
            "divider/step/hold/remainder");
        var activeQuotient = Build005HardwareBits.MuxWord(
            builder,
            runningState,
            shiftedQuotient,
            quotientState,
            "step.active_quotient",
            "divider/step/hold/quotient");
        var activeCount = Build005HardwareBits.MuxWord(
            builder,
            runningState,
            decremented,
            countState,
            "step.active_count",
            "divider/step/hold/count");
        var notLast = NandLogic.Not(builder, last, "step.not_last", "divider/step/control");
        var continueRunning = NandLogic.And(
            builder,
            runningState,
            notLast,
            "step.continue_running",
            "divider/step/control");
        var completed = NandLogic.Or(
            builder,
            doneState,
            last,
            "step.completed",
            "divider/step/control");

        var nextDividend = Build005HardwareBits.MuxWord(
            builder,
            load,
            dividendInput,
            activeDividend,
            "load.select_dividend",
            "divider/load/dividend");
        var nextDivisor = Build005HardwareBits.MuxWord(
            builder,
            load,
            divisorInput,
            divisorState,
            "load.select_divisor",
            "divider/load/divisor");
        var nextRemainder = Build005HardwareBits.MuxWord(
            builder,
            load,
            zeroRemainder,
            activeRemainder,
            "load.select_remainder",
            "divider/load/remainder");
        var nextQuotient = Build005HardwareBits.MuxWord(
            builder,
            load,
            zeroWord,
            activeQuotient,
            "load.select_quotient",
            "divider/load/quotient");
        var nextCount = Build005HardwareBits.MuxWord(
            builder,
            load,
            countLoad,
            activeCount,
            "load.select_count",
            "divider/load/count");
        var nextRunning = NandLogic.Mux(
            builder,
            load,
            inputAccepted,
            continueRunning,
            "load.select_running",
            "divider/load/control");
        var nextDone = NandLogic.Mux(
            builder,
            load,
            inputRejected,
            completed,
            "load.select_done",
            "divider/load/control");
        var nextRejected = NandLogic.Mux(
            builder,
            load,
            inputRejected,
            rejectedState,
            "load.select_rejected",
            "divider/load/control");

        AddDffs(builder, "dividend", nextDividend, dividendState, "state/divider/dividend");
        AddDffs(builder, "divisor", nextDivisor, divisorState, "state/divider/divisor");
        AddDffs(builder, "remainder", nextRemainder, remainderState, "state/divider/remainder");
        AddDffs(builder, "quotient", nextQuotient, quotientState, "state/divider/quotient");
        AddDffs(builder, "count", nextCount, countState, "state/divider/count");
        builder.Dff("running_reg", nextRunning, runningState, "state/divider/control");
        builder.Dff("done_reg", nextDone, doneState, "state/divider/control");
        builder.Dff("rejected_reg", nextRejected, rejectedState, "state/divider/control");

        var remainderZero = Build005HardwareBits.IsZero(
            builder,
            remainderState,
            "output.remainder_zero",
            "divider/output/status");
        var notRejected = NandLogic.Not(
            builder,
            rejectedState,
            "output.not_rejected",
            "divider/output/status");
        var successfulDone = NandLogic.And(
            builder,
            doneState,
            notRejected,
            "output.successful_done",
            "divider/output/status");
        var exact = NandLogic.And(
            builder,
            successfulDone,
            remainderZero,
            "output.exact",
            "divider/output/status");

        Build005HardwareBits.Outputs(builder, "quotient", quotientState, "ports/output/quotient");
        Build005HardwareBits.Outputs(
            builder,
            "remainder",
            remainderState.Take(width).ToArray(),
            "ports/output/remainder");
        builder.Output("running", runningState, "ports/output/status");
        builder.Output("done", doneState, "ports/output/status");
        builder.Output("rejected", rejectedState, "ports/output/status");
        builder.Output("exact", exact, "ports/output/status");
        return new DeclaredOddDivmodMachine(width, countWidth, builder.Build());
    }

    public static DeclaredRadixAwareValuationFamily BuildFamily(int width)
    {
        Build005HardwareDomain.ValidateWidth(width);
        var catalogue = BuildPrimeCatalogueSelector(width);
        var ctz = BuildCtz(width);
        var divider = BuildOddDivmodMachine(width);
        var services = new Dictionary<int, DeclaredRadixAwareValuationService>();
        foreach (var capacity in Build005HardwareDomain.SupportedCacheCapacities)
        {
            var cache = FrontierCacheHardware.Build(width, capacity);
            var components = Array.AsReadOnly(new[]
            {
                new DeclaredComponentCost("PRIME_CATALOGUE", catalogue.Metrics),
                new DeclaredComponentCost("CTZ", ctz.Metrics),
                new DeclaredComponentCost("ODD_DIVMOD", divider.Metrics),
                new DeclaredComponentCost($"FRONTIER_CACHE_K{capacity}", cache.Metrics),
            });
            var cost = new CompositionalServiceCost(
                width,
                capacity,
                components.Sum(component => component.Metrics.Nand2Static),
                components.Sum(component => component.Metrics.DffStatic),
                components.Sum(component => component.Metrics.StateBits),
                checked(capacity * Build005HardwareDomain.FrontierLineBits(width)),
                components);
            services.Add(
                capacity,
                new DeclaredRadixAwareValuationService(
                    width,
                    capacity,
                    catalogue,
                    ctz,
                    divider,
                    cache,
                    cost));
        }

        return new DeclaredRadixAwareValuationFamily(
            width,
            catalogue,
            ctz,
            divider,
            new ReadOnlyDictionary<int, DeclaredRadixAwareValuationService>(services));
    }

    internal static void ValidateOperand(int width, uint value, string parameterName)
    {
        Build005HardwareDomain.ValidateWidth(width);
        if (width < 32 && value >= (1U << width))
        {
            throw new ArgumentOutOfRangeException(parameterName, value, "Operand exceeds the declared word width.");
        }
    }

    private static void AddDffs(
        NandNetlistBuilder builder,
        string name,
        IReadOnlyList<NandSignal> data,
        IReadOnlyList<NandSignal> state,
        string region)
    {
        for (var index = 0; index < data.Count; index++)
        {
            builder.Dff($"{name}_reg[{index}]", data[index], state[index], $"{region}/bit:{index}");
        }
    }
}
