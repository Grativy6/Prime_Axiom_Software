[CmdletBinding()]
param(
    [string]$OutputDirectory = 'artifacts/build004-verification'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$protocolId = 'PAS-BUILD004-EXACT-LINEAGE-0001'
$baselineCommit = '31dd150540bac79de3ee5925b44afdb7abaf327a'
$planHash = '2482698A57E857F07DBDEB7103B09EC36317661A0413ABBC2B20FAB7F44B53D1'
$frameworkStatus = 'BOUNDED_EXACT_LINEAGE_TOOLKIT_VALIDATED'
$partialStatus = 'PARTIAL — FINAL DECISION NOT EARNED'
$masterSeed = '0000000050415334'
$claimCeiling = 'Bounded exact-software and abstract-structure evidence under PAS-BUILD004-EXACT-LINEAGE-0001 only; no source-authenticity, empirical-validity, privacy, cryptographic-security, PAL-conformance, universal-performance, or hardware-PPA claim.'
$canonicalJsonContract = 'COMPACT_UTF8_JSON_V1__CAMEL_CASE__ENUM_STRINGS__ALL_NUMBERS_AS_CANONICAL_STRINGS__BIGINTEGER_DECIMAL__NO_BOM'
$canonicalCommand = 'dotnet run --project src/PrimeAxiom.Cli --configuration Release -- experiment-build004 --output results/build004'
$expectedChecks = 887072L
$expectedFiles = @(
    'README.md',
    'boundary_probes.json',
    'combinatorics.json',
    'correctness.json',
    'fusion.json',
    'just_intonation_demo.wav',
    'lineage.json',
    'manifest.json',
    'protocol_coverage.json',
    'structural_costs.csv'
)
$expectedFamilies = [ordered]@{
    'FACTORIAL_0_512' = 513L
    'BINOMIAL_ALL_0_256' = 33153L
    'HYPERGEOMETRIC_POINTS_0_24' = 20475L
    'HYPERGEOMETRIC_NORMALIZATION_0_32' = 12529L
    'SEEDED_POINTS_N_LE_4096' = 10000L
    'ADJACENT_STREAMS_0_48_PLUS_N2000' = 40426L
    'NAMED_COMBINATORIAL_CONTROLS' = 8L
    'SUPPORT_PROJECTION_U8' = 65536L
    'MULTIPLICITY_PROJECTION_U4_E0_2' = 6561L
    'DERIVATION_DAG_AND_MUTATIONS' = 12L
    'EXACT_FUSION_CYCLES' = 2L
    'SEEDED_ASYNC_FUSION_SCHEDULES' = 512L
    'FUSION_FAILURE_AND_RETRACTION_BOUNDARIES' = 9L
    'CALIBRATION_AUDIO_ACCUMULATOR_BOM_PROBES' = 12L
}
$expectedFamilyChecks = [ordered]@{
    'FACTORIAL_0_512' = 1026L
    'BINOMIAL_ALL_0_256' = 66306L
    'HYPERGEOMETRIC_POINTS_0_24' = 40950L
    'HYPERGEOMETRIC_NORMALIZATION_0_32' = 25058L
    'SEEDED_POINTS_N_LE_4096' = 10000L
    'ADJACENT_STREAMS_0_48_PLUS_N2000' = 271229L
    'NAMED_COMBINATORIAL_CONTROLS' = 8L
    'SUPPORT_PROJECTION_U8' = 393216L
    'MULTIPLICITY_PROJECTION_U4_E0_2' = 78732L
    'DERIVATION_DAG_AND_MUTATIONS' = 12L
    'EXACT_FUSION_CYCLES' = 2L
    'SEEDED_ASYNC_FUSION_SCHEDULES' = 512L
    'FUSION_FAILURE_AND_RETRACTION_BOUNDARIES' = 9L
    'CALIBRATION_AUDIO_ACCUMULATOR_BOM_PROBES' = 12L
}
$expectedOrdinaryControls = @(
    'BIGINTEGER_CROSS_CANCEL_BINOMIAL_V1',
    'BIGINTEGER_EXACT_HYPERGEOMETRIC_POINT_V1',
    'BIGINTEGER_ADJACENT_HYPERGEOMETRIC_V1'
)
$expectedProjectionContracts = @(
    'EXACT_ACTIVE_SOURCE_SUPPORT',
    'TOTAL_ATOM_OCCURRENCE_MULTIPLICITY',
    'RAW_PRIME_PRODUCT_SUPPORT',
    'SPARSE_ATOM_EXPONENTS',
    'DENSE_BINARY_PEV_SUPPORT'
)
$expectedBoundaryProjectionContracts = @(
    'SIGNED_PRIME_COORDINATE_NUMERIC_FACTOR_PROJECTION',
    'SIGNED_UNIT_DIMENSION_PROJECTION'
)
$expectedFusionFailures = [ordered]@{
    'OVERLAP_IDENTIFIED_PAYLOAD_EVICTED_FROM_BOTH_STATES' = 'OverlapPayloadUnavailable'
    'ATOM_ID_PAYLOAD_CONFLICT' = 'ConflictingAtomPayload'
    'PARTIAL_LINEAGE' = 'PartialLineage'
    'RECYCLED_EPOCH' = 'RegistryEpochMismatch'
    'AUTHENTICATION_REQUIRED_NOT_PROVIDED' = 'AuthenticationNotProvided'
}
$expectedCalibrationDispositions = [ordered]@{
    'ratioScaleComposition' = 'ExactRepresentationLocal'
    'elementaryChargeDefinitionFixture' = 'ExactRepresentationLocal'
    'affine' = 'ExplicitTransformCrossing'
    'logarithmic' = 'ExplicitTransformCrossing'
    'nonlinear' = 'ExplicitTransformCrossing'
    'rounded' = 'ExplicitTransformCrossing'
    'correlated' = 'ExplicitTransformCrossing'
    'expired' = 'Unresolved'
}
$expectedStructuralCosts = [ordered]@{}
$expectedStructuralLedgers = [ordered]@{}

function Get-Utf8Sha256 {
    param([Parameter(Mandatory)][string]$Text)

    return [Convert]::ToHexString(
        [Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($Text)))
}

function Get-ExpectedStructuralLedger {
    param(
        [Parameter(Mandatory)][string]$Domain,
        [Parameter(Mandatory)][string]$CaseId,
        [Parameter(Mandatory)][string]$Metric
    )

    if ($Domain -ceq 'PHYSICAL') { return 'PHYSICAL_HARDWARE_IMPLICATION' }
    if ($Domain -ceq 'LINEAGE_LOSS') { return 'ABSTRACT_STRUCTURE' }
    if ($Domain -ceq 'AUDIO') { return 'HOST_SOFTWARE_DIAGNOSTIC' }
    if ($Metric -clike '*canonical*utf8_bytes') { return 'HOST_SOFTWARE_DIAGNOSTIC' }
    if ($Domain -ceq 'COMBINATORICS') {
        if ($Metric -cin @('prime_basis_primes', 'additive_nodes', 'additive_terms')) {
            return 'ABSTRACT_STRUCTURE'
        }

        return 'HOST_SOFTWARE_DIAGNOSTIC'
    }
    if ($Domain -ceq 'LINEAGE' -and $CaseId -ceq 'CANONICAL_DAG_DIAGNOSTIC' -and
        $Metric -cne 'projection_queries') {
        return 'HOST_SOFTWARE_DIAGNOSTIC'
    }
    return 'ABSTRACT_STRUCTURE'
}

function Add-ExpectedStructuralCost {
    param(
        [Parameter(Mandatory)][string]$Domain,
        [Parameter(Mandatory)][string]$CaseId,
        [Parameter(Mandatory)][string]$Metric,
        [Parameter(Mandatory)][long]$Value,
        [Parameter(Mandatory)][string]$Unit,
        [Parameter(Mandatory)][string]$HardwareImplication
    )

    $key = "$Domain|$CaseId|$Metric"
    if ($expectedStructuralCosts.Contains($key)) {
        throw "Duplicate verifier structural-cost key: $key"
    }
    $expectedStructuralCosts.Add(
        $key,
        "$($Value.ToString([System.Globalization.CultureInfo]::InvariantCulture))|$Unit|$HardwareImplication")
    $expectedStructuralLedgers.Add($key, (Get-ExpectedStructuralLedger $Domain $CaseId $Metric))
}

function Add-ExpectedStructuralTextCost {
    param(
        [Parameter(Mandatory)][string]$Domain,
        [Parameter(Mandatory)][string]$CaseId,
        [Parameter(Mandatory)][string]$Metric,
        [Parameter(Mandatory)][string]$Value,
        [Parameter(Mandatory)][string]$Unit,
        [Parameter(Mandatory)][string]$HardwareImplication
    )

    $key = "$Domain|$CaseId|$Metric"
    if ($expectedStructuralCosts.Contains($key)) {
        throw "Duplicate verifier structural-cost key: $key"
    }
    $expectedStructuralCosts.Add($key, "$Value|$Unit|$HardwareImplication")
    $expectedStructuralLedgers.Add($key, (Get-ExpectedStructuralLedger $Domain $CaseId $Metric))
}

$combinatorialMetricUnits = [ordered]@{
    'prime_basis_candidates' = 'operations'
    'prime_basis_composite_marks' = 'operations'
    'prime_basis_primes' = 'primes'
    'factorial_cache_hits' = 'lookups'
    'factorial_cache_misses' = 'lookups'
    'factorial_valuation_calls' = 'calls'
    'legendre_quotient_steps' = 'operations'
    'coordinate_reads' = 'operations'
    'coordinate_writes' = 'operations'
    'coordinate_additions' = 'operations'
    'coordinate_zero_eliminations' = 'operations'
    'big_integer_powers' = 'operations'
    'big_integer_multiplications' = 'operations'
    'big_integer_exact_divisions' = 'operations'
    'exact_rational_additions' = 'operations'
    'greatest_common_divisors' = 'operations'
    'reconstructions' = 'operations'
    'additive_nodes' = 'nodes'
    'additive_terms' = 'terms'
    'exact_rational_reductions' = 'operations'
}
$expectedCombinatorialValues = [ordered]@{
    'PRIME_BASIS_0_5000' = @(4999, 8087, 669, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0)
    'HYPER_POINT_5000_1200_900_225' = @(0, 0, 0, 0, 9, 2891, 3160, 2891, 5548, 2222, 234, 435, 435, 0, 0, 0, 1, 0, 0, 0)
    'HYPER_TAIL_1000_413_271_GE117' = @(0, 0, 0, 965, 430, 24504, 30852, 116160, 131535, 90120, 9129, 16911, 17376, 476, 155, 310, 155, 1, 155, 83)
}
foreach ($case in $expectedCombinatorialValues.GetEnumerator()) {
    $values = @($case.Value)
    if ($values.Count -ne $combinatorialMetricUnits.Count) {
        throw "Verifier combinatorial cost vector has the wrong width: $($case.Key)"
    }
    $index = 0
    foreach ($metric in $combinatorialMetricUnits.GetEnumerator()) {
        Add-ExpectedStructuralCost 'COMBINATORICS' $case.Key $metric.Key $values[$index] $metric.Value 'HARDWARE_NOT_MEASURED'
        $index++
    }
}
Add-ExpectedStructuralCost 'COMBINATORICS' 'HYPER_POINT_5000_1200_900_225' 'canonical_receipt_utf8_bytes' 16756 'bytes' 'HOST_SERIALIZATION_ONLY__HARDWARE_NOT_MEASURED'
Add-ExpectedStructuralCost 'COMBINATORICS' 'HYPER_TAIL_1000_413_271_GE117' 'canonical_receipt_utf8_bytes' 781519 'bytes' 'HOST_SERIALIZATION_ONLY__HARDWARE_NOT_MEASURED'

$lineageWidthCases = @(
    [pscustomobject]@{ Id = 'FULL_SUPPORT_U8'; Universe = 8; Raw = 24; Dense = 8; Sparse = 8; Words = 1; SetBytes = 826; RawBytes = 991; SparseBytes = 984; DenseBytes = 156 },
    [pscustomobject]@{ Id = 'FULL_SUPPORT_U64'; Universe = 64; Raw = 417; Dense = 64; Sparse = 64; Words = 1; SetBytes = 5539; RawBytes = 5824; SparseBytes = 6872; DenseBytes = 173 },
    [pscustomobject]@{ Id = 'FULL_SUPPORT_U256'; Universe = 256; Raw = 2290; Dense = 256; Sparse = 256; Words = 4; SetBytes = 21924; RawBytes = 22774; SparseBytes = 27288; DenseBytes = 242 },
    [pscustomobject]@{ Id = 'FULL_SUPPORT_U1024'; Universe = 1024; Raw = 11583; Dense = 1024; Sparse = 1024; Words = 16; SetBytes = 88229; RawBytes = 91877; SparseBytes = 109720; DenseBytes = 518 }
)
foreach ($case in $lineageWidthCases) {
    Add-ExpectedStructuralCost 'LINEAGE' $case.Id 'raw_prime_product_width' $case.Raw 'bits' 'VARIABLE_WIDTH_DATAPATH_REQUIRED__NAND_DFF_PPA_NOT_MEASURED'
    Add-ExpectedStructuralCost 'LINEAGE' $case.Id 'dense_pev_width' $case.Dense 'bits' 'CONCEPTUAL_PARALLEL_AND_OR_DEPTH_1__NAND_DFF_PPA_NOT_MEASURED'
    Add-ExpectedStructuralCost 'LINEAGE' $case.Id 'sparse_entries' $case.Sparse 'entries' 'LOOKUP_AND_ROUTING_REQUIRED__NAND_DFF_PPA_NOT_MEASURED'
    Add-ExpectedStructuralCost 'LINEAGE' $case.Id 'input_universe' $case.Universe 'registered_atoms' 'REGISTRY_STORAGE_AND_ROUTING__NAND_DFF_PPA_NOT_MEASURED'
    Add-ExpectedStructuralCost 'LINEAGE' $case.Id 'active_atoms' $case.Universe 'atoms' 'STATE_ACTIVITY_AND_SWITCHING_NOT_MEASURED'
    Add-ExpectedStructuralCost 'LINEAGE' $case.Id 'total_multiplicity' $case.Universe 'occurrences' 'EXPONENT_STORAGE_COST_NOT_MEASURED'
    Add-ExpectedStructuralCost 'LINEAGE' $case.Id 'maximum_multiplicity' 1 'occurrences_per_atom' 'EXPONENT_STORAGE_COST_NOT_MEASURED'
    Add-ExpectedStructuralCost 'LINEAGE' $case.Id 'dense_pev_words' $case.Words 'uint64_words' 'SOFTWARE_LAYOUT_ONLY__NAND_DFF_PPA_NOT_MEASURED'
    Add-ExpectedStructuralCost 'LINEAGE' $case.Id 'explicit_set_canonical_utf8_bytes' $case.SetBytes 'bytes' 'HOST_SERIALIZATION_ONLY__HARDWARE_NOT_MEASURED'
    Add-ExpectedStructuralCost 'LINEAGE' $case.Id 'raw_prime_product_canonical_utf8_bytes' $case.RawBytes 'bytes' 'HOST_SERIALIZATION_ONLY__HARDWARE_NOT_MEASURED'
    Add-ExpectedStructuralCost 'LINEAGE' $case.Id 'sparse_exponent_canonical_utf8_bytes' $case.SparseBytes 'bytes' 'HOST_SERIALIZATION_ONLY__HARDWARE_NOT_MEASURED'
    Add-ExpectedStructuralCost 'LINEAGE' $case.Id 'dense_pev_canonical_utf8_bytes' $case.DenseBytes 'bytes' 'HOST_SERIALIZATION_ONLY__HARDWARE_NOT_MEASURED'
    Add-ExpectedStructuralCost 'PHYSICAL' $case.Id 'materialized_raw_product_datapath_width' $case.Raw 'bits' 'VARIABLE_WIDTH_DATAPATH_REQUIRED__NAND_DFF_PPA_NOT_MEASURED'
    Add-ExpectedStructuralCost 'PHYSICAL' $case.Id 'dense_pev_state_width' $case.Dense 'bits' 'DENSE_STATE_WIDTH_ONLY__NAND_DFF_PPA_NOT_MEASURED'
    Add-ExpectedStructuralCost 'PHYSICAL' $case.Id 'conceptual_boolean_union_intersection_depth' 1 'boolean_levels' 'CONCEPTUAL_PARALLEL_BOOLEAN_DEPTH_1__NAND_DFF_PPA_NOT_MEASURED'
    Add-ExpectedStructuralCost 'PHYSICAL' $case.Id 'sparse_lookup_routing_entries' $case.Universe 'entries' 'SPARSE_LOOKUP_AND_ROUTING_REQUIRED__NAND_DFF_PPA_NOT_MEASURED'
}

Add-ExpectedStructuralCost 'LINEAGE' 'A_B_PLUS_C_D' 'reachable_dag_nodes' 7 'nodes' 'GRAPH_MEMORY_AND_HASH_TRAFFIC__NOT_MEASURED'
Add-ExpectedStructuralCost 'LINEAGE' 'A_B_PLUS_C_D' 'reachable_dag_edges' 6 'edges' 'GRAPH_MEMORY_AND_HASH_TRAFFIC__NOT_MEASURED'
Add-ExpectedStructuralCost 'LINEAGE' 'A_B_PLUS_C_D' 'maximum_depth' 3 'nodes' 'GRAPH_MEMORY_AND_HASH_TRAFFIC__NOT_MEASURED'
Add-ExpectedStructuralCost 'LINEAGE' 'A_B_PLUS_C_D' 'campaign_hash_cons_reuse' 7 'reuse_events' 'HASH_TABLE_AND_MEMORY_COST_NOT_MEASURED'
Add-ExpectedStructuralCost 'LINEAGE' 'A_B_PLUS_C_D' 'active_atoms' 4 'atoms' 'STATE_ACTIVITY_AND_SWITCHING_NOT_MEASURED'
Add-ExpectedStructuralCost 'LINEAGE' 'A_B_PLUS_C_D' 'syntactic_occurrences' 4 'occurrences' 'EXPONENT_STORAGE_COST_NOT_MEASURED'
Add-ExpectedStructuralCost 'LINEAGE' 'A_B_PLUS_C_D' 'reachable_dag_receipt_canonical_utf8_bytes' 2344 'bytes' 'HOST_SERIALIZATION_ONLY__HARDWARE_NOT_MEASURED'
Add-ExpectedStructuralCost 'LINEAGE' 'RETRACTION_BOUNDARY' 'constructed_transform_nodes' 1 'nodes' 'TRANSFORM_IMPLEMENTATION_NOT_MEASURED'
Add-ExpectedStructuralCost 'LINEAGE' 'CANONICAL_DAG_DIAGNOSTIC' 'map_reads' 123 'operations' 'HOST_IMPLEMENTATION_COUNTER__NAND_DFF_PPA_NOT_MEASURED'
Add-ExpectedStructuralCost 'LINEAGE' 'CANONICAL_DAG_DIAGNOSTIC' 'map_writes' 86 'operations' 'HOST_IMPLEMENTATION_COUNTER__NAND_DFF_PPA_NOT_MEASURED'
Add-ExpectedStructuralCost 'LINEAGE' 'CANONICAL_DAG_DIAGNOSTIC' 'semantic_hash_computations' 14 'operations' 'HOST_IMPLEMENTATION_COUNTER__NAND_DFF_PPA_NOT_MEASURED'
Add-ExpectedStructuralCost 'LINEAGE' 'CANONICAL_DAG_DIAGNOSTIC' 'dag_node_visits' 35 'visits_after_memo_miss' 'HOST_IMPLEMENTATION_COUNTER__NAND_DFF_PPA_NOT_MEASURED'
Add-ExpectedStructuralCost 'LINEAGE' 'CANONICAL_DAG_DIAGNOSTIC' 'projection_queries' 4 'queries' 'HOST_IMPLEMENTATION_COUNTER__NAND_DFF_PPA_NOT_MEASURED'
Add-ExpectedStructuralCost 'LINEAGE' 'CANONICAL_DAG_DIAGNOSTIC' 'cache_verification_requests' 2 'requests' 'HOST_IMPLEMENTATION_COUNTER__NAND_DFF_PPA_NOT_MEASURED'
Add-ExpectedStructuralCost 'LINEAGE' 'CANONICAL_DAG_DIAGNOSTIC' 'cache_verification_passes' 2 'passes' 'HOST_IMPLEMENTATION_COUNTER__NAND_DFF_PPA_NOT_MEASURED'
Add-ExpectedStructuralCost 'PHYSICAL' 'A_B_PLUS_C_D' 'dag_hash_memory_traffic_obligation' 1 'declared_obligation' 'DAG_AND_HASH_MEMORY_TRAFFIC_REQUIRED__NAND_DFF_PPA_NOT_MEASURED'
Add-ExpectedStructuralCost 'FUSION' 'ABC_EXACT_CYCLE' 'unique_active_atoms' 3 'atoms' 'DISTRIBUTED_STORAGE_TRANSFER_AND_HARDWARE_COST_NOT_MEASURED'
Add-ExpectedStructuralCost 'FUSION' 'ABC_EXACT_CYCLE' 'exact_payload_dependencies' 3 'payloads' 'DISTRIBUTED_STORAGE_TRANSFER_AND_HARDWARE_COST_NOT_MEASURED'
Add-ExpectedStructuralCost 'FUSION' 'ABC_EXACT_CYCLE' 'canonical_state_receipt_utf8_bytes' 2574 'bytes' 'HOST_SERIALIZATION_ONLY__HARDWARE_NOT_MEASURED'
Add-ExpectedStructuralCost 'FUSION' 'AB_MERGE_BC' 'exact_overlap_payload_dependencies' 1 'payloads' 'DISTRIBUTED_STORAGE_TRANSFER_AND_HARDWARE_COST_NOT_MEASURED'
Add-ExpectedStructuralCost 'AUDIO' 'JUST_FIFTH_3_2_FROM_220_HZ' 'pcm_payload' 16044 'bytes' 'DAC_TRANSDUCER_ROOM_AND_PERCEPTION_NOT_MEASURED'
Add-ExpectedStructuralTextCost 'LINEAGE_LOSS' 'EXPLICIT_SET_SUPPORT' 'declared_information_loss' 'Multiplicity, joint-versus-alternative structure, operation order, payload, authenticity, and authority.' 'loss_contract' 'SEMANTIC_LOSS_ONLY__HARDWARE_NOT_MEASURED'
Add-ExpectedStructuralTextCost 'LINEAGE_LOSS' 'MULTIPLICITY_PROJECTION' 'declared_information_loss' 'Grouping into joint terms, alternative branches, operation order, payload, authenticity, and authority.' 'loss_contract' 'SEMANTIC_LOSS_ONLY__HARDWARE_NOT_MEASURED'
Add-ExpectedStructuralTextCost 'LINEAGE_LOSS' 'RAW_PRIME_PRODUCT_SUPPORT' 'declared_information_loss' 'Joint-versus-alternative structure, operation history, payload, issuer authenticity, and registry-free meaning.' 'loss_contract' 'SEMANTIC_LOSS_ONLY__HARDWARE_NOT_MEASURED'
Add-ExpectedStructuralTextCost 'LINEAGE_LOSS' 'SPARSE_ATOM_EXPONENTS' 'declared_information_loss' 'Joint-versus-alternative grouping, operation history, payload, authenticity, and unregistered atoms.' 'loss_contract' 'SEMANTIC_LOSS_ONLY__HARDWARE_NOT_MEASURED'
Add-ExpectedStructuralTextCost 'LINEAGE_LOSS' 'DENSE_BINARY_PEV_SUPPORT' 'declared_information_loss' 'Multiplicity, joint-versus-alternative structure, operation history, payload, authenticity, and cross-epoch meaning.' 'loss_contract' 'SEMANTIC_LOSS_ONLY__HARDWARE_NOT_MEASURED'
Add-ExpectedStructuralTextCost 'LINEAGE_LOSS' 'PERSISTENT_TYPED_DAG' 'declared_information_loss' 'Issuer authenticity, empirical validity, and external payload availability remain outside the graph receipt.' 'loss_contract' 'SEMANTIC_LOSS_ONLY__HARDWARE_NOT_MEASURED'
$inheritedPaths = @(
    'BUILD_000_REPORT.md',
    'BUILD_001_REPORT.md',
    'BUILD_002_REPORT.md',
    'BUILD_003_REPORT.md',
    'research/build001_experiment_plan.md',
    'research/build002_experiment_plan.md',
    'research/build003_experiment_plan.md',
    'docs/PRIME_RECEIPT_CALCULATOR.md',
    'results/build000',
    'results/build001',
    'results/build002',
    'results/build003'
)
$protectedMutationPaths = @(
    'BUILD_000_REPORT.md',
    'BUILD_001_REPORT.md',
    'BUILD_002_REPORT.md',
    'BUILD_003_REPORT.md',
    'BUILD_004_REPORT.md',
    'results',
    'src',
    'tests',
    'research',
    'docs',
    'scripts',
    'hdl',
    '.github'
)

function Test-PathAtOrBelow {
    param(
        [Parameter(Mandatory)][string]$Candidate,
        [Parameter(Mandatory)][string]$Root
    )

    $comparison = if ($IsWindows) {
        [System.StringComparison]::OrdinalIgnoreCase
    }
    else {
        [System.StringComparison]::Ordinal
    }
    $candidatePath = [System.IO.Path]::GetFullPath($Candidate).TrimEnd(
        [System.IO.Path]::DirectorySeparatorChar,
        [System.IO.Path]::AltDirectorySeparatorChar)
    $rootPath = [System.IO.Path]::GetFullPath($Root).TrimEnd(
        [System.IO.Path]::DirectorySeparatorChar,
        [System.IO.Path]::AltDirectorySeparatorChar)
    return $candidatePath.Equals($rootPath, $comparison) -or
        $candidatePath.StartsWith($rootPath + [System.IO.Path]::DirectorySeparatorChar, $comparison)
}

function Resolve-RepositoryScopedPath {
    param([Parameter(Mandatory)][string]$Path)

    $resolved = if ([System.IO.Path]::IsPathRooted($Path)) {
        [System.IO.Path]::GetFullPath($Path)
    }
    else {
        [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot $Path))
    }

    $comparison = if ($IsWindows) {
        [System.StringComparison]::OrdinalIgnoreCase
    }
    else {
        [System.StringComparison]::Ordinal
    }
    $rootPrefix = $repositoryRoot.TrimEnd(
        [System.IO.Path]::DirectorySeparatorChar,
        [System.IO.Path]::AltDirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
    if (-not $resolved.StartsWith($rootPrefix, $comparison)) {
        throw "Build 004 verifier path must remain below the repository root: $resolved"
    }

    return $resolved
}

function Resolve-ArtifactScopedPath {
    param([Parameter(Mandatory)][string]$Path)

    $resolved = Resolve-RepositoryScopedPath $Path
    foreach ($protectedPath in $protectedMutationPaths) {
        $protected = [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot $protectedPath))
        if (Test-PathAtOrBelow -Candidate $resolved -Root $protected) {
            throw "Build 004 verifier artifact output may not target evidence, results, or source paths: $resolved"
        }
    }

    $comparison = if ($IsWindows) {
        [System.StringComparison]::OrdinalIgnoreCase
    }
    else {
        [System.StringComparison]::Ordinal
    }
    $artifactRoots = @(
        [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot 'artifacts')),
        [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot '.artifacts'))
    )
    $isStrictDescendant = $false
    foreach ($artifactRoot in $artifactRoots) {
        $artifactPrefix = $artifactRoot.TrimEnd(
            [System.IO.Path]::DirectorySeparatorChar,
            [System.IO.Path]::AltDirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
        if ($resolved.StartsWith($artifactPrefix, $comparison)) {
            $isStrictDescendant = $true
            break
        }
    }
    if (-not $isStrictDescendant) {
        throw "Build 004 verifier output must be beneath artifacts/ or .artifacts/: $resolved"
    }

    Assert-NoReparsePointTraversal $resolved
    if ((Test-Path -LiteralPath $resolved) -and -not (Test-Path -LiteralPath $resolved -PathType Container)) {
        throw "Build 004 verifier artifact output is not a directory: $resolved"
    }

    return $resolved
}

function Assert-NoReparsePointTraversal {
    param([Parameter(Mandatory)][string]$Path)

    $current = [System.IO.Path]::GetFullPath($Path)
    while (-not (Test-Path -LiteralPath $current)) {
        $parent = [System.IO.Path]::GetDirectoryName($current)
        if ([string]::IsNullOrEmpty($parent) -or $parent -eq $current) { break }
        $current = $parent
    }
    while (-not [string]::IsNullOrEmpty($current) -and (Test-Path -LiteralPath $current)) {
        $item = Get-Item -LiteralPath $current -Force
        if (($item.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
            throw "Artifact path traverses a symbolic link or junction: $current"
        }
        $parent = [System.IO.Path]::GetDirectoryName($current)
        if ([string]::IsNullOrEmpty($parent) -or $parent -eq $current) { break }
        $current = $parent
    }
}

function Get-InheritedSnapshot {
    $rows = @()
    foreach ($path in $inheritedPaths) {
        $absolute = Join-Path $repositoryRoot $path
        if (Test-Path -LiteralPath $absolute -PathType Leaf) {
            $rows += "$path|$((Get-FileHash -LiteralPath $absolute -Algorithm SHA256).Hash)"
        }
        elseif (Test-Path -LiteralPath $absolute -PathType Container) {
            foreach ($file in Get-ChildItem -LiteralPath $absolute -File -Recurse | Sort-Object FullName) {
                $relative = [System.IO.Path]::GetRelativePath($repositoryRoot, $file.FullName).Replace([System.IO.Path]::DirectorySeparatorChar, '/')
                $rows += "$relative|$((Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash)"
            }
        }
        else {
            throw "Inherited evidence path is missing: $path"
        }
    }
    return @($rows)
}

function Assert-NoUntrackedInheritedEvidence {
    $untracked = @(git ls-files --others --exclude-standard -- $inheritedPaths)
    if ($LASTEXITCODE -ne 0) { throw 'Could not inspect untracked inherited evidence.' }
    if ($untracked.Count -ne 0) {
        throw "Untracked files exist inside inherited evidence: $($untracked -join ', ')"
    }
}

function Assert-Manifest {
    param([Parameter(Mandatory)][string]$Directory)

    $inventoryItems = @(Get-ChildItem -LiteralPath $Directory -Force)
    if (@($inventoryItems | Where-Object { $_.PSIsContainer }).Count -ne 0) {
        throw "Build 004 evidence contains an unexpected directory in $Directory."
    }
    if (@($inventoryItems | Where-Object {
                ($_.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0
            }).Count -ne 0) {
        throw "Build 004 evidence contains a symbolic-link or reparse-point leaf in $Directory."
    }
    $inventory = @($inventoryItems | ForEach-Object Name | Sort-Object)
    if (($inventory -join "`n") -cne (($expectedFiles | Sort-Object) -join "`n")) {
        throw "Build 004 inventory differs in $Directory."
    }

    $manifest = Get-Content -LiteralPath (Join-Path $Directory 'manifest.json') -Raw | ConvertFrom-Json
    if ($manifest.schema -cne 'prime-axiom-build004-manifest-v1' -or
        $manifest.protocolId -cne $protocolId -or
        $manifest.frozenPlanSha256 -cne $planHash -or
        $manifest.baselineCommit -cne $baselineCommit -or
        $manifest.masterSeed -cne $masterSeed -or
        $manifest.runtimeContract -cne 'net8.0' -or
        $manifest.sdkPolicy -cne '8.0.423 with rollForward=latestPatch' -or
        $manifest.canonicalReproductionCommand -cne $canonicalCommand -or
        $manifest.status -cne $partialStatus -or
        $manifest.candidateFrameworkStatus -cne $frameworkStatus -or
        $manifest.externalVerificationRequired -ne $true -or
        [long]$manifest.checks -ne $expectedChecks -or
        [long]$manifest.failures -ne 0 -or
        $manifest.includedWallClockMeasurements -ne $false -or
        $manifest.includedHardwareMeasurements -ne $false -or
        $manifest.claimCeiling -cne $claimCeiling) {
        throw "Build 004 manifest identity, status, or measurement boundary differs in $Directory."
    }
    $familyCaseProperties = @($manifest.familyCases.PSObject.Properties)
    if ($familyCaseProperties.Count -ne $expectedFamilies.Count) {
        throw "Build 004 manifest family-case inventory differs in $Directory."
    }
    foreach ($pair in $expectedFamilies.GetEnumerator()) {
        $matches = @($familyCaseProperties | Where-Object { $_.Name -ceq $pair.Key })
        if ($matches.Count -ne 1 -or [long]$matches[0].Value -ne [long]$pair.Value) {
            throw "Build 004 manifest family-case value differs for $($pair.Key) in $Directory."
        }
    }

    $expectedManifestFiles = @($expectedFiles | Where-Object { $_ -cne 'manifest.json' } | Sort-Object)
    $entries = @($manifest.files)
    $manifestPaths = @($entries | ForEach-Object { [string]$_.path } | Sort-Object)
    if (($manifestPaths -join "`n") -cne ($expectedManifestFiles -join "`n") -or
        (@($manifestPaths | Sort-Object -Unique).Count -ne $manifestPaths.Count)) {
        throw "Build 004 manifest file set differs in $Directory."
    }
    foreach ($entry in $entries) {
        $leaf = [string]$entry.path
        if ([System.IO.Path]::IsPathRooted($leaf) -or
            [System.IO.Path]::GetFileName($leaf) -cne $leaf) {
            throw "Unsafe Build 004 manifest path: $leaf"
        }
        $path = Join-Path $Directory $leaf
        $item = Get-Item -LiteralPath $path -Force
        if ($item.PSIsContainer -or
            ($item.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0 -or
            (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -cne [string]$entry.sha256 -or
            $item.Length -ne [long]$entry.bytes) {
            throw "Build 004 manifest hash or byte count mismatch: $leaf"
        }
    }

    return $manifest
}

function Assert-Receipts {
    param([Parameter(Mandatory)][string]$Directory)

    $correctness = Get-Content -LiteralPath (Join-Path $Directory 'correctness.json') -Raw | ConvertFrom-Json
    if ($correctness.schema -cne 'prime-axiom-build004-correctness-v1' -or
        $correctness.protocolId -cne $protocolId -or
        $correctness.masterSeed -cne $masterSeed -or
        $correctness.status -cne 'BOUNDED_PASS' -or
        [long]$correctness.checks -ne $expectedChecks -or
        [long]$correctness.failures -ne 0 -or
        $correctness.zeroFailure -ne $true -or
        $correctness.exactFrozenCaseCounts -ne $true -or
        $correctness.claimCeiling -cne $claimCeiling) {
        throw "Build 004 correctness receipt differs in $Directory."
    }
    $families = @($correctness.families)
    if ($families.Count -ne $expectedFamilies.Count) {
        throw "Build 004 family count differs in $Directory."
    }
    foreach ($pair in $expectedFamilies.GetEnumerator()) {
        $matches = @($families | Where-Object { $_.family -ceq $pair.Key })
        if ($matches.Count -ne 1 -or
            [long]$matches[0].expectedCases -ne [long]$pair.Value -or
            [long]$matches[0].expectedChecks -ne [long]$expectedFamilyChecks[$pair.Key] -or
            [long]$matches[0].cases -ne [long]$pair.Value -or
            [long]$matches[0].checks -ne [long]$expectedFamilyChecks[$pair.Key] -or
            [long]$matches[0].failureCount -ne 0 -or
            @($matches[0].failureDetails).Count -ne 0 -or
            $matches[0].status -cne 'BOUNDED_PASS') {
            throw "Build 004 family coverage differs for $($pair.Key) in $Directory."
        }
    }

    $coverage = Get-Content -LiteralPath (Join-Path $Directory 'protocol_coverage.json') -Raw | ConvertFrom-Json
    if ($coverage.schema -cne 'prime-axiom-build004-protocol-coverage-v1' -or
        $coverage.protocolId -cne $protocolId -or
        $coverage.frozenPlanSha256 -cne $planHash -or
        $coverage.baselineCommit -cne $baselineCommit -or
        $coverage.status -cne $partialStatus -or
        $coverage.candidateFrameworkStatus -cne $frameworkStatus -or
        $coverage.externalVerificationRequired -ne $true -or
        $coverage.inheritedEvidence -cne 'PROTECTED_AGAINST_MERGED_BUILD003_BASELINE_BY_VERIFY_BUILD004' -or
        $coverage.results.exactPointProbability -cne 'PRIME_COORDINATE_LOCAL_AFTER_LEGENDRE_CONSTRUCTION' -or
        $coverage.results.probabilityEvent -cne 'ADDITIVE_DAG_OR_EXACT_MAGNITUDE_SUM_REQUIRED' -or
        $coverage.results.activeLineage -cne 'PEV_SET_AND_PRIME_PRODUCT_EQUIVALENT_UNDER_VALID_REGISTRY' -or
        $coverage.results.fullDerivation -cne 'TOPOLOGY_PRESERVING_RECEIPT_REQUIRED__PERSISTENT_TYPED_DAG_TESTED' -or
        $coverage.results.etl -cne 'IRREDUCIBLE_RELATIVE_TO_SCALAR_RESULT__POSITIVE_ALGEBRA_BOUND' -or
        $coverage.results.calibration -cne 'RATIO_SCALE_LOCAL__AFFINE_LOG_NONLINEAR_ROUNDED_CORRELATED_OR_UNEVALUATED_UNCERTAINTY_REQUIRE_TYPED_CROSSINGS' -or
        $coverage.results.audio -cne 'EXACT_RATIO_RETAINED_BESIDE_APPROXIMATE_PCM_READOUT' -or
        $coverage.results.accumulator -cne 'NOT_CRYPTOGRAPHIC' -or
        $coverage.results.privacy -cne 'NO_PRIVACY' -or
        [long]$coverage.costLedgers.totalRows -ne 153 -or
        [long]$coverage.costLedgers.abstractStructureRows -ne 58 -or
        [long]$coverage.costLedgers.hostSoftwareDiagnosticRows -ne 78 -or
        [long]$coverage.costLedgers.physicalHardwareImplicationRows -ne 17 -or
        $coverage.costLedgers.hostWallClock -cne 'NOT_MEASURED' -or
        $coverage.costLedgers.hostAllocation -cne 'NOT_MEASURED' -or
        $coverage.costLedgers.physicalHardwareMeasurementStatus -cne 'NOT_MEASURED' -or
        $coverage.costLedgers.crossLedgerRanking -cne 'NOT_PERFORMED' -or
        $coverage.frameworkComparison -cne 'AFTER_THE_FACT_REMOVABLE_LENSES_ONLY__NOT_IMPLEMENTATION_EVIDENCE' -or
        $coverage.claimCeiling -cne $claimCeiling -or
        $coverage.deterministicReplay -cne 'ESTABLISHED_ONLY_BY_VERIFY_BUILD004_TWO_EXTERNAL_INVOCATIONS') {
        throw "Build 004 protocol coverage differs in $Directory."
    }
    $requiredFamilies = @($coverage.requiredFamilies)
    if (($requiredFamilies -join "`n") -cne (@($expectedFamilies.Keys) -join "`n")) {
        throw "Build 004 required-family registry differs in $Directory."
    }
    $coverageFamilies = @($coverage.familyCoverage)
    if ($coverageFamilies.Count -ne $expectedFamilies.Count) {
        throw "Build 004 protocol family count differs in $Directory."
    }
    foreach ($pair in $expectedFamilies.GetEnumerator()) {
        $matches = @($coverageFamilies | Where-Object { $_.family -ceq $pair.Key })
        if ($matches.Count -ne 1 -or
            [long]$matches[0].expectedCases -ne [long]$pair.Value -or
            [long]$matches[0].expectedChecks -ne [long]$expectedFamilyChecks[$pair.Key] -or
            [long]$matches[0].cases -ne [long]$pair.Value -or
            [long]$matches[0].checks -ne [long]$expectedFamilyChecks[$pair.Key] -or
            [long]$matches[0].failureCount -ne 0 -or
            @($matches[0].failureDetails).Count -ne 0 -or
            $matches[0].status -cne 'BOUNDED_PASS') {
            throw "Build 004 protocol family coverage differs for $($pair.Key) in $Directory."
        }
    }

    $combinatorics = Get-Content -LiteralPath (Join-Path $Directory 'combinatorics.json') -Raw | ConvertFrom-Json
    if ($combinatorics.schema -cne 'prime-axiom-build004-combinatorics-v1' -or
        $combinatorics.protocolId -cne $protocolId -or
        $combinatorics.canonicalJsonContract -cne $canonicalJsonContract -or
        $combinatorics.construction -cne 'LEGENDRE_FACTORIAL_VALUATIONS_AND_SIGNED_FACTORIAL_RATIOS' -or
        $combinatorics.projectionContract.name -cne 'EXACT_COMBINATORIAL_NUMERIC_FACTOR_PROJECTION' -or
        [string]::IsNullOrWhiteSpace([string]$combinatorics.projectionContract.preserves) -or
        [string]::IsNullOrWhiteSpace([string]$combinatorics.projectionContract.discards) -or
        [string]::IsNullOrWhiteSpace([string]$combinatorics.projectionContract.replayabilitySemantics) -or
        $combinatorics.projectionInstance.name -cne 'EXACT_COMBINATORIAL_NUMERIC_FACTOR_PROJECTION' -or
        $combinatorics.projectionInstance.basisId -cne 'NUMERIC_PRIME_IDENTITY__ALL_POSITIVE_PRIMES__V1' -or
        $combinatorics.projectionInstance.completeness -cne 'Exact' -or
        $combinatorics.projectionInstance.payloadReplayability -cne 'ReplayableExact' -or
        (@($combinatorics.ordinaryControls) -join "`n") -cne ($expectedOrdinaryControls -join "`n") -or
        $combinatorics.conclusions.point -cne 'SIGNED_EXPONENT_COMPOSITION_WITHOUT_RESULT_FACTORIZATION' -or
        $combinatorics.conclusions.event -cne 'ADDITIVE_DAG_OR_EXACT_MAGNITUDE_SUM_REQUIRED' -or
        $combinatorics.conclusions.comparison -cne 'CORRECTNESS_AND_OPERATION_LEDGER_ONLY__NO_UNIVERSAL_WINNER' -or
        $combinatorics.claimCeiling -cne $claimCeiling) {
        throw "Build 004 combinatorics contract differs in $Directory."
    }
    $central = $combinatorics.examples.centralBinomial
    $hostile = $combinatorics.examples.hostileKOne
    $pointA = $combinatorics.examples.namedPointA
    $pointB = $combinatorics.examples.namedPointB
    $tail = $combinatorics.examples.tailEvent
    $included = @($tail.includedObservations)
    if ([long]$central.n -ne 4096 -or [long]$central.k -ne 2048 -or
        $central.boundary -cne 'PrimeCoordinateLocal' -or
        [long]$hostile.n -ne 100000 -or [long]$hostile.k -ne 1 -or
        [long]$hostile.value -ne 100000 -or $hostile.boundary -cne 'PrimeCoordinateLocal' -or
        [long]$pointA.population -ne 5000 -or [long]$pointA.successStates -ne 1200 -or
        [long]$pointA.draws -ne 900 -or [long]$pointA.observedSuccesses -ne 225 -or
        $pointA.status -cne 'ExactPositive' -or $pointA.boundary -cne 'PrimeCoordinateLocal' -or
        [long]$pointB.population -ne 1000 -or [long]$pointB.successStates -ne 413 -or
        [long]$pointB.draws -ne 271 -or [long]$pointB.observedSuccesses -ne 117 -or
        $pointB.status -cne 'ExactPositive' -or $pointB.boundary -cne 'PrimeCoordinateLocal' -or
        [long]$tail.population -ne 1000 -or [long]$tail.successStates -ne 413 -or
        [long]$tail.draws -ne 271 -or $tail.status -cne 'ExactPositive' -or
        $tail.boundary -cne 'AdditiveMagnitudeRequired' -or $tail.primeCoordinatesAvailable -ne $false -or
        $included.Count -ne 155 -or [long]$included[0] -ne 117 -or [long]$included[-1] -ne 271 -or
        [long]$tail.work.bigIntegerAdditions -ne 155 -or
        [long]$tail.work.exactRationalReductions -ne 83 -or
        [long]$tail.work.additiveNodes -ne 1 -or [long]$tail.work.additiveTerms -ne 155) {
        throw "Build 004 named combinatorial evidence differs in $Directory."
    }

    $lineage = Get-Content -LiteralPath (Join-Path $Directory 'lineage.json') -Raw | ConvertFrom-Json
    $projectionNames = @($lineage.projectionContracts | ForEach-Object { [string]$_.name })
    $projectionInstances = @($lineage.projectionInstances)
    $counterexample = $lineage.structuralCounterexample
    $diagnostic = $lineage.canonicalDiagnostic
    $replayabilityAxis = $lineage.replayabilityAxis
    $evidenceEnvelope = $lineage.evidenceEnvelope
    $evidenceEnvelopeSnapshot = $evidenceEnvelope.snapshot
    $evidenceEnvelopeVerification = $evidenceEnvelope.verification
    if ($lineage.schema -cne 'prime-axiom-build004-lineage-v1' -or
        $lineage.protocolId -cne $protocolId -or
        $lineage.canonicalJsonContract -cne $canonicalJsonContract -or
        $lineage.registry.namespaceId -cne 'build004-lineage' -or
        $lineage.registry.assignmentEpoch -cne 'epoch-1' -or
        [long]$lineage.registry.universeSize -ne 8 -or
        ($projectionNames -join "`n") -cne ($expectedProjectionContracts -join "`n") -or
        @($projectionNames | Sort-Object -Unique).Count -ne $expectedProjectionContracts.Count -or
        @($lineage.projectionContracts | Where-Object {
                [string]::IsNullOrWhiteSpace([string]$_.preserves) -or
                [string]::IsNullOrWhiteSpace([string]$_.discards) -or
                [string]::IsNullOrWhiteSpace([string]$_.replayabilitySemantics)
            }).Count -ne 0 -or
        $projectionInstances.Count -ne $expectedProjectionContracts.Count -or
        (@($projectionInstances | ForEach-Object { [string]$_.representation }) -join "`n") -cne
            ($expectedProjectionContracts -join "`n") -or
        @($projectionInstances | Where-Object {
                $_.registryId -cne $lineage.registry.registryId -or
                $_.completeness -cne 'Exact' -or
                $_.payloadReplayability -cne 'DigestOnly'
            }).Count -ne 0 -or
        $replayabilityAxis.sameAtoms -ne $true -or
        $replayabilityAxis.sameCompleteness -ne $true -or
        $replayabilityAxis.replayable -cne 'ReplayableExact' -or
        $replayabilityAxis.digestOnly -cne 'DigestOnly' -or
        $replayabilityAxis.distinctProjectionIdentity -ne $true -or
        $replayabilityAxis.boundary -cne 'DECLARATION_PRESERVED__NOT_PROOF_OF_PAYLOAD_AVAILABILITY' -or
        $counterexample.sameSupport -ne $true -or $counterexample.sameMultiplicity -ne $true -or
        $counterexample.sameDerivation -ne $false -or
        [string]::IsNullOrWhiteSpace([string]$counterexample.firstRoot) -or
        [string]::IsNullOrWhiteSpace([string]$counterexample.secondRoot) -or
        $counterexample.firstRoot -ceq $counterexample.secondRoot -or
        [long]$counterexample.metrics.nodeCount -ne 7 -or
        [long]$counterexample.metrics.edgeCount -ne 6 -or
        [long]$counterexample.metrics.maximumDepth -ne 3 -or
        $evidenceEnvelopeSnapshot.schema -cne 'prime-axiom-lineage-evidence-envelope-v1' -or
        $evidenceEnvelopeSnapshot.envelopeId -cnotmatch '^[0-9A-F]{64}$' -or
        $evidenceEnvelopeSnapshot.rootNodeId -cne $counterexample.firstRoot -or
        $evidenceEnvelopeSnapshot.registryId -cne $lineage.registry.registryId -or
        $evidenceEnvelopeSnapshot.lineageCompleteness -cne 'Exact' -or
        $evidenceEnvelopeSnapshot.payloadReplayability -cne 'DigestOnly' -or
        $evidenceEnvelopeSnapshot.issuerAuthenticity -cne 'NotProvided' -or
        @($evidenceEnvelopeSnapshot.evidenceReferences).Count -ne 4 -or
        $evidenceEnvelopeVerification.status -cne 'ValidIntegrityOnly' -or
        $evidenceEnvelopeVerification.envelopeHashMatches -ne $true -or
        $evidenceEnvelopeVerification.registryBindingMatches -ne $true -or
        $evidenceEnvelopeVerification.dagReplay.status -cne 'Valid' -or
        $evidenceEnvelopeVerification.isValid -ne $true -or
        $evidenceEnvelope.establishesIssuerAuthentication -ne $false -or
        $evidenceEnvelope.securityBoundary -cne 'CONTENT_ADDRESS_INTEGRITY_ONLY__NO_SIGNATURE_ISSUER_AUTHENTICATION_EMPIRICAL_VALIDITY_OR_AUTHORITY' -or
        $evidenceEnvelope.scope -cne 'BINDS_DAG_ROOT_REGISTRY_AND_EXTERNAL_REFERENCE_DIGESTS__NUMERIC_FACTOR_AND_UNIT_DIMENSION_PROBES_REMAIN_SEPARATE' -or
        $lineage.databaseBoundary -cne 'PROVENANCE_IS_IRREDUCIBLE_RELATIVE_TO_SCALAR_EVALUATION__POSITIVE_ALGEBRA_ONLY' -or
        $lineage.authentication -cne 'NotProvided' -or
        $lineage.claimCeiling -cne $claimCeiling) {
        throw "Build 004 lineage evidence differs in $Directory."
    }
    $expectedEnvelopeReferences = @(
        @('Source', 'fixture:build004/source-catalog-v1', (Get-Utf8Sha256 'BUILD004_SYNTHETIC_SOURCE_CATALOG_V1')),
        @('CalibrationValidity', 'fixture:build004/calibration-validity-v1', (Get-Utf8Sha256 'BUILD004_SYNTHETIC_CALIBRATION_VALIDITY_V1')),
        @('Uncertainty', 'fixture:build004/uncertainty-v1', (Get-Utf8Sha256 'BUILD004_SYNTHETIC_UNCERTAINTY_V1')),
        @('Residual', 'fixture:build004/residual-v1', (Get-Utf8Sha256 'BUILD004_SYNTHETIC_RESIDUAL_V1'))
    )
    for ($index = 0; $index -lt $expectedEnvelopeReferences.Count; $index++) {
        $actual = @($evidenceEnvelopeSnapshot.evidenceReferences)[$index]
        $expected = $expectedEnvelopeReferences[$index]
        if ($actual.kind -cne $expected[0] -or
            $actual.referenceId -cne $expected[1] -or
            $actual.contentSha256 -cne $expected[2]) {
            throw "Build 004 evidence-envelope reference differs at index $index in $Directory."
        }
    }
    if ($diagnostic.scope -cne 'CONSTRUCT_A_B_PLUS_C_D__PROJECT_SUPPORT_AND_MULTIPLICITY__VERIFY_BOTH_CACHES' -or
        $diagnostic.countingContract -cne 'INSTANCE_COUNTERS__MAP_ACCESS_AT_INSTRUMENTED_DAG_DICTIONARY_BOUNDARIES__NODE_VISIT_AFTER_MEMO_MISS__ONE_HASH_PER_SEMANTIC_HASH_CALL' -or
        [long]$diagnostic.diagnostics.mapReads -ne 123 -or
        [long]$diagnostic.diagnostics.mapWrites -ne 86 -or
        [long]$diagnostic.diagnostics.semanticHashComputations -ne 14 -or
        [long]$diagnostic.diagnostics.dagNodeVisits -ne 35 -or
        [long]$diagnostic.diagnostics.projectionQueries -ne 4 -or
        [long]$diagnostic.diagnostics.cacheVerificationRequests -ne 2 -or
        [long]$diagnostic.diagnostics.cacheVerificationPasses -ne 2) {
        throw "Build 004 canonical DAG diagnostics differ in $Directory."
    }

    $fusion = Get-Content -LiteralPath (Join-Path $Directory 'fusion.json') -Raw | ConvertFrom-Json
    if ($fusion.schema -cne 'prime-axiom-build004-fusion-v1' -or
        $fusion.protocolId -cne $protocolId -or
        [string]::IsNullOrWhiteSpace([string]$fusion.exactCycle.abc) -or
        $fusion.exactCycle.abc -cne $fusion.exactCycle.stabilized -or
        [long]$fusion.exactCycle.probabilityOfOne.numerator -ne 1 -or
        [long]$fusion.exactCycle.probabilityOfOne.denominator -ne 3 -or
        $fusion.independentAxes.lineage -cne 'Exact' -or
        $fusion.independentAxes.payload -cne 'ReplayableExact' -or
        $fusion.independentAxes.authenticity -cne 'NotProvided' -or
        $fusion.opaqueUniqueLeaf.status -cne 'Success' -or
        $fusion.opaqueUniqueLeaf.externalOracleMatched -ne $true -or
        $fusion.opaqueUniqueLeaf.payloadReplayability -cne 'DigestOnly' -or
        $fusion.opaqueUniqueLeaf.boundary -cne 'RETAINED_AGGREGATE_PLUS_REPLAYABLE_OVERLAP__EXTERNAL_ORACLE_REQUIRED_FOR_DIGEST_ONLY_UNIQUE_LEAF' -or
        $fusion.securityBoundary -cne 'Semantic SHA-256 IDs provide replayable integrity evidence only. No signature, issuer authentication, privacy, encryption, zero knowledge, or accumulator security is provided.' -or
        $fusion.claimCeiling -cne $claimCeiling) {
        throw "Build 004 fusion contract differs in $Directory."
    }
    $fusionFailures = @($fusion.failureReceipts)
    if ($fusionFailures.Count -ne $expectedFusionFailures.Count) {
        throw "Build 004 fusion failure inventory differs in $Directory."
    }
    foreach ($pair in $expectedFusionFailures.GetEnumerator()) {
        $matches = @($fusionFailures | Where-Object { $_.case -ceq $pair.Key })
        if ($matches.Count -ne 1 -or $matches[0].status -cne $pair.Value) {
            throw "Build 004 fusion failure receipt differs for $($pair.Key) in $Directory."
        }
    }

    $probes = Get-Content -LiteralPath (Join-Path $Directory 'boundary_probes.json') -Raw | ConvertFrom-Json
    $boundaryProjectionNames = @($probes.projectionContracts | ForEach-Object { [string]$_.name })
    $boundaryProjectionInstances = @($probes.projectionInstances)
    if ($probes.schema -cne 'prime-axiom-build004-boundary-probes-v1' -or
        $probes.protocolId -cne $protocolId -or
        ($boundaryProjectionNames -join "`n") -cne ($expectedBoundaryProjectionContracts -join "`n") -or
        @($probes.projectionContracts | Where-Object {
                [string]::IsNullOrWhiteSpace([string]$_.preserves) -or
                [string]::IsNullOrWhiteSpace([string]$_.discards) -or
                [string]::IsNullOrWhiteSpace([string]$_.replayabilitySemantics)
            }).Count -ne 0 -or
        $boundaryProjectionInstances.Count -ne 2 -or
        $boundaryProjectionInstances[0].projection -cne 'SIGNED_PRIME_COORDINATE_NUMERIC_FACTOR_PROJECTION' -or
        $boundaryProjectionInstances[0].basisId -cne 'PROBE_SIGNED_RATIONAL_PRIME_BASIS__ALL_POSITIVE_PRIMES__V1' -or
        $boundaryProjectionInstances[0].completeness -cne 'Exact' -or
        $boundaryProjectionInstances[0].payloadReplayability -cne 'ReplayableExact' -or
        $boundaryProjectionInstances[1].projection -cne 'SIGNED_UNIT_DIMENSION_PROJECTION' -or
        $boundaryProjectionInstances[1].basisId -cne 'PROBE_UNIT_DIMENSION_BASIS__CALLER_DECLARED_CASE_SENSITIVE_AXES__V1' -or
        $boundaryProjectionInstances[1].completeness -cne 'Exact' -or
        $boundaryProjectionInstances[1].payloadReplayability -cne 'MissingDependency' -or
        $probes.claimCeiling -cne $claimCeiling) {
        throw "Build 004 boundary-probe identity differs in $Directory."
    }
    foreach ($pair in $expectedCalibrationDispositions.GetEnumerator()) {
        $receipt = $probes.calibration.($pair.Key)
        if ($null -eq $receipt -or $receipt.disposition -cne $pair.Value) {
            throw "Build 004 calibration disposition differs for $($pair.Key) in $Directory."
        }
    }
    $ratioScale = $probes.calibration.ratioScaleComposition
    $elementaryCharge = $probes.calibration.elementaryChargeDefinitionFixture
    $rounded = $probes.calibration.rounded
    $correlated = $probes.calibration.correlated
    $expired = $probes.calibration.expired
    if ([long]$ratioScale.nominalCoefficient.numerator -ne 10 -or
        [long]$ratioScale.nominalCoefficient.denominator -ne 21 -or
        [long]$elementaryCharge.nominalCoefficient.numerator -ne 801088317 -or
        $elementaryCharge.nominalCoefficient.denominator -cne '5000000000000000000000000000' -or
        @($elementaryCharge.evidence).Count -ne 1 -or
        $elementaryCharge.evidence[0].authentication -cne 'Unauthenticated' -or
        @($rounded.evidence).Count -ne 1 -or
        $rounded.evidence[0].coefficientStatus -cne 'Rounded' -or
        $rounded.evidence[0].uncertaintyStatement -cne 'Synthetic rounded nominal; exact pre-rounding value and rounding error are not supplied.' -or
        @($correlated.evidence).Count -ne 1 -or
        $correlated.evidence[0].uncertaintyKind -cne 'Correlated' -or
        $correlated.evidence[0].uncertaintyStatement -cne 'Synthetic correlated uncertainty; covariance data are required and not supplied by this fixture.' -or
        $null -ne $expired.numericFactors -or
        $expired.dimension.completeness -cne 'Conflict' -or
        @($expired.dimension.axes.PSObject.Properties).Count -ne 0) {
        throw "Build 004 exact calibration fixtures differ in $Directory."
    }
    $waveReceipt = $probes.audio.wave
    if ([long]$waveReceipt.requestedRatio.numerator -ne 3 -or
        [long]$waveReceipt.requestedRatio.denominator -ne 2 -or
        [long]$waveReceipt.baseFrequencyHertz.numerator -ne 220 -or
        [long]$waveReceipt.baseFrequencyHertz.denominator -ne 1 -or
        [long]$waveReceipt.nominalFrequencyHertz.numerator -ne 330 -or
        [long]$waveReceipt.nominalFrequencyHertz.denominator -ne 1 -or
        $waveReceipt.renderedFrequencyHertz -cne '330' -or
        [long]$waveReceipt.policy.sampleRate -ne 8000 -or
        [long]$waveReceipt.policy.sampleCount -ne 8000 -or
        $waveReceipt.policy.phaseRadians -cne '0' -or
        $waveReceipt.policy.peakAmplitude -cne '0.25' -or
        [long]$waveReceipt.policy.linearAttackSamples -ne 80 -or
        [long]$waveReceipt.policy.linearReleaseSamples -ne 80 -or
        $waveReceipt.policy.roundingPolicy -cne 'NearestAwayFromZero' -or
        $waveReceipt.policy.clippingPolicy -cne 'Saturate' -or
        [long]$waveReceipt.clippedSampleCount -ne 0 -or
        [long]$waveReceipt.wavByteLength -ne 16044) {
        throw "Build 004 audio receipt differs in $Directory."
    }
    $accumulator = $probes.structuralAccumulator
    if ((@($accumulator.left.publiclyDecodableSupport) -join "`n") -cne "sensor-a`nsensor-c" -or
        (@($accumulator.right.publiclyDecodableSupport) -join "`n") -cne "sensor-b`nsensor-c" -or
        (@($accumulator.intersection.publiclyDecodableSupport) -join "`n") -cne 'sensor-c' -or
        $accumulator.leakageStatement -cne 'PUBLIC_REGISTRY_PLUS_STRUCTURAL_TOKEN_REVEALS_EACH_REGISTERED_MEMBERSHIP_BY_DIVISIBILITY' -or
        $accumulator.security.cryptographicClassification -cne 'NOT_CRYPTOGRAPHIC' -or
        $accumulator.security.privacyClassification -cne 'NO_PRIVACY' -or
        $accumulator.security.authenticatedCommitment -cne 'NotProvided' -or
        $accumulator.security.membershipProof -cne 'NotProvided' -or
        $accumulator.security.zeroKnowledgeProof -cne 'NotProvided') {
        throw "Build 004 transparent-accumulator boundary differs in $Directory."
    }
    if ($probes.bom.sameValueDifferentLineage -ne $true -or
        (@($probes.bom.sharedComponents) -join "`n") -cne 'shared-fastener' -or
        $probes.bom.integrationBoundary -cne 'TOPOLOGY_PRESERVING_RECEIPT_REQUIRED__PERSISTENT_TYPED_DAG_TESTED' -or
        $probes.frameworkComparison.status -cne 'AFTER_THE_FACT_REMOVABLE_LENSES_ONLY') {
        throw "Build 004 BOM or framework boundary differs in $Directory."
    }

    $wavPath = Join-Path $Directory 'just_intonation_demo.wav'
    $wav = [System.IO.File]::ReadAllBytes($wavPath)
    if ($wav.Length -ne 16044 -or
        [System.Text.Encoding]::ASCII.GetString($wav, 0, 4) -cne 'RIFF' -or
        [System.Text.Encoding]::ASCII.GetString($wav, 8, 4) -cne 'WAVE' -or
        (Get-FileHash -LiteralPath $wavPath -Algorithm SHA256).Hash -cne [string]$waveReceipt.wavSha256) {
        throw "Build 004 deterministic WAV receipt differs in $Directory."
    }

    $costPath = Join-Path $Directory 'structural_costs.csv'
    $costHeader = Get-Content -LiteralPath $costPath -TotalCount 1
    if ($costHeader -cne 'ledger,domain,case_id,metric,value,unit,software_meaning,hardware_implication') {
        throw "Build 004 structural cost header differs in $Directory."
    }
    $costRows = @(Import-Csv -LiteralPath $costPath)
    if ($costRows.Count -ne $expectedStructuralCosts.Count) {
        throw "Build 004 structural cost row count differs in $Directory."
    }
    $seenCostKeys = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
    foreach ($row in $costRows) {
        $key = "$($row.domain)|$($row.case_id)|$($row.metric)"
        if (-not $seenCostKeys.Add($key) -or -not $expectedStructuralCosts.Contains($key)) {
            throw "Build 004 structural cost key is duplicated or unregistered: $key"
        }
        if ($row.ledger -cne [string]$expectedStructuralLedgers[$key]) {
            throw "Build 004 structural cost ledger differs for $key in $Directory."
        }
        $actual = "$($row.value)|$($row.unit)|$($row.hardware_implication)"
        if ($actual -cne [string]$expectedStructuralCosts[$key]) {
            throw "Build 004 structural cost value differs for $key in $Directory."
        }
        if ($row.metric -clike '*canonical*utf8_bytes' -and
            $row.software_meaning -cne "Canonical host receipt under $canonicalJsonContract.") {
            throw "Build 004 canonical-byte counting contract differs for $key in $Directory."
        }
        if ($row.domain -ceq 'LINEAGE_LOSS' -and
            $row.software_meaning -cne 'Semantic information deliberately absent from this projection or receipt.') {
            throw "Build 004 declared-loss counting contract differs for $key in $Directory."
        }
        if ($row.domain -ceq 'COMBINATORICS' -and $row.metric -ceq 'coordinate_additions' -and
            $row.software_meaning -cne 'Nonzero existing-coordinate exponent merges; not every CLR integer addition.') {
            throw "Build 004 coordinate-merge counting contract differs for $key in $Directory."
        }
        if ($row.domain -ceq 'COMBINATORICS' -and $row.metric -ceq 'exact_rational_reductions' -and
            $row.software_meaning -cne 'Nontrivial residual cancellations after exact rational addition; GCD and exact-division counts are separate.') {
            throw "Build 004 rational-reduction counting contract differs for $key in $Directory."
        }
    }
}

function Assert-ByteIdenticalDirectory {
    param(
        [Parameter(Mandatory)][string]$Left,
        [Parameter(Mandatory)][string]$Right
    )

    $leftItems = @(Get-ChildItem -LiteralPath $Left -Force)
    $rightItems = @(Get-ChildItem -LiteralPath $Right -Force)
    if (@($leftItems | Where-Object { $_.PSIsContainer }).Count -ne 0 -or
        @($rightItems | Where-Object { $_.PSIsContainer }).Count -ne 0) {
        throw "Build 004 replay contains an unexpected directory: $Left or $Right"
    }
    if (@($leftItems + $rightItems | Where-Object {
                ($_.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0
            }).Count -ne 0) {
        throw "Build 004 replay contains a symbolic-link or reparse-point leaf: $Left or $Right"
    }
    $leftFiles = @($leftItems | ForEach-Object Name | Sort-Object)
    $rightFiles = @($rightItems | ForEach-Object Name | Sort-Object)
    if (($leftFiles -join "`n") -cne ($rightFiles -join "`n")) {
        throw "Build 004 replay inventories differ: $Left versus $Right"
    }
    foreach ($file in $leftFiles) {
        if ((Get-FileHash -LiteralPath (Join-Path $Left $file) -Algorithm SHA256).Hash -cne
            (Get-FileHash -LiteralPath (Join-Path $Right $file) -Algorithm SHA256).Hash) {
            throw "Build 004 replay bytes differ for $file."
        }
    }
}

Push-Location $repositoryRoot
try {
    $resolvedOutput = Resolve-ArtifactScopedPath $OutputDirectory
    $rawTests = Resolve-ArtifactScopedPath '.artifacts/build004-test-results'
    $replayA = Resolve-ArtifactScopedPath (Join-Path $resolvedOutput 'replay-a')
    $replayB = Resolve-ArtifactScopedPath (Join-Path $resolvedOutput 'replay-b')
    if ((Test-PathAtOrBelow -Candidate $resolvedOutput -Root $rawTests) -or
        (Test-PathAtOrBelow -Candidate $rawTests -Root $resolvedOutput)) {
        throw 'Build 004 verification output may not overlap the reserved raw-test artifact directory.'
    }
    foreach ($directory in @($resolvedOutput, $rawTests, $replayA, $replayB)) {
        New-Item -ItemType Directory -Path $directory -Force | Out-Null
    }
    $verificationPath = Join-Path $resolvedOutput 'verification.json'
    if (Test-Path -LiteralPath $verificationPath) {
        $existingVerification = Get-Item -LiteralPath $verificationPath -Force
        if ($existingVerification.PSIsContainer -or
            ($existingVerification.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
            throw 'Build 004 verification receipt path must be a regular file.'
        }
        [System.IO.File]::Delete($existingVerification.FullName)
        if (Test-Path -LiteralPath $verificationPath) {
            throw 'Build 004 verifier could not invalidate the previous verification receipt.'
        }
    }
    $trxPath = Join-Path $rawTests 'test-results.trx'
    if (Test-Path -LiteralPath $trxPath) {
        $existingTrx = Get-Item -LiteralPath $trxPath -Force
        if ($existingTrx.PSIsContainer -or
            ($existingTrx.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
            throw 'Build 004 raw test receipt path must be a regular file.'
        }
        [System.IO.File]::Delete($existingTrx.FullName)
        if (Test-Path -LiteralPath $trxPath) {
            throw 'Build 004 verifier could not invalidate the previous raw test receipt.'
        }
    }

    Assert-NoUntrackedInheritedEvidence
    $inheritedBefore = @(Get-InheritedSnapshot)
    git merge-base --is-ancestor $baselineCommit HEAD
    if ($LASTEXITCODE -ne 0) {
        throw 'The merged Build 003 baseline is not an ancestor of this checkout.'
    }
    git diff --exit-code $baselineCommit -- $inheritedPaths
    if ($LASTEXITCODE -ne 0) {
        throw 'Inherited Build 000-003 reports, plans, or evidence differ from the merged Build 003 baseline.'
    }
    if ((Get-FileHash -LiteralPath 'research/build004_experiment_plan.md' -Algorithm SHA256).Hash -cne $planHash) {
        throw 'The Build 004 frozen plan hash differs.'
    }

    dotnet restore PrimeAxiom.sln --locked-mode
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    $formatExit = & (Join-Path $PSScriptRoot 'verify-dotnet-format.ps1') -SolutionPath 'PrimeAxiom.sln'
    if ($formatExit -ne 0) { exit $formatExit }
    dotnet build PrimeAxiom.sln --configuration Release --no-restore
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    $dotnetSdkVersion = (& dotnet --version).Trim()
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($dotnetSdkVersion)) {
        throw 'Could not record the active .NET SDK version.'
    }
    $dotnetRuntimeInventory = @(& dotnet --list-runtimes)
    if ($LASTEXITCODE -ne 0 -or $dotnetRuntimeInventory.Count -eq 0) {
        throw 'Could not record the installed .NET runtime inventory.'
    }
    $runtimeConfigPath = Join-Path $repositoryRoot 'src/PrimeAxiom.Cli/bin/Release/net8.0/PrimeAxiom.Cli.runtimeconfig.json'
    $runtimeConfig = Get-Content -LiteralPath $runtimeConfigPath -Raw | ConvertFrom-Json
    if ($runtimeConfig.runtimeOptions.tfm -cne 'net8.0' -or
        $runtimeConfig.runtimeOptions.framework.name -cne 'Microsoft.NETCore.App' -or
        [string]::IsNullOrWhiteSpace([string]$runtimeConfig.runtimeOptions.framework.version)) {
        throw 'The built CLI runtime contract differs from the Build 004 host-software contract.'
    }

    dotnet test PrimeAxiom.sln --configuration Release --no-build --no-restore `
        --logger 'trx;LogFileName=test-results.trx' `
        --results-directory $rawTests
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    [xml]$trx = Get-Content -LiteralPath $trxPath -Raw
    $counters = $trx.SelectSingleNode("//*[local-name()='Counters']")
    if ($null -eq $counters) { throw 'Build 004 TRX receipt has no counters.' }
    $total = [int]$counters.GetAttribute('total')
    $passed = [int]$counters.GetAttribute('passed')
    $failed = [int]$counters.GetAttribute('failed')
    $skipped = [int]$counters.GetAttribute('notExecuted')
    if ($total -le 0 -or $passed -ne $total -or $failed -ne 0 -or $skipped -ne 0) {
        throw 'Build 004 repository tests are not a complete zero-skip pass.'
    }

    dotnet run --project src/PrimeAxiom.Cli --configuration Release --no-build -- `
        experiment-build004 --output $replayA
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    dotnet run --project src/PrimeAxiom.Cli --configuration Release --no-build -- `
        experiment-build004 --output $replayB
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    $committed = [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot 'results/build004'))
    foreach ($directory in @($replayA, $replayB, $committed)) {
        $null = Assert-Manifest $directory
        Assert-Receipts $directory
    }
    Assert-ByteIdenticalDirectory $replayA $replayB
    Assert-ByteIdenticalDirectory $committed $replayA

    foreach ($extension in @('*.json', '*.csv', '*.md')) {
        foreach ($file in Get-ChildItem -LiteralPath $committed -Filter $extension -File) {
            $bytes = [System.IO.File]::ReadAllBytes($file.FullName)
            if ($bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF) {
                throw "Generated Build 004 text has a UTF-8 BOM: $($file.Name)"
            }
            if ([System.IO.File]::ReadAllText($file.FullName).Contains("`r`n", [System.StringComparison]::Ordinal)) {
                throw "Generated Build 004 text is not LF-normalized: $($file.Name)"
            }
        }
    }

    git diff --exit-code $baselineCommit -- $inheritedPaths
    if ($LASTEXITCODE -ne 0) { throw 'Build 004 verification changed inherited evidence.' }
    Assert-NoUntrackedInheritedEvidence
    $inheritedAfter = @(Get-InheritedSnapshot)
    if (($inheritedBefore -join "`n") -cne ($inheritedAfter -join "`n")) {
        throw 'Inherited evidence bytes changed during Build 004 verification.'
    }

    $verification = [ordered]@{
        schema = 'prime-axiom-build004-verification-v1'
        protocolId = $protocolId
        baselineCommit = $baselineCommit
        frozenPlanSha256 = $planHash
        command = '& .\scripts\verify-build004.ps1'
        status = 'PASS'
        frameworkStatus = $frameworkStatus
        tests = [ordered]@{ total = $total; passed = $passed; failed = $failed; skipped = $skipped }
        correctnessChecks = $expectedChecks
        correctnessFailures = 0
        deterministicReplay = $true
        committedManifestSha256 = (Get-FileHash -LiteralPath (Join-Path $committed 'manifest.json') -Algorithm SHA256).Hash
        hostSoftware = [ordered]@{
            dotnetSdkVersion = $dotnetSdkVersion
            cliTargetFramework = [string]$runtimeConfig.runtimeOptions.tfm
            cliRuntimeFramework = [string]$runtimeConfig.runtimeOptions.framework.name
            cliRequestedRuntimeVersion = [string]$runtimeConfig.runtimeOptions.framework.version
            installedDotnetRuntimes = @($dotnetRuntimeInventory)
            verifierFrameworkDescription = [System.Runtime.InteropServices.RuntimeInformation]::FrameworkDescription
            osDescription = [System.Runtime.InteropServices.RuntimeInformation]::OSDescription
            osArchitecture = [System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture.ToString()
            processArchitecture = [System.Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture.ToString()
            powershellVersion = $PSVersionTable.PSVersion.ToString()
            diagnosticScope = 'CANONICAL_DAG_DIAGNOSTIC_COUNTERS_INTERPRETED_UNDER_THIS_HOST_RECEIPT'
        }
        hardware = 'NOT_MEASURED'
        cryptographicSecurity = 'NOT_PROVIDED'
        privacy = 'NO_PRIVACY'
        claimCeiling = 'PASS verifies this checkout and the frozen bounded Build 004 protocol only.'
    }
    [System.IO.File]::WriteAllText(
        $verificationPath,
        ($verification | ConvertTo-Json -Depth 5) + "`n",
        [System.Text.UTF8Encoding]::new($false))
    Write-Host "Build 004 verification passed: $passed/$total tests; 0 skipped; $expectedChecks deterministic checks; two byte-identical replays."
}
finally {
    Pop-Location
}
