# Build 002 hardware architecture

## Resulting fork

Both machines share the same two-state NAND substrate and the same explicit netlist implementation. They diverge only when a stable state vector is assigned meaning.

```text
                         stable binary state
                                  |
                   +--------------+--------------+
                   |                             |
            positional word               valuation state
                   |                             |
      ripple carry / compare /       per-prime counters or
      shift-add / restore / GCD       threshold-prefix lanes
                   |                             |
            conventional FU              structural VFU
```

The experimental fork does not replace bits, NAND, or registers. Prime identity is a fixed catalog attached to lane wiring. The gates themselves have no prime semantics.

## Shared floor: `NAND-NET`

`NandNetlist` is the common structural object for both C# lineages. It contains:

- stable, contiguous node identities;
- named input, constant, state-boundary, and NAND2 nets;
- named output sinks;
- explicit DFF data/Q boundary metadata;
- region and prime-lane labels;
- driver and combinational-cycle validation;
- topological two-state evaluation;
- settled gate/input/state transition traces;
- static gate, net, sink, fanout, depth, and cross-region metrics.

Every derived Boolean function calls the same `NandLogic` layer:

| Function | Declared NAND2 cells |
|---|---:|
| NOT | 1 |
| AND | 2 |
| OR | 3 |
| XOR | 4 |
| XNOR | 5 |
| 2:1 mux | 4 |
| half adder | 6 |
| full adder | 15 |

Signals are LSB-first inside word and exponent buses. Logical depth assigns one unit to each NAND dependency. Constants and direct connections have zero gate cost but remain driven nets and acquire sinks/fanout.

DFFs are deliberate architecture boundaries, not decomposed NAND circuits. The model therefore reports DFF/state count independently instead of silently choosing one flip-flop transistor topology.

## Conventional lineage

### Positional representation

For `W in {4,6,8}`, an unsigned value is a W-bit positional word in `[0, 2^W-1]`. Arithmetic status is explicit:

- ripple addition returns W sum bits and carry;
- ripple subtraction returns W difference bits and borrow;
- comparison returns less/equal/greater;
- multiplication returns the full 2W-bit product;
- division returns W quotient, W remainder, divide-by-zero, and exactness.

No test equates overflowed W-bit arithmetic with an unbounded result.

### `BIN-FU`

The combinational baseline calculates add, subtract, compare, and shift-add multiply in parallel and selects one 2W-bit result with a two-bit opcode tree. Add/sub status and comparison flags occupy documented result bits. The registered view has two W-bit operand registers, a two-bit opcode register, and a 2W-bit result register.

The repository also builds standalone W-bit registers and enabled counters so state cost is not inferred from a host variable.

### Restoring division

`BIN.DIVIDE_RESTORING` is a transparent unrolled unsigned divider. Each of W stages:

1. shifts one dividend bit into a W+1-bit remainder;
2. subtracts the zero-extended divisor;
3. uses borrow and divisor-nonzero to select subtraction or restoration;
4. emits one quotient bit.

Dividing by zero produces quotient zero, the unchanged dividend as remainder, `divide_by_zero=1`, and `exact=0`. This deliberately large one-cycle circuit is a reproducible conventional control, not a claim that it is an optimal divider.

### Subtractive GCD machine

`BIN.GCD_SUBTRACTIVE` is a registered state machine with two W-bit operands plus `running` and `done` state (`2W+2` DFFs). Load has priority. Each active cycle either:

- finishes on zero or equality and normalizes state to `(gcd,0)`; or
- subtracts the smaller operand from the larger.

The sum of nonnegative operands strictly decreases on every subtraction, giving a bounded termination argument. This is a transparent GCD baseline; faster binary-GCD and Euclidean hardware remain important prior-art controls rather than hidden assumptions.

## Experimental lineage

### Fixed catalog and lane geometry

The catalog is `S4=(2,3,5,7)`. Lane caps are the largest exponents individually realizable below `2^W`:

| W | Caps `(v2,v3,v5,v7)` | Binary exponent payload | Thermometer payload |
|---:|---|---:|---:|
| 4 | `(3,2,1,1)` | 6 bits | 7 bits |
| 6 | `(5,3,2,2)` | 9 bits | 12 bits |
| 8 | `(7,5,3,2)` | 10 bits | 17 bits |

The full Cartesian product of capped lanes is a valid structural state even when its reconstructed product exceeds the W-bit magnitude range. Hardware equivalence to conventional arithmetic is claimed only on the common legal domain.

Zero has an explicit tag. Binary and thermometer states also carry per-lane saturation status when an operation exceeds a cap. Saturated values are lower-bound facts, not silently exact exponent vectors.

### Binary-exponent VFU

`VFU-BINEXP-S4` stores each exponent as the minimum ordinary binary field that covers its lane cap. Its native combinational operations are:

| Instruction | Circuit action | Status |
|---|---|---|
| `COMPOSE` | componentwise exponent addition, cap comparison, clamp | per-lane saturation |
| `CANCEL` | componentwise subtraction | atomic reject on zero divisor, saturated input, or any underflow |
| `MEET` | componentwise minimum | exact inputs required |
| `JOIN` | componentwise maximum | exact inputs required |
| `DIVIDES` | conjunction of componentwise `<=` | predicate plus accepted/reject |

The five-operation native FU calculates these paths and selects with a three-bit opcode. It is intentionally combinational in the C# declared view; its port/state overhead is not misreported as a registered integrated unit.

For legal exact nonzero S4 states:

```text
COMPOSE  <-> multiplication
CANCEL   <-> exact division
MEET     <-> gcd
JOIN     <-> lcm
DIVIDES  <-> divisibility order
```

Those correspondences stop at the declared S4 support boundary and saturation behavior.

### Thermometer VFU

Lane threshold `t[p,k]` means `v_p(n) >= k`. Canonical lanes are true prefixes followed by false suffixes.

The direct circuits expose why this encoding is attractive for order-like operations:

- meet is bitwise AND;
- join is bitwise OR;
- divisibility is conjunction of threshold implications;
- a fixed `p^k | n` query is a selected threshold wire, plus zero/exact status;
- canonicality is a monotonic-prefix check.

Composition is less local. Output threshold k must determine whether the two input exponents sum to at least k. The declared circuit implements the monotone convolution

```text
t_out[k] = OR over i+j=k of (t_left[i] AND t_right[j])
```

with the implicit threshold-zero constant and a separate over-cap test. This removes binary carry chains but introduces multiple product terms and cross-threshold fan-in. Build 002 therefore measures thermometer composition rather than calling it carry-free and stopping there.

### Exact magnitude sidecar

`BIN+VSC-S4` keeps the W-bit magnitude authoritative and attaches exact S4 thresholds plus validity. Its semantic state cost is:

| W | Magnitude | Thresholds | Valid | Total state bits |
|---:|---:|---:|---:|---:|
| 4 | 4 | 7 | 1 | 12 |
| 6 | 6 | 12 | 1 | 19 |
| 8 | 8 | 17 | 1 | 26 |

The HDL implementation supplies a separately charged cold magnitude-to-valuation detector and sidecar query/known-factor update tops. These are integrated candidates, not part of the pure VFU's operation-only count.

Unsupported factors remain in magnitude. The threshold sidecar is a catalog projection and never claims a full factorization.

## Addition boundary

Binary addition remains local to `BIN-FU`. It is not closed as a coordinate-local operation in either valuation representation.

The sidecar uses the exact valuation law:

- if `v_p(a) != v_p(b)`, then `v_p(a+b)` is their minimum;
- if they are equal, only that minimum is known without examining the reduced sum.

The conservative hardware state therefore preserves certified true thresholds, invalidates exact-negative claims when necessary, and either refreshes from the authoritative magnitude or reports stale metadata. An exact structural output after addition must pay for recovery.

## Cold and warm boundaries

The machine exposes four distinct paths:

```text
COLD_MAG:
binary magnitude -> charged valuation acquisition -> structural execution

WARM_RESIDENT:
resident structural state -> structural execution

MAGNITUDE_FINAL:
structural execution -> one charged reconstruction

MAGNITUDE_EVERY_OP:
structural operation -> charged magnitude boundary after every instruction
```

No benchmark pools these paths. A warm structural operator is not evidence of cold replacement arithmetic.

## HDL realization

The strongest baseline and experimental components are expressed in synthesizable SystemVerilog under `hdl/` with W=4/6/8 wrappers. The shared flow uses the pinned OSS CAD Suite 2026-08-24, independent Icarus simulation, Verilator lint, Yosys SAT, and a common NAND mapping. Cold acquisition and sidecar circuits exist here even though their C# semantic model is not assigned a fictional static gate count.

Declared C# NAND graphs and optimized HDL netlists are distinct evidence views. Optimized synthesis may erase construction artifacts; it cannot rewrite the declared graph.

## Boundaries not crossed

Build 002 does not model or claim:

- transistor device behavior or an alternative physical switch;
- exact DFF transistor cost;
- glitches or physical switching energy;
- placed wire length, congestion, or buffered fanout;
- clock frequency or silicon area;
- a general prime factorizer;
- arbitrary-precision arithmetic;
- full GCD/LCM from an S4 projection;
- novelty for valuation vectors, componentwise lattices, threshold encodings, or factored arithmetic.

The architecture is deliberately small enough that its finite claims can be exhausted and its omitted boundaries can remain visible.
