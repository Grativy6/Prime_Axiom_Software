[CmdletBinding()]
param(
    [string]$OutputDirectory = 'artifacts/build005-final'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$protocolId = 'PAH-BUILD005-DEMAND-VALUATION-0001'
$baselineCommit = '1fff29e2f1e454921aa51cb4a91bd5b41821ebcc'
$freezeCommit = '3ffb86be198d4b35839dd7faf0ea3619547d23df'
$planHash = '8B76649A4D4E7E60B756BCFB5FDA7954385A10A9E6DDD520C97123B845CE9031'
$partialStatus = 'PARTIAL — FINAL DECISION NOT EARNED'
$claimCeiling = 'Bounded exact semantic, host-software, and declared NAND/DFF evidence under PAH-BUILD005-DEMAND-VALUATION-0001 only; no universal arithmetic, novelty, FPGA/ASIC PPA, physical-energy, fabricated-hardware, or PAL-conformance claim.'
$masterSeed = '5041485742303035'
$canonicalCommand = 'dotnet run --project src/PrimeAxiom.Cli --configuration Release -- experiment-build005 --output results/build005'
$expectedWorkloadRows = 864
$expectedBreakEvenRows = 1134
$expectedStaticRows = 48
$expectedTraceRows = 54
$expectedPolicyRows = 16
$expectedFamilyRows = 18
$expectedRowsPerFamily = 48
$expectedChecks = 1066724
$expectedIndependentChecks = 163360
$expectedWorkloadChecks = 903364
$expectedFiles = @(
    'README.md',
    'attribution.json',
    'break_even.csv',
    'correctness.json',
    'manifest.json',
    'protocol_coverage.json',
    'static_costs.csv',
    'trace_inventory.json',
    'workload_matrix.csv'
)
$expectedFamilies = @(
    'ADDITION_MUTATION',
    'COMPOSITE_CONTROL',
    'DIVISIBILITY_FILTER_PERSISTENT',
    'DIVISIBILITY_FILTER_STREAM',
    'HOSTILE_BOUNDARY_FAILURE',
    'HOSTILE_GENERATION_WRAP',
    'HOSTILE_MUTATE_AFTER_FILL',
    'HOSTILE_PRIME_THRASH',
    'HOSTILE_SLOT_THRASH',
    'HOSTILE_SPECULATION_POISON',
    'MULTIPLICATIVE_DAG',
    'PHASE_SHIFT',
    'PRODUCER_FACTORED',
    'RADIX_V2',
    'RATIONAL_CANCEL',
    'SMOOTH_STRIP',
    'STATIC_REUSE',
    'THRESHOLD_STAIRCASE'
)
$expectedUnmetGates = @(
    'PRE_RESULT_TRACE_DIGEST_REGISTRY',
    'ALL_OUTPUT_OBLIGATIONS',
    'PHASE_AND_TRANSITION_LEDGER',
    'INTEGRATED_PROPAGATION_HARDWARE',
    'POLICY_MATCHED_CONTENT_CACHE_HARDWARE',
    'COMPETENT_CONVENTIONAL_CONTROLS',
    'CAUSAL_PRIME_ATTRIBUTION',
    'FULL_INDEPENDENT_CORRECTNESS_MATRIX',
    'EXTERNAL_DETERMINISTIC_REPLAY'
)
$postVerificationUnmetGates = @(
    $expectedUnmetGates | Where-Object { $_ -cne 'EXTERNAL_DETERMINISTIC_REPLAY' }
)
$inheritedPaths = @(
    'BUILD_000_REPORT.md',
    'BUILD_001_REPORT.md',
    'BUILD_002_REPORT.md',
    'BUILD_003_REPORT.md',
    'BUILD_004_REPORT.md',
    'results/build000',
    'results/build001',
    'results/build002',
    'results/build003',
    'results/build004'
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
        $candidatePath.StartsWith(
            $rootPath + [System.IO.Path]::DirectorySeparatorChar,
            $comparison)
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
            throw "Verifier path traverses a symbolic link or junction: $current"
        }
        $parent = [System.IO.Path]::GetDirectoryName($current)
        if ([string]::IsNullOrEmpty($parent) -or $parent -eq $current) { break }
        $current = $parent
    }
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
        throw "Build 005 verifier path must remain below the repository root: $resolved"
    }
    return $resolved
}

function Resolve-ArtifactScopedPath {
    param([Parameter(Mandatory)][string]$Path)

    $resolved = Resolve-RepositoryScopedPath $Path
    foreach ($protectedPath in $protectedMutationPaths) {
        $protected = [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot $protectedPath))
        if (Test-PathAtOrBelow -Candidate $resolved -Root $protected) {
            throw "Build 005 verifier artifacts may not target evidence, source, or script paths: $resolved"
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
    $strictDescendant = $false
    foreach ($artifactRoot in $artifactRoots) {
        $prefix = $artifactRoot.TrimEnd(
            [System.IO.Path]::DirectorySeparatorChar,
            [System.IO.Path]::AltDirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
        if ($resolved.StartsWith($prefix, $comparison)) {
            $strictDescendant = $true
            break
        }
    }
    if (-not $strictDescendant) {
        throw "Build 005 verifier output must be beneath artifacts/ or .artifacts/: $resolved"
    }

    Assert-NoReparsePointTraversal $resolved
    if ((Test-Path -LiteralPath $resolved) -and
        -not (Test-Path -LiteralPath $resolved -PathType Container)) {
        throw "Build 005 verifier artifact output is not a directory: $resolved"
    }
    return $resolved
}

function Assert-ExactStringSet {
    param(
        [Parameter(Mandatory)][AllowEmptyCollection()][object[]]$Actual,
        [Parameter(Mandatory)][AllowEmptyCollection()][object[]]$Expected,
        [Parameter(Mandatory)][string]$Label
    )

    $actualStrings = @($Actual | ForEach-Object { [string]$_ })
    $expectedStrings = @($Expected | ForEach-Object { [string]$_ })
    $actualSet = [System.Collections.Generic.HashSet[string]]::new(
        [System.StringComparer]::Ordinal)
    foreach ($item in $actualStrings) {
        if (-not $actualSet.Add($item)) {
            throw "$Label contains a duplicate value: $item"
        }
    }
    $expectedSet = [System.Collections.Generic.HashSet[string]]::new(
        [System.StringComparer]::Ordinal)
    foreach ($item in $expectedStrings) {
        if (-not $expectedSet.Add($item)) {
            throw "$Label verifier expectation contains a duplicate value: $item"
        }
    }
    if ($actualSet.Count -ne $expectedSet.Count -or -not $actualSet.SetEquals($expectedSet)) {
        throw "$Label differs. Expected [$($expectedStrings -join ', ')]; observed [$($actualStrings -join ', ')]."
    }
}

function Get-InheritedSnapshot {
    $rows = [System.Collections.Generic.List[string]]::new()
    foreach ($relativePath in $inheritedPaths) {
        $absolutePath = Join-Path $repositoryRoot $relativePath
        if (Test-Path -LiteralPath $absolutePath -PathType Leaf) {
            $hash = (Get-FileHash -LiteralPath $absolutePath -Algorithm SHA256).Hash
            $rows.Add("$relativePath|$hash")
            continue
        }
        if (-not (Test-Path -LiteralPath $absolutePath -PathType Container)) {
            throw "Inherited Build 000-004 evidence is missing: $relativePath"
        }
        foreach ($file in Get-ChildItem -LiteralPath $absolutePath -File -Recurse | Sort-Object FullName) {
            if (($file.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
                throw "Inherited evidence contains a redirected leaf: $($file.FullName)"
            }
            $relative = [System.IO.Path]::GetRelativePath($repositoryRoot, $file.FullName).
                Replace([System.IO.Path]::DirectorySeparatorChar, '/')
            $hash = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash
            $rows.Add("$relative|$hash")
        }
    }
    return @($rows)
}

function Assert-NoUntrackedInheritedEvidence {
    $untracked = @(git ls-files --others --exclude-standard -- $inheritedPaths)
    if ($LASTEXITCODE -ne 0) {
        throw 'Could not inspect untracked inherited Build 000-004 evidence.'
    }
    if ($untracked.Count -ne 0) {
        throw "Untracked files exist inside inherited Build 000-004 evidence: $($untracked -join ', ')"
    }

    $ignored = @(git ls-files --others --ignored --exclude-standard -- $inheritedPaths)
    if ($LASTEXITCODE -ne 0) {
        throw 'Could not inspect ignored inherited Build 000-004 evidence.'
    }
    if ($ignored.Count -ne 0) {
        throw "Ignored files exist inside inherited Build 000-004 evidence: $($ignored -join ', ')"
    }
}

function Assert-GeneratedInventory {
    param([Parameter(Mandatory)][string]$Directory)

    $items = @(Get-ChildItem -LiteralPath $Directory -Force)
    if (@($items | Where-Object { $_.PSIsContainer }).Count -ne 0) {
        throw "Build 005 evidence contains an unexpected directory in $Directory."
    }
    if (@($items | Where-Object {
                ($_.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0
            }).Count -ne 0) {
        throw "Build 005 evidence contains a redirected leaf in $Directory."
    }
    Assert-ExactStringSet `
        -Actual @($items | ForEach-Object Name) `
        -Expected $expectedFiles `
        -Label "Build 005 generated inventory in $Directory"
}

function Assert-Manifest {
    param([Parameter(Mandatory)][string]$Directory)

    Assert-GeneratedInventory $Directory
    $manifestPath = Join-Path $Directory 'manifest.json'
    $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
    if ($manifest.schema -cne 'prime-axiom-build005-manifest-v1' -or
        $manifest.protocolId -cne $protocolId -or
        $manifest.frozenPlanSha256 -cne $planHash -or
        $manifest.baselineCommit -cne $baselineCommit -or
        $manifest.freezeCommit -cne '3ffb86b' -or
        $manifest.generatedStatus -cne $partialStatus -or
        $manifest.candidateTerminalLabel -cne $partialStatus -or
        $manifest.decisionAxesEarned -ne $false -or
        $manifest.implementedTraceCoverageComplete -ne $true -or
        $manifest.completeFrozenCoverage -ne $false -or
        $manifest.command -cne $canonicalCommand -or
        $manifest.selfExcluding -ne $true -or
        [long]$manifest.checks -ne $expectedChecks -or
        [long]$manifest.failures -ne 0 -or
        [string]::IsNullOrWhiteSpace([string]$manifest.runtime) -or
        [string]::IsNullOrWhiteSpace([string]$manifest.platform) -or
        $manifest.claimCeiling -cne $claimCeiling) {
        throw "Build 005 manifest identity, status, or claim boundary differs in $Directory."
    }
    Assert-ExactStringSet -Actual @($manifest.unmetGates) -Expected $expectedUnmetGates `
        -Label "Build 005 manifest unmet gates in $Directory"

    $expectedManifestFiles = @($expectedFiles | Where-Object { $_ -cne 'manifest.json' })
    $entries = @($manifest.entries)
    Assert-ExactStringSet `
        -Actual @($entries | ForEach-Object { [string]$_.path }) `
        -Expected $expectedManifestFiles `
        -Label "Build 005 manifest entries in $Directory"
    foreach ($entry in $entries) {
        $leaf = [string]$entry.path
        if ([System.IO.Path]::IsPathRooted($leaf) -or
            [System.IO.Path]::GetFileName($leaf) -cne $leaf) {
            throw "Unsafe Build 005 manifest path: $leaf"
        }
        $path = Join-Path $Directory $leaf
        $item = Get-Item -LiteralPath $path -Force
        if ($item.PSIsContainer -or
            ($item.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0 -or
            $item.Length -ne [long]$entry.bytes -or
            (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -cne [string]$entry.sha256) {
            throw "Build 005 manifest hash or byte count differs for $leaf in $Directory."
        }
    }
    return $manifest
}

function Assert-Receipts {
    param(
        [Parameter(Mandatory)][string]$Directory,
        [Parameter(Mandatory)]$Manifest
    )

    $coverage = Get-Content -LiteralPath (Join-Path $Directory 'protocol_coverage.json') -Raw |
        ConvertFrom-Json
    if ($coverage.schema -cne 'prime-axiom-build005-protocol-coverage-v1' -or
        $coverage.protocolId -cne $protocolId -or
        $coverage.frozenPlanSha256 -cne $planHash -or
        $coverage.baselineCommit -cne $baselineCommit -or
        $coverage.freezeCommit -cne '3ffb86b' -or
        $coverage.generatedStatus -cne $partialStatus -or
        $coverage.candidateTerminalLabel -cne $partialStatus -or
        $coverage.externalVerificationRequired -ne $true -or
        $coverage.implementedTraceCoverageComplete -ne $true -or
        $coverage.completeFrozenCoverage -ne $false -or
        [int]$coverage.workloadRows -ne $expectedWorkloadRows -or
        [int]$coverage.breakEvenRows -ne $expectedBreakEvenRows -or
        [int]$coverage.staticRows -ne $expectedStaticRows -or
        $coverage.evidence.semantic -cne 'EXACT_BOUNDED' -or
        $coverage.evidence.declaredLogical -cne 'EXPLORATORY_COMPONENT_INVENTORY_NOT_DECISION_ELIGIBLE' -or
        $coverage.evidence.integratedNetlist -ne $false -or
        $coverage.evidence.hdlSynthesis -cne 'NOT_MEASURED' -or
        $coverage.evidence.fpgaPlaceAndRoute -cne 'NOT_MEASURED' -or
        $coverage.evidence.physicalMeasurement -cne 'NOT_MEASURED' -or
        $coverage.deterministicReplay -cne 'ESTABLISHED_ONLY_BY_EXTERNAL_TWO_RUN_VERIFIER' -or
        $coverage.inheritedEvidence -cne 'PROTECTED_ONLY_BY_EXTERNAL_VERIFIER' -or
        $coverage.claimCeiling -cne $claimCeiling) {
        throw "Build 005 protocol coverage differs in $Directory."
    }
    Assert-ExactStringSet -Actual @($coverage.unmetGates) -Expected $expectedUnmetGates `
        -Label "Build 005 coverage unmet gates in $Directory"
    $failedEvidenceGates = @($coverage.evidenceGates | Where-Object { $_.satisfied -eq $false })
    if (@($coverage.evidenceGates).Count -ne 10 -or $failedEvidenceGates.Count -ne 9) {
        throw "Build 005 explicit evidence-gate inventory differs in $Directory."
    }
    Assert-ExactStringSet -Actual @($coverage.widths) -Expected @(8, 16, 32) `
        -Label "Build 005 widths in $Directory"
    Assert-ExactStringSet -Actual @($coverage.cacheSizes) -Expected @(0, 1, 2, 4) `
        -Label "Build 005 cache sizes in $Directory"
    Assert-ExactStringSet -Actual @($coverage.speculationBudgets) -Expected @(1, 4) `
        -Label "Build 005 speculation budgets in $Directory"
    Assert-ExactStringSet `
        -Actual @($coverage.primeCatalog) `
        -Expected @(2, 3, 5, 7, 11, 13, 17, 19, 23, 29, 31) `
        -Label "Build 005 prime catalogue in $Directory"
    Assert-ExactStringSet `
        -Actual @($coverage.compositeControls) `
        -Expected @(4, 6, 9, 10, 15, 21, 25, 27, 33, 35) `
        -Label "Build 005 composite controls in $Directory"
    if (@($coverage.policies).Count -ne $expectedPolicyRows) {
        throw "Build 005 policy count differs in $Directory."
    }
    Assert-ExactStringSet `
        -Actual @($coverage.requiredTraceFamilies) `
        -Expected $expectedFamilies `
        -Label "Build 005 required trace families in $Directory"
    $families = @($coverage.families)
    if ($families.Count -ne $expectedFamilyRows) {
        throw "Build 005 family receipt count differs in $Directory."
    }
    Assert-ExactStringSet `
        -Actual @($families | ForEach-Object { [string]$_.family }) `
        -Expected $expectedFamilies `
        -Label "Build 005 family receipts in $Directory"
    foreach ($family in $families) {
        if ([int]$family.expectedRows -ne $expectedRowsPerFamily -or
            [int]$family.rows -ne $expectedRowsPerFamily -or
            [long]$family.checks -le 0 -or
            [long]$family.failures -ne 0 -or
            $family.status -cne 'IMPLEMENTED_TRACE_PASS') {
            throw "Build 005 family receipt is incomplete or failed for $($family.family) in $Directory."
        }
    }

    $correctness = Get-Content -LiteralPath (Join-Path $Directory 'correctness.json') -Raw |
        ConvertFrom-Json
    if ($correctness.schema -cne 'prime-axiom-build005-correctness-v1' -or
        $correctness.protocolId -cne $protocolId -or
        $correctness.masterSeed -cne $masterSeed -or
        $correctness.implementedStatus -cne 'IMPLEMENTED_TRACE_PASS' -or
        $correctness.frozenDecisionStatus -cne $partialStatus -or
        [long]$correctness.checks -ne [long]$Manifest.checks -or
        [long]$correctness.failures -ne 0 -or
        [long]$correctness.independent.checks -ne $expectedIndependentChecks -or
        [long]$correctness.independent.failures -ne 0 -or
        @($correctness.independent.failureDetails).Count -ne 0 -or
        [long]$correctness.workloadChecks -ne $expectedWorkloadChecks -or
        [long]$correctness.workloadFailures -ne 0 -or
        $correctness.campaignHasNoSkipMechanism -ne $true -or
        $correctness.testAssemblySkippedCount -cne 'ESTABLISHED_ONLY_BY_EXTERNAL_TRX_VERIFIER' -or
        $correctness.claimCeiling -cne $claimCeiling) {
        throw "Build 005 correctness receipt differs in $Directory."
    }

    $attribution = Get-Content -LiteralPath (Join-Path $Directory 'attribution.json') -Raw |
        ConvertFrom-Json
    if ($attribution.generatedStatus -cne $partialStatus -or
        $attribution.candidateTerminalLabel -cne $partialStatus -or
        $attribution.decisionAxesEarned -ne $false -or
        $attribution.searchPolicy -cne 'NOT_EARNED' -or
        $attribution.attribution -cne 'NOT_ESTABLISHED' -or
        $attribution.evidenceBoundary -cne 'SEMANTIC' -or
        $attribution.exploratoryObservedPattern -cne 'EXPLORATORY_GENERIC_REUSE_PATTERN' -or
        $attribution.exploratorySearchObservation -cne 'BLIND_SPECULATION_INCURRED_WASTED_WORK' -or
        $attribution.claimCeiling -cne $claimCeiling) {
        throw "Build 005 attribution receipt differs in $Directory."
    }
    Assert-ExactStringSet -Actual @($attribution.unmetGates) -Expected $expectedUnmetGates `
        -Label "Build 005 attribution unmet gates in $Directory"

    $traceInventory = Get-Content -LiteralPath (Join-Path $Directory 'trace_inventory.json') -Raw |
        ConvertFrom-Json
    $traces = @($traceInventory.traces)
    if ($traceInventory.schema -cne 'prime-axiom-build005-trace-inventory-v1' -or
        $traceInventory.protocolId -cne $protocolId -or
        $traces.Count -ne $expectedTraceRows) {
        throw "Build 005 trace inventory differs in $Directory."
    }
    $traceKeys = [System.Collections.Generic.HashSet[string]]::new(
        [System.StringComparer]::Ordinal)
    foreach ($trace in $traces) {
        $key = "$($trace.width)|$($trace.family)|$($trace.traceId)"
        if (-not $traceKeys.Add($key) -or
            [int]$trace.width -notin @(8, 16, 32) -or
            [string]$trace.family -notin $expectedFamilies -or
            [string]::IsNullOrWhiteSpace([string]$trace.traceId) -or
            ([string]$trace.traceSha256).Length -ne 64 -or
            [int]$trace.eventCount -le 0 -or
            [int]$trace.policyRows -ne $expectedPolicyRows) {
            throw "Build 005 trace inventory contains an invalid or duplicate row in $Directory."
        }
    }

    $workload = @(Import-Csv -LiteralPath (Join-Path $Directory 'workload_matrix.csv'))
    if ($workload.Count -ne $expectedWorkloadRows) {
        throw "Build 005 workload row count differs in $Directory."
    }
    $workloadKeys = [System.Collections.Generic.HashSet[string]]::new(
        [System.StringComparer]::Ordinal)
    foreach ($row in $workload) {
        $key = "$($row.width)|$($row.family)|$($row.trace_id)|$($row.policy)|$($row.cache_capacity)|$($row.speculation_budget)"
        if (-not $workloadKeys.Add($key) -or
            $row.protocol_id -cne $protocolId -or
            [int]$row.width -notin @(8, 16, 32) -or
            [string]$row.family -notin $expectedFamilies -or
            [long]$row.correctness_checks -le 0 -or
            [long]$row.correctness_failures -ne 0 -or
            $row.status -cne 'IMPLEMENTED_TRACE_PASS') {
            throw "Build 005 workload matrix contains an invalid or duplicate row in $Directory."
        }
    }
    foreach ($width in @(8, 16, 32)) {
        if (@($workload | Where-Object { [int]$_.width -eq $width }).Count -ne 288) {
            throw "Build 005 workload width coverage differs for W$width in $Directory."
        }
    }
    foreach ($familyName in $expectedFamilies) {
        if (@($workload | Where-Object { $_.family -ceq $familyName }).Count -ne $expectedRowsPerFamily) {
            throw "Build 005 workload family coverage differs for $familyName in $Directory."
        }
    }

    $breakEven = @(Import-Csv -LiteralPath (Join-Path $Directory 'break_even.csv'))
    if ($breakEven.Count -ne $expectedBreakEvenRows -or
        @($breakEven | Where-Object {
                [int]$_.width -notin @(8, 16, 32) -or
                [string]$_.family -notin $expectedFamilies -or
                [string]::IsNullOrWhiteSpace([string]$_.candidate) -or
                [string]::IsNullOrWhiteSpace([string]$_.baseline) -or
                $_.eligible_for_frozen_decision -cne 'false'
            }).Count -ne 0) {
        throw "Build 005 break-even matrix differs in $Directory."
    }

    $staticCosts = @(Import-Csv -LiteralPath (Join-Path $Directory 'static_costs.csv'))
    if ($staticCosts.Count -ne $expectedStaticRows -or
        @($staticCosts | Where-Object {
                [int]$_.width -notin @(8, 16, 32) -or
                [int]$_.cache_capacity -notin @(0, 1, 2, 4) -or
                $_.evidence_class -cne 'STRUCTURAL_DECLARED_COMPOSITIONAL' -or
                $_.integrated_netlist -cne 'false' -or
                $_.combinational_loop_status -cne 'Acyclic'
            }).Count -ne 0) {
        throw "Build 005 static component matrix differs in $Directory."
    }
    foreach ($width in @(8, 16, 32)) {
        foreach ($capacity in @(0, 1, 2, 4)) {
            if (@($staticCosts | Where-Object {
                        [int]$_.width -eq $width -and [int]$_.cache_capacity -eq $capacity
                    }).Count -ne 4) {
                throw "Build 005 static component coverage differs for W$width K$capacity in $Directory."
            }
        }
    }

    return [pscustomobject]@{
        Coverage = $coverage
        Correctness = $correctness
        Attribution = $attribution
    }
}

function Assert-ByteIdenticalDirectory {
    param(
        [Parameter(Mandatory)][string]$Left,
        [Parameter(Mandatory)][string]$Right
    )

    Assert-GeneratedInventory $Left
    Assert-GeneratedInventory $Right
    foreach ($leaf in $expectedFiles) {
        $leftHash = (Get-FileHash -LiteralPath (Join-Path $Left $leaf) -Algorithm SHA256).Hash
        $rightHash = (Get-FileHash -LiteralPath (Join-Path $Right $leaf) -Algorithm SHA256).Hash
        if ($leftHash -cne $rightHash) {
            throw "Build 005 bytes differ for $leaf between $Left and $Right."
        }
    }
}

function Assert-TextNormalization {
    param([Parameter(Mandatory)][string]$Directory)

    foreach ($leaf in $expectedFiles) {
        $path = Join-Path $Directory $leaf
        $bytes = [System.IO.File]::ReadAllBytes($path)
        if ($bytes.Length -ge 3 -and
            $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF) {
            throw "Generated Build 005 text has a UTF-8 BOM: $leaf"
        }
        if ([System.IO.File]::ReadAllText($path).Contains(
                "`r`n",
                [System.StringComparison]::Ordinal)) {
            throw "Generated Build 005 text is not LF-normalized: $leaf"
        }
    }
}

$temporaryRoot = $null
$locationPushed = $false
try {
    Push-Location $repositoryRoot
    $locationPushed = $true

    $resolvedOutput = Resolve-ArtifactScopedPath $OutputDirectory
    New-Item -ItemType Directory -Path $resolvedOutput -Force | Out-Null
    $verificationPath = Join-Path $resolvedOutput 'verification.json'
    $collidingLeaves = @(Get-ChildItem -LiteralPath $resolvedOutput -Force | Where-Object {
            $_.Name -ieq 'verification.json'
        })
    if ($collidingLeaves.Count -gt 1) {
        throw 'Build 005 verification output contains ambiguous verification.json leaves.'
    }
    if ($collidingLeaves.Count -eq 1) {
        $existing = $collidingLeaves[0]
        if ($existing.PSIsContainer -or
            ($existing.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
            throw 'Build 005 verification receipt path is not a regular file.'
        }
        [System.IO.File]::Delete($existing.FullName)
        if (Test-Path -LiteralPath $verificationPath) {
            throw 'Build 005 verifier could not invalidate the previous PASS receipt.'
        }
    }

    Assert-NoUntrackedInheritedEvidence
    $inheritedBefore = @(Get-InheritedSnapshot)

    git merge-base --is-ancestor $baselineCommit HEAD
    if ($LASTEXITCODE -ne 0) {
        throw 'The merged Build 004 baseline is not an ancestor of this checkout.'
    }
    git merge-base --is-ancestor $freezeCommit HEAD
    if ($LASTEXITCODE -ne 0) {
        throw 'The frozen Build 005 protocol commit is not an ancestor of this checkout.'
    }
    git merge-base --is-ancestor $baselineCommit $freezeCommit
    if ($LASTEXITCODE -ne 0) {
        throw 'The Build 005 protocol freeze is not descended from the merged Build 004 baseline.'
    }
    git diff --exit-code $baselineCommit -- $inheritedPaths
    if ($LASTEXITCODE -ne 0) {
        throw 'Inherited Build 000-004 reports or generated evidence differ from the merged Build 004 baseline.'
    }
    if ((Get-FileHash -LiteralPath 'research/build005_experiment_plan.md' -Algorithm SHA256).Hash -cne
        $planHash) {
        throw 'The Build 005 frozen plan hash differs.'
    }

    dotnet restore PrimeAxiom.sln --locked-mode
    if ($LASTEXITCODE -ne 0) { throw 'Build 005 locked restore failed.' }
    $formatExitCode = & (Join-Path $PSScriptRoot 'verify-dotnet-format.ps1') `
        -SolutionPath 'PrimeAxiom.sln'
    if ($formatExitCode -ne 0) {
        throw "Build 005 format verification failed with exit code $formatExitCode."
    }
    dotnet build PrimeAxiom.sln --configuration Release --no-restore
    if ($LASTEXITCODE -ne 0) { throw 'Build 005 Release build failed.' }

    $temporaryBase = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath())
    $temporaryName = 'prime-axiom-build005-verification-' + [Guid]::NewGuid().ToString('N')
    $temporaryRoot = [System.IO.Path]::GetFullPath((Join-Path $temporaryBase $temporaryName))
    $temporaryParent = [System.IO.Path]::GetDirectoryName($temporaryRoot)
    $normalizedTemporaryBase = $temporaryBase.TrimEnd(
        [System.IO.Path]::DirectorySeparatorChar,
        [System.IO.Path]::AltDirectorySeparatorChar)
    $pathComparison = if ($IsWindows) {
        [System.StringComparison]::OrdinalIgnoreCase
    }
    else {
        [System.StringComparison]::Ordinal
    }
    if (-not [string]::Equals($temporaryParent, $normalizedTemporaryBase, $pathComparison) -or
        -not $temporaryName.StartsWith(
            'prime-axiom-build005-verification-',
            [System.StringComparison]::Ordinal)) {
        throw "Unsafe Build 005 temporary root: $temporaryRoot"
    }
    Assert-NoReparsePointTraversal $temporaryRoot
    $testDirectory = Join-Path $temporaryRoot 'tests'
    $replayA = Join-Path $temporaryRoot 'replay-a'
    $replayB = Join-Path $temporaryRoot 'replay-b'
    foreach ($directory in @($temporaryRoot, $testDirectory, $replayA, $replayB)) {
        New-Item -ItemType Directory -Path $directory -Force | Out-Null
    }

    $trxPath = Join-Path $testDirectory 'test-results.trx'
    dotnet test PrimeAxiom.sln --configuration Release --no-build --no-restore `
        --logger 'trx;LogFileName=test-results.trx' `
        --results-directory $testDirectory
    if ($LASTEXITCODE -ne 0) { throw 'Build 005 full repository test run failed.' }
    [xml]$trx = Get-Content -LiteralPath $trxPath -Raw
    $counters = $trx.SelectSingleNode("//*[local-name()='Counters']")
    if ($null -eq $counters) { throw 'Build 005 TRX receipt has no counters.' }
    $totalTests = [int]$counters.GetAttribute('total')
    $executedTests = [int]$counters.GetAttribute('executed')
    $passedTests = [int]$counters.GetAttribute('passed')
    $failedTests = [int]$counters.GetAttribute('failed')
    $skippedTests = [int]$counters.GetAttribute('notExecuted')
    $testRows = @($trx.SelectNodes("//*[local-name()='UnitTestResult']"))
    $testIds = @($testRows | ForEach-Object { $_.GetAttribute('testId') } | Sort-Object -Unique)
    if ($totalTests -le 0 -or
        $executedTests -ne $totalTests -or
        $passedTests -ne $totalTests -or
        $failedTests -ne 0 -or
        $skippedTests -ne 0 -or
        $testRows.Count -ne $totalTests -or
        $testIds.Count -ne $totalTests) {
        throw 'Build 005 repository tests are not a complete zero-skip pass with unique case identifiers.'
    }

    dotnet run --project src/PrimeAxiom.Cli --configuration Release --no-build -- `
        experiment-build005 --output $replayA
    if ($LASTEXITCODE -ne 0) { throw 'Build 005 replay A failed.' }
    dotnet run --project src/PrimeAxiom.Cli --configuration Release --no-build -- `
        experiment-build005 --output $replayB
    if ($LASTEXITCODE -ne 0) { throw 'Build 005 replay B failed.' }

    $committed = Resolve-RepositoryScopedPath 'results/build005'
    $manifestA = $null
    $receiptsA = $null
    foreach ($directory in @($replayA, $replayB, $committed)) {
        $manifest = Assert-Manifest $directory
        $receipts = Assert-Receipts -Directory $directory -Manifest $manifest
        Assert-TextNormalization $directory
        if ($directory -ceq $replayA) {
            $manifestA = $manifest
            $receiptsA = $receipts
        }
    }
    if ($null -eq $manifestA -or $null -eq $receiptsA) {
        throw 'Build 005 replay receipts were not captured.'
    }
    Assert-ByteIdenticalDirectory $replayA $replayB
    Assert-ByteIdenticalDirectory $committed $replayA

    git diff --exit-code $baselineCommit -- $inheritedPaths
    if ($LASTEXITCODE -ne 0) {
        throw 'Build 005 verification changed inherited Build 000-004 evidence.'
    }
    Assert-NoUntrackedInheritedEvidence
    $inheritedAfter = @(Get-InheritedSnapshot)
    if (($inheritedBefore -join "`n") -cne ($inheritedAfter -join "`n")) {
        throw 'Inherited Build 000-004 evidence bytes changed during Build 005 verification.'
    }

    $dotnetSdkVersion = (& dotnet --version).Trim()
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($dotnetSdkVersion)) {
        throw 'Could not record the active .NET SDK version.'
    }
    $verification = [ordered]@{
        schema = 'prime-axiom-build005-verification-v1'
        protocolId = $protocolId
        baselineCommit = $baselineCommit
        freezeCommit = $freezeCommit
        frozenPlanSha256 = $planHash
        command = '& .\scripts\verify-build005.ps1'
        status = 'PASS'
        terminalDecisionEarned = $false
        frozenDecisionStatus = $partialStatus
        generatedStatus = [string]$manifestA.generatedStatus
        searchPolicy = [string]$receiptsA.Attribution.searchPolicy
        attribution = [string]$receiptsA.Attribution.attribution
        evidenceBoundary = [string]$receiptsA.Attribution.evidenceBoundary
        exploratoryObservedPattern = [string]$receiptsA.Attribution.exploratoryObservedPattern
        exploratorySearchObservation = [string]$receiptsA.Attribution.exploratorySearchObservation
        generatedUnmetGates = @($expectedUnmetGates)
        unmetGatesAfterVerification = @($postVerificationUnmetGates)
        tests = [ordered]@{
            total = $totalTests
            passed = $passedTests
            failed = $failedTests
            skipped = $skippedTests
        }
        correctnessChecks = [long]$manifestA.checks
        correctnessFailures = [long]$manifestA.failures
        independentCorrectnessChecks = $expectedIndependentChecks
        workloadCorrectnessChecks = $expectedWorkloadChecks
        workloadRows = $expectedWorkloadRows
        breakEvenRows = $expectedBreakEvenRows
        staticRows = $expectedStaticRows
        traceRows = $expectedTraceRows
        deterministicReplay = $true
        inheritedEvidenceUnchanged = $true
        committedManifestSha256 = (Get-FileHash `
                -LiteralPath (Join-Path $committed 'manifest.json') `
                -Algorithm SHA256).Hash
        hostSoftware = [ordered]@{
            dotnetSdkVersion = $dotnetSdkVersion
            verifierFrameworkDescription = [System.Runtime.InteropServices.RuntimeInformation]::FrameworkDescription
            osDescription = [System.Runtime.InteropServices.RuntimeInformation]::OSDescription
            osArchitecture = [System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture.ToString()
            processArchitecture = [System.Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture.ToString()
            powershellVersion = $PSVersionTable.PSVersion.ToString()
        }
        hardwareEvidence = 'EXPLORATORY_COMPONENT_INVENTORY_ONLY__NOT_DECISION_ELIGIBLE_OR_PHYSICAL'
        claimCeiling = 'PASS verifies checkout integrity, tests, and deterministic partial receipts only; no Build 005 terminal optimization decision is earned.'
    }
    $verificationBytes = [System.Text.UTF8Encoding]::new($false).GetBytes(
        ($verification | ConvertTo-Json -Depth 6) + "`n")
    $verificationStream = [System.IO.FileStream]::new(
        $verificationPath,
        [System.IO.FileMode]::CreateNew,
        [System.IO.FileAccess]::Write,
        [System.IO.FileShare]::None)
    try {
        $verificationStream.Write($verificationBytes, 0, $verificationBytes.Length)
        $verificationStream.Flush($true)
    }
    finally {
        $verificationStream.Dispose()
    }

    Write-Host "Build 005 verification passed: $passedTests/$totalTests tests; 0 skipped; $($manifestA.checks) deterministic checks; two byte-identical external replays; frozen decision remains PARTIAL."
}
finally {
    if ($locationPushed) {
        Pop-Location
    }
    if ($null -ne $temporaryRoot -and (Test-Path -LiteralPath $temporaryRoot)) {
        $temporaryBase = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath()).TrimEnd(
            [System.IO.Path]::DirectorySeparatorChar,
            [System.IO.Path]::AltDirectorySeparatorChar)
        $resolvedTemporaryRoot = [System.IO.Path]::GetFullPath($temporaryRoot)
        $temporaryLeaf = [System.IO.Path]::GetFileName($resolvedTemporaryRoot)
        $temporaryParent = [System.IO.Path]::GetDirectoryName($resolvedTemporaryRoot)
        $pathComparison = if ($IsWindows) {
            [System.StringComparison]::OrdinalIgnoreCase
        }
        else {
            [System.StringComparison]::Ordinal
        }
        if (-not [string]::Equals($temporaryParent, $temporaryBase, $pathComparison) -or
            -not $temporaryLeaf.StartsWith(
                'prime-axiom-build005-verification-',
                [System.StringComparison]::Ordinal)) {
            throw "Refusing to remove unsafe Build 005 temporary root: $resolvedTemporaryRoot"
        }
        Remove-Item -LiteralPath $resolvedTemporaryRoot -Recurse -Force
    }
}
