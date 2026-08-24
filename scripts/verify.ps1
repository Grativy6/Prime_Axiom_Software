[CmdletBinding()]
param(
    [switch]$SkipBenchmarks
)

$ErrorActionPreference = 'Stop'

dotnet restore PrimeAxiom.sln --locked-mode
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

dotnet format PrimeAxiom.sln --verify-no-changes --no-restore
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

dotnet build PrimeAxiom.sln --configuration Release --no-restore
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$rawTestDirectory = '.artifacts/test-results'
$rawTestReceipt = Join-Path $rawTestDirectory 'test-results.trx'
New-Item -ItemType Directory -Force -Path $rawTestDirectory | Out-Null
New-Item -ItemType Directory -Force -Path results/build000 | Out-Null
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
$testNames = @($testReceipt.SelectNodes("//*[local-name()='UnitTestResult']") |
    ForEach-Object { $_.GetAttribute('testName') } |
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
    $testNames.Count -eq $totalTests
$sanitizedReceipt = [ordered]@{
    Schema = 'prime-axiom-build000-test-summary-v1'
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
    TestNames = $testNames
    ClaimStatus = if ($allTestsPassed) { 'BOUNDED_PASS' } else { 'FAILED' }
    ClaimCeiling = 'Pass applies only to TestNames executed from the recorded TestAssemblySha256.'
}
$testSummaryPath = Join-Path (Resolve-Path 'results/build000') 'test-summary.json'
$testSummaryJson = $sanitizedReceipt | ConvertTo-Json -Depth 4
[System.IO.File]::WriteAllText(
    $testSummaryPath,
    $testSummaryJson + [Environment]::NewLine,
    [System.Text.UTF8Encoding]::new($false))
if (-not $allTestsPassed) {
    throw 'The test receipt is not a complete zero-skip pass.'
}

$experimentArguments = @(
    'run', '--project', 'src/PrimeAxiom.Cli', '--configuration', 'Release', '--no-build', '--',
    'experiment', '--output', 'results/build000'
)
if ($SkipBenchmarks) {
    $experimentArguments += '--skip-benchmarks'
}

dotnet @experimentArguments
exit $LASTEXITCODE
