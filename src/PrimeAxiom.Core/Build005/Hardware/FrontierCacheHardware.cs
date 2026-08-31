using PrimeAxiom.Core.Hardware;
using PrimeAxiom.Core.Substrate;

namespace PrimeAxiom.Core.Build005.Hardware;

public sealed record FrontierCacheLineInput(
    uint Slot,
    uint Generation,
    uint PrimeIndex,
    uint Exponent,
    uint Residual,
    bool Terminal,
    bool Infinite);

public sealed record FrontierCacheCycleInput(
    uint QuerySlot,
    uint QueryGeneration,
    uint QueryPrimeIndex,
    bool UpdateEnable,
    uint UpdateIndex,
    FrontierCacheLineInput Update,
    bool InvalidateEnable,
    uint InvalidateSlot,
    bool Flush);

public sealed record FrontierCacheObservation(
    bool Hit,
    bool DuplicateMatch,
    uint HitIndex,
    uint Exponent,
    uint Residual,
    bool Terminal,
    bool Infinite,
    bool UpdateAccepted,
    bool UpdateRejected);

public sealed record FrontierCacheCycleReceipt(
    FrontierCacheObservation Observation,
    IReadOnlyDictionary<string, BitState> NextState,
    NandEvaluation Evaluation);

public sealed record DeclaredFrontierCacheCircuit(
    int Width,
    int Capacity,
    int ExponentWidth,
    int LineBits,
    NandNetlist Netlist)
{
    public const string ReplacementBoundary =
        "CALLER_SELECTED_UPDATE_INDEX; EXACT_LRU_POLICY_IS_NOT_INTEGRATED_IN_THIS_NETLIST";

    public const string GenerationWrapBoundary =
        "CALLER_ASSERTS_FLUSH_ON_8_BIT_GENERATION_WRAP; WRAP_DETECTION_IS_NOT_INTEGRATED";

    public NandStaticMetrics Metrics => Netlist.Metrics;

    /// <summary>
    /// Evaluates one lookup/update/invalidate/flush clock boundary. The caller
    /// supplies the write index selected by its separately charged replacement
    /// policy. This block therefore measures exact line state, parallel lookup,
    /// update, invalidation, and flush logic, but not an integrated LRU policy.
    /// </summary>
    public FrontierCacheCycleReceipt Evaluate(
        FrontierCacheCycleInput input,
        IReadOnlyDictionary<string, BitState>? state = null,
        NandEvaluation? previous = null,
        bool compareWithAllOff = false)
    {
        Validate(input);
        var inputs = new Dictionary<string, BitState>(StringComparer.Ordinal)
        {
            ["update_enable"] = ToState(input.UpdateEnable),
            ["invalidate_enable"] = ToState(input.InvalidateEnable),
            ["flush"] = ToState(input.Flush),
            ["update_terminal"] = ToState(input.Update.Terminal),
            ["update_infinite"] = ToState(input.Update.Infinite),
        };
        Build005HardwareBits.WriteWord(inputs, "query_slot", Build005HardwareDomain.SlotWidth, input.QuerySlot);
        Build005HardwareBits.WriteWord(
            inputs,
            "query_generation",
            Build005HardwareDomain.GenerationWidth,
            input.QueryGeneration);
        Build005HardwareBits.WriteWord(
            inputs,
            "query_prime",
            Build005HardwareDomain.PrimeIndexWidth,
            input.QueryPrimeIndex);
        Build005HardwareBits.WriteWord(
            inputs,
            "update_index",
            Build005HardwareDomain.CacheIndexWidth,
            input.UpdateIndex);
        Build005HardwareBits.WriteWord(inputs, "update_slot", Build005HardwareDomain.SlotWidth, input.Update.Slot);
        Build005HardwareBits.WriteWord(
            inputs,
            "update_generation",
            Build005HardwareDomain.GenerationWidth,
            input.Update.Generation);
        Build005HardwareBits.WriteWord(
            inputs,
            "update_prime",
            Build005HardwareDomain.PrimeIndexWidth,
            input.Update.PrimeIndex);
        Build005HardwareBits.WriteWord(inputs, "update_exponent", ExponentWidth, input.Update.Exponent);
        Build005HardwareBits.WriteWord(inputs, "update_residual", Width, input.Update.Residual);
        Build005HardwareBits.WriteWord(
            inputs,
            "invalidate_slot",
            Build005HardwareDomain.SlotWidth,
            input.InvalidateSlot);

        var evaluated = Netlist.Evaluate(inputs, state, previous, compareWithAllOff);
        var observation = new FrontierCacheObservation(
            Build005HardwareBits.ReadFlag(evaluated.Outputs, "hit"),
            Build005HardwareBits.ReadFlag(evaluated.Outputs, "duplicate_match"),
            Build005HardwareBits.ReadWord(
                evaluated.Outputs,
                "hit_index",
                Build005HardwareDomain.CacheIndexWidth),
            Build005HardwareBits.ReadWord(evaluated.Outputs, "exponent", ExponentWidth),
            Build005HardwareBits.ReadWord(evaluated.Outputs, "residual", Width),
            Build005HardwareBits.ReadFlag(evaluated.Outputs, "terminal"),
            Build005HardwareBits.ReadFlag(evaluated.Outputs, "infinite"),
            Build005HardwareBits.ReadFlag(evaluated.Outputs, "update_accepted"),
            Build005HardwareBits.ReadFlag(evaluated.Outputs, "update_rejected"));
        return new FrontierCacheCycleReceipt(observation, evaluated.DffNextStates, evaluated);
    }

    private void Validate(FrontierCacheCycleInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(input.Update);
        ValidateField(input.QuerySlot, Build005HardwareDomain.SlotWidth, nameof(input.QuerySlot));
        ValidateField(
            input.QueryGeneration,
            Build005HardwareDomain.GenerationWidth,
            nameof(input.QueryGeneration));
        ValidateField(
            input.QueryPrimeIndex,
            Build005HardwareDomain.PrimeIndexWidth,
            nameof(input.QueryPrimeIndex));
        ValidateField(
            input.UpdateIndex,
            Build005HardwareDomain.CacheIndexWidth,
            nameof(input.UpdateIndex));
        ValidateField(input.Update.Slot, Build005HardwareDomain.SlotWidth, nameof(input.Update.Slot));
        ValidateField(
            input.Update.Generation,
            Build005HardwareDomain.GenerationWidth,
            nameof(input.Update.Generation));
        ValidateField(
            input.Update.PrimeIndex,
            Build005HardwareDomain.PrimeIndexWidth,
            nameof(input.Update.PrimeIndex));
        ValidateField(input.Update.Exponent, ExponentWidth, nameof(input.Update.Exponent));
        RadixAwareValuationHardware.ValidateOperand(Width, input.Update.Residual, nameof(input.Update.Residual));
        ValidateField(
            input.InvalidateSlot,
            Build005HardwareDomain.SlotWidth,
            nameof(input.InvalidateSlot));
    }

    private static void ValidateField(uint value, int width, string parameterName)
    {
        if (value >= (1U << width))
        {
            throw new ArgumentOutOfRangeException(parameterName, value, "Field exceeds its declared width.");
        }
    }

    private static BitState ToState(bool value) => value ? BitState.On : BitState.Off;
}

public static class FrontierCacheHardware
{
    public static DeclaredFrontierCacheCircuit Build(int width, int capacity)
    {
        Build005HardwareDomain.ValidateWidth(width);
        Build005HardwareDomain.ValidateCacheCapacity(capacity);
        var exponentWidth = Build005HardwareDomain.ExponentWidth(width);
        var lineBits = Build005HardwareDomain.FrontierLineBits(width);
        var builder = new NandNetlistBuilder($"B005.FRONTIER_CACHE.W{width}.K{capacity}");

        var querySlot = Build005HardwareBits.Inputs(
            builder,
            "query_slot",
            Build005HardwareDomain.SlotWidth,
            "ports/input/query/slot");
        var queryGeneration = Build005HardwareBits.Inputs(
            builder,
            "query_generation",
            Build005HardwareDomain.GenerationWidth,
            "ports/input/query/generation");
        var queryPrime = Build005HardwareBits.Inputs(
            builder,
            "query_prime",
            Build005HardwareDomain.PrimeIndexWidth,
            "ports/input/query/prime");
        var updateEnable = builder.Input("update_enable", "ports/input/update/control");
        var updateIndex = Build005HardwareBits.Inputs(
            builder,
            "update_index",
            Build005HardwareDomain.CacheIndexWidth,
            "ports/input/update/index");
        var updateSlot = Build005HardwareBits.Inputs(
            builder,
            "update_slot",
            Build005HardwareDomain.SlotWidth,
            "ports/input/update/slot");
        var updateGeneration = Build005HardwareBits.Inputs(
            builder,
            "update_generation",
            Build005HardwareDomain.GenerationWidth,
            "ports/input/update/generation");
        var updatePrime = Build005HardwareBits.Inputs(
            builder,
            "update_prime",
            Build005HardwareDomain.PrimeIndexWidth,
            "ports/input/update/prime");
        var updateExponent = Build005HardwareBits.Inputs(
            builder,
            "update_exponent",
            exponentWidth,
            "ports/input/update/exponent");
        var updateResidual = Build005HardwareBits.Inputs(
            builder,
            "update_residual",
            width,
            "ports/input/update/residual");
        var updateTerminal = builder.Input("update_terminal", "ports/input/update/status");
        var updateInfinite = builder.Input("update_infinite", "ports/input/update/status");
        var invalidateEnable = builder.Input("invalidate_enable", "ports/input/invalidate/control");
        var invalidateSlot = Build005HardwareBits.Inputs(
            builder,
            "invalidate_slot",
            Build005HardwareDomain.SlotWidth,
            "ports/input/invalidate/slot");
        var flush = builder.Input("flush", "ports/input/flush");
        var zero = builder.Constant("const.zero", BitState.Off, "configuration/constants");
        var one = builder.Constant("const.one", BitState.On, "configuration/constants");
        var zeroExponent = Enumerable.Repeat(zero, exponentWidth).ToArray();
        var zeroResidual = Enumerable.Repeat(zero, width).ToArray();
        var zeroIndex = Enumerable.Repeat(zero, Build005HardwareDomain.CacheIndexWidth).ToArray();

        if (capacity == 0)
        {
            Build005HardwareBits.Outputs(builder, "hit_index", zeroIndex, "ports/output/hit_index");
            Build005HardwareBits.Outputs(builder, "exponent", zeroExponent, "ports/output/exponent");
            Build005HardwareBits.Outputs(builder, "residual", zeroResidual, "ports/output/residual");
            builder.Output("hit", zero, "ports/output/status");
            builder.Output("duplicate_match", zero, "ports/output/status");
            builder.Output("terminal", zero, "ports/output/status");
            builder.Output("infinite", zero, "ports/output/status");
            builder.Output("update_accepted", zero, "ports/output/status");
            builder.Output("update_rejected", updateEnable, "ports/output/status");
            return new DeclaredFrontierCacheCircuit(
                width,
                capacity,
                exponentWidth,
                lineBits,
                builder.Build());
        }

        var lines = new CacheLineSignals[capacity];
        var matches = new NandSignal[capacity];
        var writeSelects = new NandSignal[capacity];
        for (var line = 0; line < capacity; line++)
        {
            lines[line] = CreateLine(builder, width, exponentWidth, line);
            var region = $"cache/line:{line}/lookup";
            var slotEqual = Build005HardwareBits.EqualWord(
                builder,
                lines[line].Slot,
                querySlot,
                $"lookup.line[{line}].slot",
                $"{region}/slot");
            var generationEqual = Build005HardwareBits.EqualWord(
                builder,
                lines[line].Generation,
                queryGeneration,
                $"lookup.line[{line}].generation",
                $"{region}/generation");
            var primeEqual = Build005HardwareBits.EqualWord(
                builder,
                lines[line].Prime,
                queryPrime,
                $"lookup.line[{line}].prime",
                $"{region}/prime");
            var slotAndGeneration = NandLogic.And(
                builder,
                slotEqual,
                generationEqual,
                $"lookup.line[{line}].slot_generation",
                region);
            var keyEqual = NandLogic.And(
                builder,
                slotAndGeneration,
                primeEqual,
                $"lookup.line[{line}].key",
                region);
            matches[line] = NandLogic.And(
                builder,
                lines[line].Valid,
                keyEqual,
                $"lookup.line[{line}].valid_key",
                region);

            var lineIndex = Build005HardwareBits.ConstantWord(
                builder,
                $"line[{line}].index",
                Build005HardwareDomain.CacheIndexWidth,
                checked((uint)line),
                $"configuration/cache/line:{line}");
            var indexEqual = Build005HardwareBits.EqualWord(
                builder,
                updateIndex,
                lineIndex,
                $"update.line[{line}].index",
                $"cache/line:{line}/update/index");
            writeSelects[line] = NandLogic.And(
                builder,
                updateEnable,
                indexEqual,
                $"update.line[{line}].selected",
                $"cache/line:{line}/update/control");
        }

        var hit = Build005HardwareBits.ReduceOr(builder, matches, "lookup.hit", "cache/lookup/status");
        var duplicateTerms = new List<NandSignal>();
        for (var first = 0; first < capacity; first++)
        {
            for (var second = first + 1; second < capacity; second++)
            {
                duplicateTerms.Add(NandLogic.And(
                    builder,
                    matches[first],
                    matches[second],
                    $"lookup.duplicate[{first},{second}]",
                    "cache/lookup/duplicate"));
            }
        }

        var duplicate = duplicateTerms.Count == 0
            ? zero
            : Build005HardwareBits.ReduceOr(
                builder,
                duplicateTerms,
                "lookup.duplicate_any",
                "cache/lookup/duplicate");
        var selectedExponent = zeroExponent;
        var selectedResidual = zeroResidual;
        var selectedTerminal = zero;
        var selectedInfinite = zero;
        var selectedIndex = zeroIndex;
        for (var line = capacity - 1; line >= 0; line--)
        {
            selectedExponent = Build005HardwareBits.MuxWord(
                builder,
                matches[line],
                lines[line].Exponent,
                selectedExponent,
                $"lookup.select[{line}].exponent",
                "cache/lookup/select/exponent");
            selectedResidual = Build005HardwareBits.MuxWord(
                builder,
                matches[line],
                lines[line].Residual,
                selectedResidual,
                $"lookup.select[{line}].residual",
                "cache/lookup/select/residual");
            selectedTerminal = NandLogic.Mux(
                builder,
                matches[line],
                lines[line].Terminal,
                selectedTerminal,
                $"lookup.select[{line}].terminal",
                "cache/lookup/select/status");
            selectedInfinite = NandLogic.Mux(
                builder,
                matches[line],
                lines[line].Infinite,
                selectedInfinite,
                $"lookup.select[{line}].infinite",
                "cache/lookup/select/status");
            var lineIndex = Build005HardwareBits.ConstantWord(
                builder,
                $"select.line[{line}].index",
                Build005HardwareDomain.CacheIndexWidth,
                checked((uint)line),
                $"configuration/cache/select:{line}");
            selectedIndex = Build005HardwareBits.MuxWord(
                builder,
                matches[line],
                lineIndex,
                selectedIndex,
                $"lookup.select[{line}].index",
                "cache/lookup/select/index");
        }

        var updateAccepted = Build005HardwareBits.ReduceOr(
            builder,
            writeSelects,
            "update.accepted",
            "cache/update/status");
        var notUpdateAccepted = NandLogic.Not(
            builder,
            updateAccepted,
            "update.not_accepted",
            "cache/update/status");
        var updateRejected = NandLogic.And(
            builder,
            updateEnable,
            notUpdateAccepted,
            "update.rejected",
            "cache/update/status");

        for (var line = 0; line < capacity; line++)
        {
            var region = $"cache/line:{line}/state_update";
            var invalidationSlotEqual = Build005HardwareBits.EqualWord(
                builder,
                lines[line].Slot,
                invalidateSlot,
                $"invalidate.line[{line}].slot",
                $"{region}/invalidate");
            var invalidateLine = NandLogic.And(
                builder,
                invalidateEnable,
                invalidationSlotEqual,
                $"invalidate.line[{line}].enabled",
                $"{region}/invalidate");
            var clearLine = NandLogic.Or(
                builder,
                flush,
                invalidateLine,
                $"clear.line[{line}]",
                $"{region}/clear");

            AddFieldDff(
                builder,
                $"line[{line}].valid",
                [lines[line].Valid],
                [one],
                writeSelects[line],
                clearLine,
                [zero],
                $"{region}/valid");
            AddFieldDff(builder, $"line[{line}].slot", lines[line].Slot, updateSlot, writeSelects[line], clearLine, [zero, zero], $"{region}/slot");
            AddFieldDff(builder, $"line[{line}].generation", lines[line].Generation, updateGeneration, writeSelects[line], clearLine, Enumerable.Repeat(zero, Build005HardwareDomain.GenerationWidth).ToArray(), $"{region}/generation");
            AddFieldDff(builder, $"line[{line}].prime", lines[line].Prime, updatePrime, writeSelects[line], clearLine, Enumerable.Repeat(zero, Build005HardwareDomain.PrimeIndexWidth).ToArray(), $"{region}/prime");
            AddFieldDff(builder, $"line[{line}].exponent", lines[line].Exponent, updateExponent, writeSelects[line], clearLine, zeroExponent, $"{region}/exponent");
            AddFieldDff(builder, $"line[{line}].residual", lines[line].Residual, updateResidual, writeSelects[line], clearLine, zeroResidual, $"{region}/residual");
            AddFieldDff(builder, $"line[{line}].terminal", [lines[line].Terminal], [updateTerminal], writeSelects[line], clearLine, [zero], $"{region}/terminal");
            AddFieldDff(builder, $"line[{line}].infinite", [lines[line].Infinite], [updateInfinite], writeSelects[line], clearLine, [zero], $"{region}/infinite");
        }

        builder.Output("hit", hit, "ports/output/status");
        builder.Output("duplicate_match", duplicate, "ports/output/status");
        Build005HardwareBits.Outputs(builder, "hit_index", selectedIndex, "ports/output/hit_index");
        Build005HardwareBits.Outputs(builder, "exponent", selectedExponent, "ports/output/exponent");
        Build005HardwareBits.Outputs(builder, "residual", selectedResidual, "ports/output/residual");
        builder.Output("terminal", selectedTerminal, "ports/output/status");
        builder.Output("infinite", selectedInfinite, "ports/output/status");
        builder.Output("update_accepted", updateAccepted, "ports/output/status");
        builder.Output("update_rejected", updateRejected, "ports/output/status");
        return new DeclaredFrontierCacheCircuit(
            width,
            capacity,
            exponentWidth,
            lineBits,
            builder.Build());
    }

    private static CacheLineSignals CreateLine(
        NandNetlistBuilder builder,
        int width,
        int exponentWidth,
        int line)
    {
        var region = $"state/cache/line:{line}";
        return new CacheLineSignals(
            builder.State($"line[{line}].valid_q", BitState.Off, $"{region}/valid"),
            Build005HardwareBits.States(builder, $"line[{line}].slot_q", Build005HardwareDomain.SlotWidth, $"{region}/slot"),
            Build005HardwareBits.States(builder, $"line[{line}].generation_q", Build005HardwareDomain.GenerationWidth, $"{region}/generation"),
            Build005HardwareBits.States(builder, $"line[{line}].prime_q", Build005HardwareDomain.PrimeIndexWidth, $"{region}/prime"),
            Build005HardwareBits.States(builder, $"line[{line}].exponent_q", exponentWidth, $"{region}/exponent"),
            Build005HardwareBits.States(builder, $"line[{line}].residual_q", width, $"{region}/residual"),
            builder.State($"line[{line}].terminal_q", BitState.Off, $"{region}/terminal"),
            builder.State($"line[{line}].infinite_q", BitState.Off, $"{region}/infinite"));
    }

    private static void AddFieldDff(
        NandNetlistBuilder builder,
        string name,
        IReadOnlyList<NandSignal> state,
        IReadOnlyList<NandSignal> update,
        NandSignal write,
        NandSignal clear,
        IReadOnlyList<NandSignal> cleared,
        string region)
    {
        for (var index = 0; index < state.Count; index++)
        {
            var written = NandLogic.Mux(
                builder,
                write,
                update[index],
                state[index],
                $"{name}.bit[{index}].write",
                $"{region}/bit:{index}");
            var next = NandLogic.Mux(
                builder,
                clear,
                cleared[index],
                written,
                $"{name}.bit[{index}].clear",
                $"{region}/bit:{index}");
            builder.Dff($"{name}_reg[{index}]", next, state[index], $"{region}/bit:{index}");
        }
    }

    private sealed record CacheLineSignals(
        NandSignal Valid,
        NandSignal[] Slot,
        NandSignal[] Generation,
        NandSignal[] Prime,
        NandSignal[] Exponent,
        NandSignal[] Residual,
        NandSignal Terminal,
        NandSignal Infinite);
}
