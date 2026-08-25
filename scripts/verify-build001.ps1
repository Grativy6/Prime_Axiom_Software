[CmdletBinding()]
param(
    [switch]$SkipBenchmarks,
    [ValidateNotNullOrEmpty()]
    [string]$OutputDirectory = 'results/build001'
)

$ErrorActionPreference = 'Stop'
$baselineCommit = '7792b8b2a83c95693a6db48a0ed4b153bb0808f4'

git diff --exit-code $baselineCommit -- BUILD_000_REPORT.md results/build000
if ($LASTEXITCODE -ne 0) {
    throw 'Build 000 report or checked evidence differs from the pinned baseline.'
}

dotnet restore PrimeAxiom.sln --locked-mode
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

dotnet format PrimeAxiom.sln --verify-no-changes --no-restore
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

dotnet build PrimeAxiom.sln --configuration Release --no-restore
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$rawTestDirectory = '.artifacts/build001-test-results'
$rawTestReceipt = Join-Path $rawTestDirectory 'test-results.trx'
New-Item -ItemType Directory -Force -Path $rawTestDirectory | Out-Null
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$resolvedOutputDirectory = (Resolve-Path -LiteralPath $OutputDirectory).Path
Remove-Item -LiteralPath $rawTestReceipt -Force -ErrorAction SilentlyContinue
dotnet test PrimeAxiom.sln --configuration Release --no-build `
    --logger 'trx;LogFileName=test-results.trx' `
    --results-directory $rawTestDirectory
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

[xml]$testReceipt = Get-Content -LiteralPath $rawTestReceipt -Raw
$counters = $testReceipt.SelectSingleNode("//*[local-name()='Counters']")
$times = $testReceipt.SelectSingleNode("//*[local-name()='Times']")
if ($null -eq $counters -or $null -eq $times) {
    throw 'The TRX receipt did not contain expected counters and timing metadata.'
}

$testAssembly = Resolve-Path 'tests/PrimeAxiom.Tests/bin/Release/net8.0/PrimeAxiom.Tests.dll'
$testResults = @($testReceipt.SelectNodes("//*[local-name()='UnitTestResult']"))
$testNames = @($testResults |
    ForEach-Object { $_.GetAttribute('testName') } |
    Sort-Object -Unique)
$testCaseIds = @($testResults |
    ForEach-Object { $_.GetAttribute('testId') })
$uniqueTestCaseIds = @($testCaseIds |
    Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
    Sort-Object -Unique)
$totalTests = [int]$counters.GetAttribute('total')
$executedTests = [int]$counters.GetAttribute('executed')
$passedTests = [int]$counters.GetAttribute('passed')
$failedTests = [int]$counters.GetAttribute('failed')
$skippedTests = [int]$counters.GetAttribute('notExecuted')
$allTestsPassed =
    $totalTests -gt 0 -and
    $executedTests -eq $totalTests -and
    $passedTests -eq $totalTests -and
    $failedTests -eq 0 -and
    $skippedTests -eq 0 -and
    $testResults.Count -eq $totalTests -and
    $testNames.Count -gt 0 -and
    $testCaseIds.Count -eq $totalTests -and
    $uniqueTestCaseIds.Count -eq $totalTests
$sanitizedReceipt = [ordered]@{
    Schema = 'prime-axiom-build001-test-summary-v1'
    Command = 'dotnet test PrimeAxiom.sln --configuration Release --no-build --logger trx'
    BuildConfiguration = 'Release'
    TargetFramework = 'net8.0'
    StartedAtUtc = [DateTimeOffset]::Parse($times.GetAttribute('start')).ToUniversalTime().ToString('o')
    FinishedAtUtc = [DateTimeOffset]::Parse($times.GetAttribute('finish')).ToUniversalTime().ToString('o')
    Counters = [ordered]@{
        Total = $totalTests
        Executed = $executedTests
        Passed = $passedTests
        Failed = $failedTests
        Skipped = $skippedTests
    }
    TestAssembly = 'PrimeAxiom.Tests.dll'
    TestAssemblySha256 = (Get-FileHash -LiteralPath $testAssembly -Algorithm SHA256).Hash
    TestResultCount = $testResults.Count
    UniqueDisplayNameCount = $testNames.Count
    DisplayTestNames = $testNames
    TestCaseIds = @($uniqueTestCaseIds | Sort-Object)
    ClaimStatus = if ($allTestsPassed) { 'BOUNDED_PASS' } else { 'FAILED' }
    ClaimCeiling = 'Pass applies only to the recorded TestCaseIds executed from the recorded TestAssemblySha256; parameterized display names may collapse.'
}
$testSummaryPath = Join-Path $resolvedOutputDirectory 'test-summary.json'
$testSummaryJson = $sanitizedReceipt | ConvertTo-Json -Depth 4
[System.IO.File]::WriteAllText(
    $testSummaryPath,
    $testSummaryJson + [Environment]::NewLine,
    [System.Text.UTF8Encoding]::new($false))
if (-not $allTestsPassed) {
    throw 'The Build 001 test receipt is not a complete zero-skip pass.'
}

$experimentArguments = @(
    'run', '--project', 'src/PrimeAxiom.Cli', '--configuration', 'Release', '--no-build', '--',
    'experiment-build001', '--output', $OutputDirectory
)
if ($SkipBenchmarks) {
    $experimentArguments += '--skip-benchmarks'
}

dotnet @experimentArguments
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$manifestPath = Resolve-Path (Join-Path $resolvedOutputDirectory 'manifest.json')
$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
$frozenPlanPath = Resolve-Path 'research/build001_experiment_plan.md'
$actualFrozenPlanHash = (Get-FileHash -LiteralPath $frozenPlanPath -Algorithm SHA256).Hash
if ($manifest.FrozenPlanSha256 -ne $actualFrozenPlanHash) {
    throw 'Manifest frozen-plan hash does not match research/build001_experiment_plan.md.'
}

foreach ($file in $manifest.Files) {
    $candidate = Join-Path (Split-Path $manifestPath) $file.Path
    if (-not (Test-Path -LiteralPath $candidate -PathType Leaf)) {
        throw "Manifest file is missing: $($file.Path)"
    }

    $actualHash = (Get-FileHash -LiteralPath $candidate -Algorithm SHA256).Hash
    if ($actualHash -ne $file.Sha256) {
        throw "Manifest hash mismatch: $($file.Path)"
    }
}

$coveragePath = Join-Path $resolvedOutputDirectory 'protocol_coverage.json'
$coverage = Get-Content -LiteralPath $coveragePath -Raw | ConvertFrom-Json
if ($coverage.Status -ne 'PILOT_SUBSET_COMPLETE_FULL_CONFIRMATION_NOT_RUN') {
    throw 'Protocol coverage status is absent or unexpectedly broadened.'
}
if ($coverage.FrozenPlanSha256 -ne $actualFrozenPlanHash) {
    throw 'Protocol coverage frozen-plan hash does not match research/build001_experiment_plan.md.'
}

git diff --exit-code $baselineCommit -- BUILD_000_REPORT.md results/build000
if ($LASTEXITCODE -ne 0) {
    throw 'Build 001 verification changed immutable Build 000 evidence.'
}

Write-Host "Build 001 verification passed: $passedTests/$totalTests tests; 0 skipped; evidence manifest hashes verified."
