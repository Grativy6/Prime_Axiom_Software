[CmdletBinding()]
param(
    [ValidateNotNullOrEmpty()]
    [string]$OutputDirectory = 'artifacts/build003-verification'
)

$ErrorActionPreference = 'Stop'
$baselineCommit = 'a83b660443df489c2ff218887953926a33a84c84'
$protocolId = 'PAS-BUILD003-PRIME-RECEIPT-0001'
$expectedPlanHash = '8893D15E539F3750981F63AE6B6EE26FBC537D356AE7C5ACDA24F881ED766EE5'
$repositoryRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$pathComparison = if ([System.Runtime.InteropServices.RuntimeInformation]::IsOSPlatform(
        [System.Runtime.InteropServices.OSPlatform]::Windows)) {
    [System.StringComparison]::OrdinalIgnoreCase
}
else {
    [System.StringComparison]::Ordinal
}
$expectedGeneratedFiles = @(
    'README.md',
    'calculator_examples.json',
    'arithmetic_comparisons.json',
    'correctness.json',
    'protocol_coverage.json',
    'manifest.json'
)
$expectedManifestFiles = @(
    'README.md',
    'calculator_examples.json',
    'arithmetic_comparisons.json',
    'correctness.json',
    'protocol_coverage.json'
)
$expectedComparisonIds = @(
    'USER_ADD_001',
    'USER_MUL_001',
    'ADD_REORGANIZE_001',
    'ADD_RADIX_BOUNDARY_001',
    'MUL_COLD_PRIMES_001',
    'MUL_FACTOR_RICH_001'
)
$expectedRequiredFamilies = @(
    'EXHAUSTIVE_SIGNED_SMALL_DOMAIN',
    'SEEDED_FACTORED_PRODUCTS',
    'PARTIAL_BUDGET',
    'CANONICAL_CLI_GRAMMAR',
    'FROZEN_ARITHMETIC_COMPARISONS',
    'DETERMINISTIC_REPLAY'
)
$expectedCorrectnessChecks = 52914L
$expectedMultiplicationConclusion = 'LOCAL_EXPONENT_MERGE_AFTER_EXPLICIT_ACQUISITION'
$expectedAdditionConclusion = 'MAGNITUDE_ADD_THEN_FRESH_FACTOR_DISCOVERY'
$expectedLlmConclusion = 'NOT_MEASURED'
$expectedClaimCeiling = 'Bounded functional and deterministic-path evidence only; no hardware advantage, factoring-performance, private reasoning, model understanding, or general LLM accuracy claim.'
$expectedAiBoundary = 'This compares public deterministic arithmetic strategies, not hidden chain of thought, model understanding, or general LLM accuracy.'
$inheritedPaths = @(
    'BUILD_000_REPORT.md',
    'BUILD_001_REPORT.md',
    'BUILD_002_REPORT.md',
    'results/build000',
    'results/build001',
    'results/build002'
)
$protectedMutationPaths = @(
    'BUILD_000_REPORT.md',
    'BUILD_001_REPORT.md',
    'BUILD_002_REPORT.md',
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

    $normalizedCandidate = [System.IO.Path]::GetFullPath($Candidate)
    $normalizedRoot = [System.IO.Path]::GetFullPath($Root).TrimEnd('\', '/')
    if ($normalizedCandidate.Equals($normalizedRoot, $pathComparison)) {
        return $true
    }

    $rootPrefix = $normalizedRoot + [System.IO.Path]::DirectorySeparatorChar
    return $normalizedCandidate.StartsWith($rootPrefix, $pathComparison)
}

function Resolve-RepositoryScopedPath {
    param([Parameter(Mandatory)][string]$Path)

    $candidate = if ([System.IO.Path]::IsPathRooted($Path)) {
        [System.IO.Path]::GetFullPath($Path)
    }
    else {
        [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot $Path))
    }
    $rootPrefix = $repositoryRoot.TrimEnd('\', '/') + [System.IO.Path]::DirectorySeparatorChar
    if (-not $candidate.StartsWith($rootPrefix, $pathComparison)) {
        throw "Build 003 verifier path must remain below the repository root: $candidate"
    }

    return $candidate
}

function Assert-NoReparsePointTraversal {
    param([Parameter(Mandatory)][string]$Path)

    $current = [System.IO.Path]::GetFullPath($Path)
    while (-not (Test-Path -LiteralPath $current)) {
        $parent = [System.IO.Path]::GetDirectoryName($current)
        if ([string]::IsNullOrEmpty($parent) -or $parent -eq $current) {
            break
        }
        $current = $parent
    }

    while (Test-Path -LiteralPath $current) {
        if ($current.Equals($repositoryRoot, $pathComparison)) {
            break
        }

        $item = Get-Item -LiteralPath $current -Force
        if (($item.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
            throw "Build 003 verifier refuses an artifact path that traverses a symbolic link or junction: $current"
        }

        $parent = [System.IO.Path]::GetDirectoryName($current)
        if ([string]::IsNullOrEmpty($parent) -or $parent -eq $current) {
            break
        }
        $current = $parent
    }
}

function Resolve-ArtifactScopedPath {
    param([Parameter(Mandatory)][string]$Path)

    $candidate = Resolve-RepositoryScopedPath $Path
    foreach ($protectedPath in $protectedMutationPaths) {
        $protected = [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot $protectedPath))
        if (Test-PathAtOrBelow -Candidate $candidate -Root $protected) {
            throw "Build 003 verifier artifact output may not target inherited evidence, generated results, or source paths: $candidate"
        }
    }

    $artifactRoots = @(
        [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot 'artifacts')),
        [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot '.artifacts'))
    )
    $isAllowed = $false
    foreach ($artifactRoot in $artifactRoots) {
        $artifactPrefix = $artifactRoot.TrimEnd('\', '/') + [System.IO.Path]::DirectorySeparatorChar
        if ($candidate.StartsWith($artifactPrefix, $pathComparison)) {
            $isAllowed = $true
            break
        }
    }
    if (-not $isAllowed) {
        throw "Build 003 verifier output must be beneath artifacts/ or .artifacts/: $candidate"
    }

    Assert-NoReparsePointTraversal $candidate
    if ((Test-Path -LiteralPath $candidate) -and -not (Test-Path -LiteralPath $candidate -PathType Container)) {
        throw "Build 003 verifier artifact output is not a directory: $candidate"
    }

    return $candidate
}

function ConvertTo-SnapshotToken {
    param([AllowEmptyString()][string]$Value)

    return [System.Convert]::ToBase64String([System.Text.Encoding]::UTF8.GetBytes($Value))
}

function Get-InheritedEvidenceSnapshot {
    $entries = [System.Collections.Generic.List[string]]::new()
    foreach ($relativeRoot in $inheritedPaths) {
        $fullRoot = Join-Path $repositoryRoot $relativeRoot
        if (-not (Test-Path -LiteralPath $fullRoot)) {
            throw "Inherited evidence path is missing: $relativeRoot"
        }

        $rootItem = Get-Item -LiteralPath $fullRoot -Force
        $items = @($rootItem)
        if ($rootItem.PSIsContainer) {
            $items += @(Get-ChildItem -LiteralPath $fullRoot -Force -Recurse)
        }

        foreach ($item in $items) {
            $relative = [System.IO.Path]::GetRelativePath($repositoryRoot, $item.FullName).Replace('\', '/')
            $pathToken = ConvertTo-SnapshotToken $relative
            $linkTarget = ''
            if ($null -ne $item.PSObject.Properties['LinkTarget'] -and $null -ne $item.LinkTarget) {
                $linkTarget = [string]$item.LinkTarget
            }
            $linkToken = ConvertTo-SnapshotToken $linkTarget
            if ($item.PSIsContainer) {
                $entries.Add("D|$pathToken|$linkToken")
            }
            else {
                $hash = (Get-FileHash -LiteralPath $item.FullName -Algorithm SHA256).Hash
                $entries.Add("F|$pathToken|$hash|$linkToken")
            }
        }
    }

    return @($entries | Sort-Object)
}

function Assert-InheritedEvidenceUnchanged {
    param([Parameter(Mandatory)][string[]]$Before)

    $after = @(Get-InheritedEvidenceSnapshot)
    $beforeInventory = $Before -join "`n"
    $afterInventory = $after -join "`n"
    if ($beforeInventory -cne $afterInventory) {
        $difference = @(Compare-Object -ReferenceObject $Before -DifferenceObject $after | Select-Object -First 1)
        $detail = if ($difference.Count -gt 0) { " First inventory difference: $($difference[0].InputObject)" } else { '' }
        throw "Inherited Build 000/001/002 recursive inventory or SHA-256 snapshot changed during verification.$detail"
    }
}

function Assert-NoUntrackedInheritedEvidence {
    $untracked = @(git ls-files --others --exclude-standard -- $inheritedPaths | Where-Object {
            -not [string]::IsNullOrWhiteSpace($_)
        })
    if ($LASTEXITCODE -ne 0) {
        throw 'Could not enumerate untracked files beneath inherited Build 000/001/002 evidence paths.'
    }
    if ($untracked.Count -ne 0) {
        throw "Inherited Build 000/001/002 evidence contains an untracked path: $($untracked[0])"
    }

    $ignored = @(git ls-files --others --ignored --exclude-standard -- $inheritedPaths | Where-Object {
            -not [string]::IsNullOrWhiteSpace($_)
        })
    if ($LASTEXITCODE -ne 0) {
        throw 'Could not enumerate ignored files beneath inherited Build 000/001/002 evidence paths.'
    }
    if ($ignored.Count -ne 0) {
        throw "Inherited Build 000/001/002 evidence contains an ignored extra path: $($ignored[0])"
    }
}

function Assert-ExactUniqueStringSet {
    param(
        [AllowEmptyCollection()][object[]]$Actual,
        [Parameter(Mandatory)][string[]]$Expected,
        [Parameter(Mandatory)][string]$Label
    )

    $actualValues = @($Actual)
    if ($actualValues.Count -ne $Expected.Count) {
        throw "$Label count mismatch: expected $($Expected.Count), found $($actualValues.Count)."
    }

    $actualSet = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
    foreach ($value in $actualValues) {
        if ($null -eq $value -or $value -isnot [string]) {
            throw "$Label contains a missing or non-string value."
        }
        if (-not $actualSet.Add([string]$value)) {
            throw "$Label contains a duplicate value: $value"
        }
    }
    foreach ($expectedValue in $Expected) {
        if (-not $actualSet.Contains($expectedValue)) {
            throw "$Label is missing the frozen value: $expectedValue"
        }
    }
}

function Assert-ByteIdenticalDirectory {
    param(
        [Parameter(Mandatory)][string]$Expected,
        [Parameter(Mandatory)][string]$Actual
    )

    $expectedFiles = @(Get-ChildItem -LiteralPath $Expected -File | Sort-Object Name)
    $actualFiles = @(Get-ChildItem -LiteralPath $Actual -File | Sort-Object Name)
    $expectedInventory = @($expectedFiles.Name) -join "`n"
    $actualInventory = @($actualFiles.Name) -join "`n"
    if ($expectedInventory -ne $actualInventory) {
        throw "Generated file inventory differs between '$Expected' and '$Actual'."
    }

    foreach ($file in $expectedFiles) {
        $other = Join-Path $Actual $file.Name
        $expectedHash = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash
        $actualHash = (Get-FileHash -LiteralPath $other -Algorithm SHA256).Hash
        if ($expectedHash -ne $actualHash) {
            throw "Generated bytes differ for $($file.Name)."
        }
    }
}

function Assert-Manifest {
    param([Parameter(Mandatory)][string]$Directory)

    $directoryItems = @(Get-ChildItem -LiteralPath $Directory -Force)
    if (@($directoryItems | Where-Object { $_.PSIsContainer }).Count -ne 0) {
        throw "Build 003 generated directory contains an unexpected subdirectory: $Directory"
    }
    Assert-ExactUniqueStringSet `
        -Actual @($directoryItems | ForEach-Object { $_.Name }) `
        -Expected $expectedGeneratedFiles `
        -Label 'Build 003 generated directory inventory'

    $manifestPath = Join-Path $Directory 'manifest.json'
    $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
    if ($manifest.schema -cne 'prime-axiom-build003-manifest-v1') {
        throw 'Build 003 manifest schema mismatch.'
    }
    if ($manifest.protocolId -cne $protocolId) {
        throw 'Build 003 manifest protocol identifier mismatch.'
    }
    if ($manifest.frozenPlanSha256 -cne $expectedPlanHash) {
        throw 'Build 003 manifest frozen-plan hash mismatch.'
    }
    if ($manifest.baselineCommit -cne $baselineCommit) {
        throw 'Build 003 manifest baseline commit mismatch.'
    }
    if ($manifest.status -cne 'BOUNDED_TOOL_PATH_VALIDATED') {
        throw 'Build 003 manifest did not earn the bounded validated status.'
    }
    if ($manifest.runtimeContract -cne 'net8.0' -or
        $manifest.sdkPolicy -cne '8.0.423 with rollForward=latestPatch' -or
        $null -ne $manifest.PSObject.Properties['framework']) {
        throw 'Build 003 manifest runtime contract is missing, incorrect, or contaminated by an observed patch runtime.'
    }
    if ($manifest.masterSeed -cne '5041534230303033' -or
        $manifest.defaultPolicy.maxOddCandidates -cne '1000000' -or
        $manifest.cliMaximumDecimalDigits -cne '4096' -or
        $manifest.canonicalReproductionCommand -cne
            'dotnet run --project src/PrimeAxiom.Cli --configuration Release -- experiment-build003 --output results/build003' -or
        $manifest.includedWallClockMeasurements -ne $false) {
        throw 'Build 003 manifest seed, default policy, CLI limit, reproduction command, or wall-clock boundary mismatch.'
    }
    if ($manifest.claimCeiling -cne $expectedClaimCeiling) {
        throw 'Build 003 manifest claim ceiling differs from the frozen bounded claim.'
    }
    if ([long]$manifest.correctnessChecks -ne $expectedCorrectnessChecks -or
        [int]$manifest.correctnessFailures -ne 0) {
        throw 'Build 003 manifest does not record exactly 52,914 correctness checks with zero failures.'
    }
    if ([int]$manifest.frozenComparisonRows -ne $expectedComparisonIds.Count) {
        throw 'Build 003 manifest does not record exactly six frozen comparison rows.'
    }
    Assert-ExactUniqueStringSet `
        -Actual @($manifest.frozenComparisonIds) `
        -Expected $expectedComparisonIds `
        -Label 'Build 003 manifest frozen comparison identifiers'
    Assert-ExactUniqueStringSet `
        -Actual @($manifest.requiredFamilies) `
        -Expected $expectedRequiredFamilies `
        -Label 'Build 003 manifest required protocol families'

    $manifestFiles = @($manifest.files)
    if ($manifestFiles.Count -ne $expectedManifestFiles.Count) {
        throw 'Build 003 manifest must list exactly five generated leaf files.'
    }
    $manifestPaths = @($manifestFiles | ForEach-Object { $_.path })
    Assert-ExactUniqueStringSet `
        -Actual $manifestPaths `
        -Expected $expectedManifestFiles `
        -Label 'Build 003 manifest file paths'

    foreach ($file in $manifestFiles) {
        $filePath = [string]$file.path
        if ([System.IO.Path]::IsPathRooted($filePath) -or
            $filePath.Contains('/') -or
            $filePath.Contains('\') -or
            [System.IO.Path]::GetFileName($filePath) -cne $filePath) {
            throw "Build 003 manifest path is not a safe leaf name: $filePath"
        }

        $candidate = Join-Path $Directory $filePath
        if (-not (Test-Path -LiteralPath $candidate -PathType Leaf)) {
            throw "Manifest file is missing: $filePath"
        }
        $actualHash = (Get-FileHash -LiteralPath $candidate -Algorithm SHA256).Hash
        if ($actualHash -cne [string]$file.sha256) {
            throw "Manifest hash mismatch: $filePath"
        }
    }

    return $manifest
}

function Assert-CorrectnessReceipt {
    param([Parameter(Mandatory)][string]$Directory)

    $correctness = Get-Content -LiteralPath (Join-Path $Directory 'correctness.json') -Raw | ConvertFrom-Json
    if ($correctness.schema -cne 'prime-axiom-build003-correctness-v1' -or
        $correctness.protocolId -cne $protocolId -or
        [long]$correctness.checks -ne $expectedCorrectnessChecks -or
        $correctness.status -cne 'BOUNDED_PASS' -or
        @($correctness.failures).Count -ne 0) {
        throw 'Build 003 correctness receipt is not the frozen 52,914-check zero-failure pass.'
    }
    if ($correctness.claimCeiling -cne $expectedClaimCeiling) {
        throw 'Build 003 correctness receipt claim ceiling differs from the frozen bounded claim.'
    }
    $expectedNamedCases = @(
        'zero',
        'positive-unit',
        'negative-unit',
        'negative-prime',
        'repeated-power',
        'square',
        'semiprime',
        'positive-prime'
    )
    $expectedPartialBudgets = @('0', '1', '3')
    if ($correctness.domains.exhaustiveSignedMinimum -cne '-4096' -or
        $correctness.domains.exhaustiveSignedMaximum -cne '4096' -or
        $correctness.domains.randomizedFactoredProducts -cne '5000' -or
        $correctness.domains.calculatorExamples -cne '6' -or
        $correctness.domains.frozenComparisons -cne '6' -or
        $correctness.domains.cliMaximumDecimalDigits -cne '4096' -or
        (@($correctness.domains.partialBudgets) -join "`n") -cne ($expectedPartialBudgets -join "`n") -or
        (@($correctness.domains.namedCases) -join "`n") -cne ($expectedNamedCases -join "`n")) {
        throw 'Build 003 correctness receipt domain declaration differs from the frozen bounded campaign.'
    }
}

function Assert-ArithmeticComparisonReceipt {
    param([Parameter(Mandatory)][string]$Directory)

    $comparisons = Get-Content -LiteralPath (Join-Path $Directory 'arithmetic_comparisons.json') -Raw | ConvertFrom-Json
    if ($comparisons.schema -cne 'prime-axiom-build003-arithmetic-comparisons-v1' -or
        $comparisons.protocolId -cne $protocolId -or
        $comparisons.outputContract -cne 'MAGNITUDE_AND_RECEIPT' -or
        $comparisons.claimCeiling -cne $expectedClaimCeiling) {
        throw 'Build 003 arithmetic comparison protocol identifier mismatch.'
    }
    $rows = @($comparisons.comparisons)
    if ($rows.Count -ne $expectedComparisonIds.Count) {
        throw 'Build 003 arithmetic comparison receipt does not contain exactly six rows.'
    }
    Assert-ExactUniqueStringSet `
        -Actual @($rows | ForEach-Object { $_.id }) `
        -Expected $expectedComparisonIds `
        -Label 'Build 003 arithmetic comparison identifiers'
    foreach ($row in $rows) {
        if ($row.protocolId -cne $protocolId -or
            $row.outputObligation -cne 'MAGNITUDE_AND_RECEIPT' -or
            $row.aiComparisonBoundary -cne $expectedAiBoundary) {
            throw "Build 003 arithmetic row has the wrong protocol, output contract, or AI comparison boundary: $($row.id)"
        }
    }
    if ($comparisons.conclusions.multiplication -cne $expectedMultiplicationConclusion -or
        $comparisons.conclusions.addition -cne $expectedAdditionConclusion -or
        $comparisons.conclusions.generalLlmImprovement -cne $expectedLlmConclusion) {
        throw 'Build 003 arithmetic comparison conclusions differ from the frozen conclusions.'
    }
}

function Assert-ProtocolCoverage {
    param([Parameter(Mandatory)][string]$Directory)

    $coverage = Get-Content -LiteralPath (Join-Path $Directory 'protocol_coverage.json') -Raw | ConvertFrom-Json
    if ($coverage.schema -cne 'prime-axiom-build003-protocol-coverage-v1' -or
        $coverage.protocolId -cne $protocolId -or
        $coverage.frozenPlanSha256 -cne $expectedPlanHash -or
        $coverage.baselineCommit -cne $baselineCommit -or
        $coverage.status -cne 'BOUNDED_TOOL_PATH_VALIDATED' -or
        $coverage.hardwareClaim -cne 'NOT_APPLICABLE' -or
        $coverage.claimCeiling -cne $expectedClaimCeiling) {
        throw 'Build 003 protocol coverage identity, plan, baseline, or status mismatch.'
    }
    if ([long]$coverage.correctnessChecks -ne $expectedCorrectnessChecks -or
        [int]$coverage.correctnessFailures -ne 0 -or
        [int]$coverage.frozenComparisonRows -ne $expectedComparisonIds.Count -or
        $coverage.exactFrozenComparisonSet -ne $true) {
        throw 'Build 003 protocol coverage does not record the exact frozen completion counts.'
    }
    if ($coverage.exactFrozenComparisonSet -ne $true) {
        throw 'Build 003 protocol coverage does not affirm the exact ordered frozen comparison set.'
    }
    Assert-ExactUniqueStringSet `
        -Actual @($coverage.requiredFamilies) `
        -Expected $expectedRequiredFamilies `
        -Label 'Build 003 required protocol families'
    Assert-ExactUniqueStringSet `
        -Actual @($coverage.frozenComparisonIds) `
        -Expected $expectedComparisonIds `
        -Label 'Build 003 protocol coverage comparison identifiers'
    if ($coverage.multiplication -cne $expectedMultiplicationConclusion -or
        $coverage.addition -cne $expectedAdditionConclusion -or
        $coverage.generalLlmImprovement -cne $expectedLlmConclusion) {
        throw 'Build 003 protocol coverage conclusions differ from the frozen conclusions.'
    }
    if ($coverage.requiredFamilyEvidence.deterministicReplay -cne
        'ESTABLISHED_BY_VERIFY_BUILD003_NOT_BY_A_SINGLE_GENERATOR_INVOCATION') {
        throw 'Build 003 protocol coverage misstates the deterministic replay evidence boundary.'
    }
}

Push-Location $repositoryRoot
try {
    $resolvedOutput = Resolve-ArtifactScopedPath $OutputDirectory
    $rawTestDirectory = Resolve-ArtifactScopedPath '.artifacts/build003-test-results'
    $replayA = Resolve-ArtifactScopedPath (Join-Path $resolvedOutput 'replay-a')
    $replayB = Resolve-ArtifactScopedPath (Join-Path $resolvedOutput 'replay-b')
    if ((Test-PathAtOrBelow -Candidate $resolvedOutput -Root $rawTestDirectory) -or
        (Test-PathAtOrBelow -Candidate $rawTestDirectory -Root $resolvedOutput)) {
        throw 'Build 003 verification output may not overlap the reserved raw-test artifact directory.'
    }

    New-Item -ItemType Directory -Path $resolvedOutput -Force | Out-Null
    $verificationPath = Join-Path $resolvedOutput 'verification.json'
    $existingVerificationLeaves = @(Get-ChildItem -LiteralPath $resolvedOutput -Force | Where-Object {
            $_.Name -ceq 'verification.json'
        })
    if ($existingVerificationLeaves.Count -gt 1) {
        throw 'Build 003 verification output contains ambiguous verification.json leaves.'
    }
    if ($existingVerificationLeaves.Count -eq 1) {
        $existingVerificationLeaf = $existingVerificationLeaves[0]
        if ($existingVerificationLeaf.PSIsContainer) {
            throw 'Build 003 verification receipt path is an existing directory; no PASS receipt can be written.'
        }
        $wasReparsePoint = ($existingVerificationLeaf.Attributes -band
            [System.IO.FileAttributes]::ReparsePoint) -ne 0
        [System.IO.File]::Delete($existingVerificationLeaf.FullName)
        if (Test-Path -LiteralPath $verificationPath) {
            throw 'Build 003 verifier could not invalidate the previous verification receipt.'
        }
        if ($wasReparsePoint) {
            throw 'Build 003 verifier removed a redirected verification receipt leaf; rerun with a regular artifact path.'
        }
    }

    Assert-NoUntrackedInheritedEvidence
    $inheritedSnapshotBefore = @(Get-InheritedEvidenceSnapshot)

    git diff --exit-code $baselineCommit -- $inheritedPaths
    if ($LASTEXITCODE -ne 0) {
        throw 'Inherited Build 000/001/002 reports or generated evidence differ from the merged Build 002 baseline.'
    }

    $actualPlanHash = (Get-FileHash -LiteralPath 'research/build003_experiment_plan.md' -Algorithm SHA256).Hash
    if ($actualPlanHash -ne $expectedPlanHash) {
        throw 'research/build003_experiment_plan.md no longer matches the frozen protocol hash.'
    }

    dotnet restore PrimeAxiom.sln --locked-mode
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    $formatExitCode = & (Join-Path $PSScriptRoot 'verify-dotnet-format.ps1') -SolutionPath 'PrimeAxiom.sln'
    if ($formatExitCode -ne 0) { exit $formatExitCode }

    dotnet build PrimeAxiom.sln --configuration Release --no-restore
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    foreach ($directory in @($rawTestDirectory, $replayA, $replayB)) {
        Assert-NoReparsePointTraversal $directory
        if (Test-Path -LiteralPath $directory) {
            Remove-Item -LiteralPath $directory -Recurse -Force
        }
        New-Item -ItemType Directory -Path $directory -Force | Out-Null
    }

    $rawTestReceipt = Join-Path $rawTestDirectory 'test-results.trx'
    dotnet test PrimeAxiom.sln --configuration Release --no-build --no-restore `
        --logger 'trx;LogFileName=test-results.trx' `
        --results-directory $rawTestDirectory
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    [xml]$testReceipt = Get-Content -LiteralPath $rawTestReceipt -Raw
    $counters = $testReceipt.SelectSingleNode("//*[local-name()='Counters']")
    if ($null -eq $counters) {
        throw 'The Build 003 TRX receipt did not contain test counters.'
    }
    $totalTests = [int]$counters.GetAttribute('total')
    $executedTests = [int]$counters.GetAttribute('executed')
    $passedTests = [int]$counters.GetAttribute('passed')
    $failedTests = [int]$counters.GetAttribute('failed')
    $skippedTests = [int]$counters.GetAttribute('notExecuted')
    $testRows = @($testReceipt.SelectNodes("//*[local-name()='UnitTestResult']"))
    $testIds = @($testRows | ForEach-Object { $_.GetAttribute('testId') } | Sort-Object -Unique)
    if ($totalTests -le 0 -or
        $executedTests -ne $totalTests -or
        $passedTests -ne $totalTests -or
        $failedTests -ne 0 -or
        $skippedTests -ne 0 -or
        $testRows.Count -ne $totalTests -or
        $testIds.Count -ne $totalTests) {
        throw 'The Build 003 test receipt is not a complete zero-skip pass with unique case identifiers.'
    }

    dotnet run --project src/PrimeAxiom.Cli --configuration Release --no-build -- `
        experiment-build003 --output $replayA
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    dotnet run --project src/PrimeAxiom.Cli --configuration Release --no-build -- `
        experiment-build003 --output $replayB
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    $committedResults = Resolve-RepositoryScopedPath 'results/build003'
    $validatedDirectories = @($replayA, $replayB, $committedResults)
    $manifestA = $null
    foreach ($directory in $validatedDirectories) {
        $manifest = Assert-Manifest $directory
        Assert-CorrectnessReceipt $directory
        Assert-ArithmeticComparisonReceipt $directory
        Assert-ProtocolCoverage $directory
        if ($directory -eq $replayA) {
            $manifestA = $manifest
        }
    }
    if ($null -eq $manifestA) {
        throw 'Build 003 replay manifest was not captured.'
    }
    Assert-ByteIdenticalDirectory $replayA $replayB
    Assert-ByteIdenticalDirectory $committedResults $replayA

    foreach ($file in Get-ChildItem -LiteralPath $committedResults -File) {
        $bytes = [System.IO.File]::ReadAllBytes($file.FullName)
        if ($bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF) {
            throw "Generated file has a UTF-8 BOM: $($file.Name)"
        }
        if ([System.IO.File]::ReadAllText($file.FullName).Contains("`r`n", [System.StringComparison]::Ordinal)) {
            throw "Generated file is not LF-normalized: $($file.Name)"
        }
    }

    git diff --exit-code $baselineCommit -- $inheritedPaths
    if ($LASTEXITCODE -ne 0) {
        throw 'Build 003 verification changed inherited evidence.'
    }
    Assert-NoUntrackedInheritedEvidence
    Assert-InheritedEvidenceUnchanged -Before $inheritedSnapshotBefore

    $verification = [ordered]@{
        Schema = 'prime-axiom-build003-verification-v1'
        ProtocolId = $protocolId
        BaselineCommit = $baselineCommit
        FrozenPlanSha256 = $expectedPlanHash
        Command = '& .\scripts\verify-build003.ps1'
        Status = 'PASS'
        FrameworkStatus = $manifestA.status
        Tests = [ordered]@{
            Total = $totalTests
            Passed = $passedTests
            Failed = $failedTests
            Skipped = $skippedTests
        }
        CorrectnessChecks = [long]$manifestA.correctnessChecks
        CorrectnessFailures = [int]$manifestA.correctnessFailures
        FrozenComparisonRows = [int]$manifestA.frozenComparisonRows
        FrozenComparisonIds = @($manifestA.frozenComparisonIds)
        DeterministicReplay = $true
        CommittedManifestSha256 = (Get-FileHash -LiteralPath (Join-Path $committedResults 'manifest.json') -Algorithm SHA256).Hash
        ClaimCeiling = 'PASS verifies this checkout and bounded Build 003 protocol only; it is not a hardware or LLM-cognition result.'
    }
    $verificationBytes = [System.Text.UTF8Encoding]::new($false).GetBytes(
        ($verification | ConvertTo-Json -Depth 5) + "`n")
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

    Write-Host "Build 003 verification passed: $passedTests/$totalTests tests; 0 skipped; $($manifestA.correctnessChecks) deterministic checks; $($manifestA.frozenComparisonRows) comparison rows; replay byte-identical."
}
finally {
    Pop-Location
}
