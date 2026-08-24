# Build 000 historical reconstruction: evidence notes

Status: research notes for Prime Axiom Software Build 000, not final canon. Prepared 2026-08-24. The attached PAL/A0 documents were treated as reference context only; none was used as a historical source or specification here.

## Evidence discipline

- **Sourced fact** means the linked source directly supports the statement.
- **Engineering inference** is a conclusion drawn by comparing artifacts or architectures. It is not attributed to the source unless the source says it.
- Claims of “first” are used only with a category attached (for example, “first stored-program electronic digital computer”) and should still be treated cautiously. Several source institutions have a natural interest in their own machine's priority.
- “Necessary” below means necessary for a stated kind of implementation, not metaphysically necessary for all computation. Analogue, mechanical, electromechanical, and electronic lineages show that computation as a whole does not require two-state electronics.
- This pass deliberately does not use the Ishango or Lebombo bones as secure evidence of prehistoric arithmetic. Incisions are physical facts; their interpretation as numerical tallies is contested and unnecessary to the conclusions below.

## Main conclusion

The familiar story

`physical distinction -> bit -> binary integer -> Boolean arithmetic -> instruction -> software`

is not the historical sequence. At least five lineages developed partly independently and later converged:

1. material recording and counting (marks, counters, tallies);
2. positional representations and human-operated calculating surfaces (counting boards and abaci);
3. mechanical arithmetic (digit wheels, carries, stepped drums, difference engines);
4. external symbolic control and data processing (Jacquard cards, Babbage cards, Hollerith cards, paper tape);
5. switching and electronic machines (relay logic, binary and decimal machines, stored-program architectures).

The evidence strongly supports a fork **above reliable state and transition, but below a commitment to ordinary binary magnitude and a conventional ALU**. Binary physical events can carry decimal digits, control signals, instructions, unary counters, or another structural representation. That does not establish that prime-native structure is useful; it establishes only that binary magnitude is not forced at the first computational layer.

## Chronological reconstruction

### 1. Marks, tallies, counters, and the abstraction of quantity

**Sourced facts**

- The Whipple Museum's survey says early written quantity systems commonly used groups of lines for units and modified signs for groups of five or ten; counting sticks, knots, and tally sticks were widespread forms of record-keeping. It also notes that such systems persisted alongside Roman numerals into the Renaissance. [S1]
- The Smithsonian defines an abacus as a device on which arithmetic is performed by moving counters along rods, wires, or lines; medieval and Renaissance European users also moved loose counters on ruled counting boards. [S2]
- A Japanese soroban assigns columns to units, tens, hundreds, and so on. Only beads moved against the crossbar count toward the represented value. [S2]

**Engineering inference**

- A tally-like representation is close to one persistent physical distinction per counted unit. It needs token identity/persistence and a convention of correspondence; it does **not** need a radix, zero, carries, or a pre-existing abstract integer encoding.
- Unary/tally is not physically necessary. It is one low-interpretation-cost encoding whose storage and traversal cost grows linearly with quantity.
- The abacus is a crucial branch point: place value can live in a **spatial arrangement and operating practice** rather than in written numeral strings or a machine word. Quantity is a joint property of bead state and position.
- Grouping by five, ten, or another bundle size is compression and ergonomic convention. It reduces tokens but introduces normalization/carry rules.

### 2. Positional notation was an early fork, not an inevitable starting point

**Sourced facts**

- MacTutor documents Babylonian base-60 positional numerals built from only two basic signs. Earlier Sumerian and Akkadian systems were not positional. [S3]
- The same source records the ambiguity created by lacking a zero placeholder: the same written form could denote 1 or 60, and context had to supply scale. A later placeholder reduced some ambiguity. [S3]
- MacTutor notes that finite sexagesimal fractions can have denominators with prime factors 2, 3, and 5, whereas finite decimal fractions permit only 2 and 5. It explicitly says the historical reason for base 60 remains uncertain and surveys competing theories rather than claiming a settled design rationale. [S3]

**Engineering inference**

- Positional notation is a representation-compression scheme: a bounded digit alphabet plus position represents unbounded magnitude. Its costs are positional interpretation, a scale convention, normalization, and special treatment of empty places.
- A zero **placeholder** is an engineering response to positional ambiguity; zero as a number is a separate mathematical development.
- Decimal is contingent. Babylonian sexagesimal, later decimal, and modern binary are different points in a design space. Radix affects which fractions terminate, digit hardware complexity, word length, carry frequency, and human familiarity.
- Prime structure already appears indirectly in radix choice: the prime factors of a radix determine which rational denominators terminate. This is established mathematics, not evidence that a prime-exponent machine should replace positional arithmetic.

### 3. Mechanical arithmetic made carry and manufacture visible

**Sourced facts**

- The Whipple Museum describes early mechanical addition, the difficulty of automatic carry, and Leibniz's stepped reckoner. A stepped drum meshes with a number of teeth determined by lateral position; repeated cranking performs multiplication. [S1]
- The Deutsches Museum identifies Leibniz's surviving design lineage as a four-operation mechanical calculator dating to around 1700 and displays later four-operation machines. [S9]
- The Computer History Museum says Babbage's Difference Engine used finite differences so that polynomial tabulation required repeated addition rather than general multiplication or division. [S4]
- Babbage's engines were decimal digital machines with a separate gear wheel for each digit. CHM reports that Babbage considered bases 2, 3, 4, 5, 12, 16, and 100, then chose decimal for a combination of moving-part efficiency and everyday familiarity. [S4]
- The Difference Engine included automatic printing so that computed tables could pass into published output without manual transcription. [S4]

**Engineering inference**

- Carry is not a universal fact of quantity; it is a normalization obligation created by bounded positional digits. Mechanical machines expose its cost because a carry has to propagate through actual gearing.
- Leibniz's mechanism made multiplication an automated **schedule of digit selection, repeated addition, and carriage shift**. The machine's available primitives and its user-visible operation need not be the same thing.
- The Difference Engine is a strong historical counterexample to “a general arithmetic unit must come first.” For a bounded workload, choosing a representation/recurrence that removes multiplication and division made the machine more feasible.
- Babbage's radix study is direct evidence that binary was considered but not mechanically inevitable. The chosen radix optimized a mixed objective: parts, error behavior, and interface familiarity.

### 4. Control structure existed before numerical binary computers

**Sourced facts**

- The Smithsonian says Jacquard's early-nineteenth-century loom attachment used a sequence of punched cards, one per weave row, to determine which threads were raised. [S5]
- Babbage adapted the punched-card idea for the Analytical Engine. CHM describes separate `Store` and `Mill`, punched-card programming, four arithmetic functions, conditional branching, and iteration in the design. [S4]
- Smithsonian materials distinguish a later data-processing lineage: Hollerith's 1887 system used cards, a punch, a tabulator, and a sorter; it was used for the 1890 U.S. census. Later card systems added mechanical sorting and arithmetic. [S5]

**Engineering inference**

- Hole/no-hole is a two-state physical distinction, but the Jacquard card's immediate semantics were **selection and control**, not binary magnitude. This cleanly separates “binary substrate” from “binary number representation.”
- External programs are sufficient for programmable sequence control. A stored program is therefore not necessary for programmability, though it changes speed, flexibility, and the program/data boundary.
- Hollerith's lineage is computational without being centered on a general arithmetic stack. Selection, counting, classification, and sorting form a plausible alternate foundation for data-oriented machines.
- The historical transfer `loom control -> engine control -> tabulation/computer I/O` shows that a representation can migrate between domains while retaining only the physical distinction, not the original semantics.

### 5. Boolean algebra and switching circuits met late

**Sourced facts**

- George Boole's 1854 *An Investigation of the Laws of Thought* developed algebraic symbolic logic independently of electrical computing. The Berkeley record provides a digitized library edition. [S6]
- Claude Shannon's 1938 paper *A Symbolic Analysis of Relay and Switching Circuits* explicitly maps switching circuits to symbolic logic and gives methods for analysis and synthesis of relay/contact networks. The paper was an abstract of his MIT master's thesis. [S7]
- CHM documents George Stibitz's “Model K” relay circuit, built from telephone relays and able to add two binary digits; it led to a larger relay calculator and a 1940 remote Teletype demonstration. CHM's page dates the original Model K to 1936, while many other histories use 1937, so the exact year should not carry an argument. [S8]

**Engineering inference**

- Boolean algebra did not arise because electronic hardware demanded it. Shannon discovered a powerful isomorphism between an existing algebra of two-valued logic and relay contact networks.
- Two-state switching is an excellent robust digital substrate because open/closed (or low/high) states map economically to Boolean variables. It does not follow that the *numbers* carried by a switching network must be ordinary binary positional integers.
- Switching algebra primarily simplifies circuit composition, equivalence, and minimization. The semantics placed on the signals remain an architectural choice.

### 6. Binary and decimal machines coexisted

**Sourced facts**

- The Deutsches Museum describes Zuse's Z3 (1941) as a fully functional programmable binary machine built in the relay era. [S9]
- Iowa State's ABC history says Atanasoff and Berry built their special-purpose electronic digital machine in 1939-1942 for systems of linear equations. The project explicitly chose base two “in spite of custom,” regenerative capacitor memory, and direct logical action rather than enumeration. [S11]
- Harvard's Mark I was designed around modified commercial IBM components, delivered in 1944, and controlled by punched paper tape. Its 72 storage counters were modified mechanical adding machines; a motor and long drive shaft synchronized the machine. [S10]
- ENIAC was an electronic **decimal** machine. Penn describes the decade counter (states zero through nine) as the key subassembly; ten decade counters plus control formed an accumulator able to store and add a signed ten-digit number. [S12]
- Penn also records that ENIAC's pulse circuitry used ones and zeroes as data/control signals while decimal ring counters represented numeric digits. [S12]

**Engineering inference**

- Electronics did not force binary numerals: ENIAC implemented decimal digits electronically. Conversely, mechanical/electromechanical construction did not force decimal: Zuse's machines used binary.
- The cost distinction is concrete. A decimal electronic digit required a multi-state counter made from many switching elements; binary offers simpler digit cells but more digits. Which wins depends on component cost, reliability, interconnect, arithmetic, I/O, and human conversion.
- Mark I's global mechanical shaft shows that synchronization is a coordination requirement, not inherently an electronic clock.
- ABC is an important branch point because binary, regenerative memory, and electronics appeared in a special-purpose solver rather than as the inevitable package of a general stored-program computer.

### 7. Memory, arithmetic, and control boundaries were still fluid

**Sourced facts**

- The primary 1946 *Report on the ENIAC* says an accumulator served as both memory and arithmetic unit; it stored a signed ten-digit number and performed addition/subtraction. [S12]
- Von Neumann's *First Draft of a Report on the EDVAC* was distributed 30 June 1945. The Smithsonian scan is a primary document. Penn's later institutional history cautions that the report was authored by von Neumann but based at least partly on ideas from other members of the project. [S13]
- The IAS project's *Preliminary Discussion of the Logical Design of an Electronic Computing Instrument* (Burks, Goldstine, von Neumann, 28 June 1946) formalized distinct arithmetic, control, memory, input, and output organs. The IAS hosts the project report register. [S14]
- The Manchester Small-Scale Experimental Machine (“Baby”) ran a stored program on 21 June 1948. Its same random-access memory held both numbers and instructions; the official project history says this avoided physically reconfiguring circuitry for a new program. It used serial 32-bit two's-complement arithmetic. [S15]

**Engineering inference**

- A separate monolithic ALU is not primitive. ENIAC's accumulators combined register, arithmetic, transfer, and local control; later documents stabilized a cleaner arithmetic/control/memory partition.
- Stored-program architecture is a high-leverage convention, not a prerequisite for computation or programmability. It trades a fixed/external control path for mutable, addressable instruction state and permits program handling at memory speed.
- Treating instructions and numbers as same-memory words is also not logically forced. It is a representation unification with major engineering benefits and new hazards (self-modification, accidental code/data confusion).
- The modern `register -> ALU -> memory -> instruction decoder` boundary was earned through implementation and standardization; it should not be smuggled into Build 000's primitive simulator as if physically necessary.

## Necessary versus contingent: compact audit

| Layer or problem | Observed engineering requirement | Historically contingent choice |
|---|---|---|
| Persistent count/record | Some recoverable physical configuration if the result must outlast the act | notch, knot, pebble, bead, glyph, electrical state |
| Digital operation | Distinguishable regions of state plus transitions with adequate noise margin | exactly two states; electrical rather than mechanical states |
| Representation | A stable convention mapping physical configurations to interpreted objects | unary, positional magnitude, signed magnitude, two's complement, factor/exponent coordinates |
| Unbounded quantity with bounded symbols | Repetition, extensible storage, or positional/compositional structure | radix 2, 10, 60; prime coordinates; linked structures |
| Positional arithmetic | Normalization and carry/borrow or an equivalent redundant-digit rule | ripple carry, lookahead, mechanical carry, deferred normalization |
| Sequential computation | A way to order or enable transitions | shaft timing, asynchronous handoff, relay pulses, electronic clock, dataflow firing |
| Reuse of intermediate results | State persistence/addressability adequate for the algorithm | separate registers, accumulators, delay lines, CRT RAM, cards |
| Programmability | A selectable/repeatable control description | plugboards, drums, cards, paper tape, stored instructions |
| General-purpose arithmetic | Some realizable set of operations sufficient for the target computations | a centralized ALU and the familiar `ADD/SUB/MUL/DIV/SHIFT/COMPARE` menu |
| Reliability | Detectable margins, maintenance/error strategy, or redundancy appropriate to medium | Babbage jamming, parity/biquinary checks, preventive tube maintenance, ECC |

Important boundary: the first row becomes a physical necessity only when persistence is required; purely combinational or continuously observed computation need not retain a record. The second row applies to **digital** operation, not analogue computation in general.

## How machines came to exhibit `1, 2, 3, 4, ...`

This is a reconstruction, not a claim of one universal historical sequence.

1. **Correspondence:** one mark/counter/event is associated with one item or occurrence.
2. **Repeatable successor action:** add another mark or advance a counter one state.
3. **Grouping:** replace several unit marks by a higher-valued token or region.
4. **Place weighting:** interpret the same local digit by position (abacus columns, written places, geared digit wheels).
5. **Normalization:** when a local digit exceeds its range, carry into another position.
6. **Machine encoding:** implement each digit as gear position, relay pattern, pulse count, ring-counter phase, charge pattern, or another stable physical code.
7. **Arithmetic/control:** build state-transition networks that preserve the representation's invariants.
8. **Stored programs:** optionally encode the transition schedule in the same addressable memory as data.

The earliest credible Build 000 fork is between steps 3 and 5: after the substrate can preserve and address repeatable distinctions, but before quantity is committed to a radix-magnitude plus carry invariant. Prime identity cannot sensibly be physical “below distinction”; it requires at least a stable symbol/index convention. A prime-exponent representation could nevertheless be the first *numeric structure* built above generic binary cells.

That last sentence is an engineering hypothesis, not a historical finding.

## Surprising branch points worth preserving

1. **Abacus before numeral string:** positional value can be embodied spatially and operated on without first writing a conventional integer word.
2. **Base 60 was viable:** radix is not a natural law; it changes fraction exactness and ambiguity costs.
3. **Carry dominated mechanics:** positional normalization was a concrete mechanism, not a free arithmetic rule.
4. **Difference Engine removed multiplication:** workload-specific recurrence simplified the physical machine more than a general arithmetic unit would.
5. **Babbage evaluated binary and rejected it for his medium:** decimal was a multi-objective engineering decision, not an unquestioned inheritance.
6. **Punched-card control preceded computer numerics:** hole/no-hole first selected loom threads; binary physical distinctions need not mean binary numbers.
7. **Tabulation was an alternate computing lineage:** sorting and counting records scaled before general stored-program arithmetic.
8. **Boole and switching were independently motivated:** Shannon's bridge was a later synthesis, not a single co-origin of logic and hardware.
9. **Decimal electronics and binary mechanics both existed:** medium does not uniquely determine radix.
10. **Binary first appeared profitably in specialized machines:** ABC's special-purpose solver and Stibitz's small adder undercut a simple progression from universal theory to universal machine.
11. **Accumulator preceded clean ALU boundaries:** memory and arithmetic were fused locally in ENIAC.
12. **Programmability preceded stored programs:** cards, tape, and plugboards were executable control state; stored program unified and accelerated a capability that already existed.
13. **The first Baby program searched for a factor:** a binary stored-program machine's first demonstrated workload was number-theoretic, but it represented ordinary binary integers and found factors algorithmically; it was not prime-native.

## Implications and falsifiable hypotheses for Build 000

These are engineering inferences to test, not history claims.

1. **Use a shared substrate.** Implement state, transition, optional memory, and wiring once. Build at least a tally/counter lineage, a positional-binary lineage, and a prime-coordinate lineage on top so physical-operation counts are comparable.
2. **Do not equate bit with binary integer.** A bit can encode occupancy, control, sign, delimiter, a digit code, or sparse prime-coordinate structure.
3. **Measure normalization.** Count carry propagation and digit normalization explicitly in positional arithmetic. Count sparse-vector merge, exponent normalization, prime-index encoding, reconstruction, and factorization explicitly in the alternative.
4. **Keep control and number representation separable.** Punched-card history suggests that a conventional binary control plane can operate on a nonstandard data representation.
5. **Test fused units.** An exponent-bank that combines register and `COMPOSE/CANCEL/PROJECT` behavior may be more historically honest than forcing the idea into a conventional ALU interface. Compare it to a separate-unit design.
6. **Try workload-specific machines before a VM.** The Difference Engine suggests first testing a bounded recurrence or multiplicative workflow where structure can remove an operation. A general instruction set should be earned later.
7. **Treat conversion as an I/O boundary.** Human-readable magnitude and prime-coordinate state may coexist like decimal interfaces over binary devices. Charge every conversion.
8. **Negative-result criterion.** If the prime-coordinate lineage wins only when input is already factored and output remains factored, record the advantage as local and the costs as displaced, not foundational.

## Known limits of this historical pass

- It is a focused reconstruction, not a complete global history. Chinese rod numerals, Indian decimal place value, quipu, Islamic arithmetic transmission, African counting systems, Colossus, the Harvard/IBM attribution dispute, Soviet and Japanese developments, analogue computing, ternary machines, and reversible computing need dedicated follow-up if Build 001 depends on them.
- Museum pages are excellent for artifact descriptions but compress scholarly disputes. Their “first” labels are not independent adjudications.
- Institutional pages from Iowa State, Manchester, Penn, Harvard, and the Deutsches Museum have strong access to local artifacts and archives but also an institutional interest in priority narratives.
- The 1996 Penn history says EDSAC was the first machine to operate with stored-program design, which conflicts with Manchester's documented Baby run on 21 June 1948. Do not repeat the Penn “first” sentence. A careful formulation is: Baby first executed an electronically stored program; EDSAC soon became an early practical stored-program service machine. The second clause needs its own source if used in a final claim.
- No historical source found in this pass shows a machine whose primitive numeric state was a unique-factorization or prime-exponent representation. Absence from this source set is not evidence of nonexistence; the prior-art review must search that separately.

## Source register

All web sources were accessed 2026-08-24. Page publication dates are given only when displayed or intrinsic to the primary document.

### S1. Whipple Museum, University of Cambridge — early and mechanical calculation

- **Titles:** “A Brief History of Calculating Devices”; “Mechanical Calculation”
- **URLs:**
  - https://www.whipplemuseum.cam.ac.uk/explore-whipple-collections/calculating-devices/brief-history-calculating-devices
  - https://www.whipplemuseum.cam.ac.uk/explore-whipple-collections/calculating-devices/mechanical-calculation
- **Page date:** not stated.
- **Type/strength:** authoritative university-museum synthesis; secondary.
- **Supports:** tally/knotted/marked records, abacus lineage, early digit grouping, mechanical carry problem, Pascal/Leibniz mechanisms.
- **Caution:** broad survey; priority language such as “first” is simplified, and tally artifacts do not by themselves prove abstract arithmetic.

### S2. Smithsonian / National Museum of American History — abacus and numeral frame

- **Titles:** “The Abacus and the Numeral Frame”; “The Japanese Abacus”
- **URLs:**
  - https://americanhistory.si.edu/collections/object-groups/the-abacus-the-numeral-frame-and-counters
  - https://www.si.edu/spotlight/the-abacus-the-numeral-frame-and-counters/the-japanese-abacus
- **Page date:** not stated.
- **Type/strength:** authoritative museum object-group interpretation; secondary grounded in artifacts.
- **Supports:** counters moved along rods/lines; European counting boards; soroban column values and operational representation.
- **Caution:** its statement that the abacus “may have originated” in the Middle East is explicitly uncertain; do not use it as a settled origin claim.

### S3. MacTutor, University of St Andrews — Babylonian positional numerals

- **Title:** “Babylonian numerals”
- **URL:** https://mathshistory.st-andrews.ac.uk/HistTopics/Babylonian_numerals/
- **Date:** last updated December 2000.
- **Type/strength:** university-hosted historical synthesis with bibliography; secondary.
- **Supports:** base-60 positional system, two basic signs, placeholder ambiguity, fraction/radix consequences, uncertainty over origin of base 60.
- **Caution:** not a primary Assyriological publication; use cited specialist literature for any high-stakes ancient-history claim.

### S4. Computer History Museum — Babbage engines

- **Title:** “The Engines”
- **URL:** https://www.computerhistory.org/babbage/engines/
- **Page date:** not stated.
- **Type/strength:** specialist museum synthesis closely tied to the Babbage Engine reconstruction; strong secondary source.
- **Supports:** finite differences/repeated addition, decimal gear digits, alternative radices Babbage considered, rationale for decimal, Store/Mill, punched-card program, branching/iteration, automatic printing.
- **Caution:** modern architectural vocabulary is retrospective; the Analytical Engine was never completed in Babbage's lifetime.

### S5. Smithsonian / National Museum of American History — punched media and tabulation

- **Titles:** “Punch Cards”; “From Herman Hollerith to IBM”; “Hollerith Tabulating Machine”
- **URLs:**
  - https://www.americanhistory.si.edu/collections/object-groups/punch-cards
  - https://www.si.edu/spotlight/tabulating-equipment/from-herman-hollerith-to-ibm
  - https://www.si.edu/object/hollerith-tabulating-machine%3Anmah_694410
- **Page date:** not stated.
- **Type/strength:** authoritative museum object histories; strong secondary/artifact metadata.
- **Supports:** Jacquard row-control cards; Babbage's proposed card use; Hollerith's 1887 system and 1890 census deployment; punch/read/tabulate/sort workflow.
- **Caution:** the exact intellectual path from Jacquard/railroad cards to Hollerith is more complex than a one-line inheritance story.

### S6. George Boole — primary work in algebraic logic

- **Title:** *An Investigation of the Laws of Thought, on Which Are Founded the Mathematical Theories of Logic and Probabilities*
- **URL:** https://digicoll.lib.berkeley.edu/record/206199
- **Original publication:** Walton and Maberly, London, 1854; Berkeley record also describes a later Open Court collected-works edition.
- **Type/strength:** digitized primary mathematical text and library metadata.
- **Supports:** existence and content domain of algebraic symbolic logic before electrical switching theory.
- **Caution:** Boole did not design digital relay computers; the hardware connection is Shannon's later synthesis.

### S7. Claude E. Shannon — primary switching-circuit paper

- **Title:** “A Symbolic Analysis of Relay and Switching Circuits”
- **URL:** https://tubes.mit.edu/6S917/_static/2025/resources/shannon38.pdf
- **Publication:** *Transactions of the American Institute of Electrical Engineers*, vol. 57 (1938); manuscript submitted 1 March 1938, presented 20–24 June 1938. The paper states that it abstracts Shannon's MIT master's thesis.
- **Type/strength:** primary technical paper, hosted by MIT.
- **Supports:** algebraic analysis/synthesis of relay and contact networks and the explicit bridge from symbolic logic to switching.
- **Caution:** this establishes a representation method for switching circuits, not the physical necessity of Boolean or binary computation.

### S8. Computer History Museum — Stibitz relay computation

- **Title:** “George Stibitz”
- **URL:** https://www.computerhistory.org/revolution/birth-of-the-computer/4/85
- **Page date:** not stated; events described are 1930s–1940.
- **Type/strength:** museum synthesis plus artifact description; secondary.
- **Supports:** two-relay binary adder, relay-computer lineage, remote 1940 Teletype demonstration.
- **Caution:** CHM gives 1936 for the Model K on this page while other histories commonly use 1937. Avoid making the exact year load-bearing.

### S9. Deutsches Museum — mechanical, analogue, and binary machines

- **Title:** “Computers – the History of the Calculating Machine”
- **URL:** https://www.deutsches-museum.de/en/museumsinsel/ausstellung/computers
- **Page date:** not stated.
- **Type/strength:** authoritative museum exhibition description; secondary/artifact grounded.
- **Supports:** analogue and digital lineages, Leibniz replica, Z3 as a 1941 programmable binary machine, Z4 relay machine with mechanical memory.
- **Caution:** “first” and “universal computer” labels depend on definitions and exclude some secret/special-purpose projects.

### S10. Harvard Collection of Historical Scientific Instruments — Mark I

- **Titles:** “Harvard IBM Mark I – About”; “Harvard IBM Mark I – Function”
- **URLs:**
  - https://chsi.harvard.edu/harvard-ibm-mark-1-about
  - https://chsi.harvard.edu/harvard-ibm-mark-1-function
- **Page date:** not stated; machine designed 1937, delivered 1944, operated 1944–1959.
- **Type/strength:** authoritative institutional artifact interpretation; secondary with local archival access.
- **Supports:** modified commercial components, paper-tape control, 72 mechanical storage counters, centralized shaft timing, punch-card I/O.
- **Caution:** Harvard/IBM credit and “first programmable computer in the United States” terminology have historiographic nuances; not needed for this build's inference.

### S11. Iowa State University — Atanasoff–Berry Computer

- **Title:** “Atanasoff-Berry Computer: Operation/Purpose”
- **URL:** https://jva.cs.iastate.edu/operation.php
- **Page date:** not stated; site copyright 2011; events described 1939–1942.
- **Type/strength:** institutional history linked to primary proposal and manual; technically useful but advocacy-adjacent.
- **Supports:** special-purpose linear-equation workload; explicit choice of electronics, base two, regenerative capacitor memory, and direct logical action.
- **Caution:** Iowa State's broad “first electronic digital computer” claim is priority advocacy. Use the machine's documented features, not the unqualified first claim.

### S12. U.S. Army / University of Pennsylvania — ENIAC primary report and contextual history

- **Titles:** *A Report on the ENIAC (Electronic Numerical Integrator and Computer)*; “A Short History of the Second American Revolution”
- **URLs:**
  - https://ftp.arl.army.mil/~mike/comphist/46eniac-report/
  - https://almanac.upenn.edu/archive/v42/n18/eniac.html
- **Dates:** primary report 1 June 1946; Penn history 30 January 1996.
- **Type/strength:** primary Army/Penn technical report plus institutional historical synthesis.
- **Supports:** accumulator as memory plus arithmetic unit, signed ten-decimal-digit storage, decade counters, performance/reliability challenges, manual rewiring burden, project context.
- **Caution:** the Penn anniversary article contains an outdated/overbroad claim that EDSAC was the first operating stored-program design; use Manchester evidence for the 1948 Baby.

### S13. John von Neumann — EDVAC primary report

- **Title:** *First Draft of a Report on the EDVAC*
- **URL:** https://library.si.edu/digital-library/book/firstdraftofrepo00vonn
- **Date:** 30 June 1945 (Smithsonian catalog publication year 1945); DOI https://doi.org/10.5479/sil.538961.39088011475779
- **Type/strength:** Smithsonian scan of a primary design report.
- **Supports:** formal early description of a high-speed electronic stored-program architecture and its functional divisions.
- **Caution:** authorship is not equivalent to sole invention. Penn and later scholarship note contributions by Eckert, Mauchly, Goldstine, Burks, and others.

### S14. Institute for Advanced Study — Electronic Computer Project

- **Title:** “Electronic Computer Project” report register; especially *Preliminary Discussion of the Logical Design of an Electronic Computing Instrument*
- **URL:** https://www.ias.edu/library/ecp
- **Report date:** Burks, Goldstine, and von Neumann report, 28 June 1946.
- **Type/strength:** authoritative archive/register linking digitized primary project reports.
- **Supports:** stabilization of arithmetic, control, memory, input, and output as explicit design organs; later work on physical realization and coding.
- **Caution:** this is one highly influential lineage, not proof that its partition is the only natural architecture.

### S15. University of Manchester — Manchester Baby

- **Title:** “The Manchester Small Scale Experimental Machine — ‘The Baby’”
- **URL:** https://curation.cs.manchester.ac.uk/computer50/www.computer50.org/mark1/new.baby.html
- **Page date:** not stated (archival Computer 50 project); event date 21 June 1948.
- **Type/strength:** authoritative institutional project history with specifications and named participants.
- **Supports:** first successful stored-program run, same RAM for instructions and numbers, 32-bit serial two's-complement arithmetic, factor-search program, reduced reconfiguration cost.
- **Caution:** institutionally interested “first” claim; category must remain “stored-program electronic digital computer,” not unqualified “first computer.”

## Minimal citation set for the eventual report

If space is tight, the most probative sources for this Build 000 question are S3 (radix contingency), S4 (Babbage's explicit radix decision and recurrence machine), S5 (control/data cards), S7 (Boolean/switch bridge), S9/S11/S12 (binary and decimal machines across media), S13/S14 (architectural formalization), and S15 (stored-program implementation). S1/S2 are important for the pre-positional floor.
