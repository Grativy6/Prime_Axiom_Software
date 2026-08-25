[CmdletBinding()]
param(
    [ValidateNotNullOrEmpty()]
    [string]$OutputDirectory = 'artifacts/build002-ci',
    [ValidateNotNullOrEmpty()]
    [string]$HdlOutputDirectory = '.artifacts/build002-hdl-ci',
    [switch]$UseExistingHdlEvidence
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$baselineCommit = 'dfd2e7a409aaa114f054a0b40e4b282c68dc0d52'
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
Push-Location $repositoryRoot
try {
    git diff --exit-code $baselineCommit -- BUILD_000_REPORT.md BUILD_001_REPORT.md results/build000 results/build001
    if ($LASTEXITCODE -ne 0) {
        throw 'Inherited Build 000/001 reports or checked evidence differ from the frozen Build 002 baseline.'
    }

    dotnet restore PrimeAxiom.sln --locked-mode
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    dotnet format PrimeAxiom.sln --verify-no-changes --no-restore
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    dotnet build PrimeAxiom.sln --configuration Release --no-restore
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    $testReceiptDirectory = '.artifacts/build002-test-results'
    $testReceipt = Join-Path $testReceiptDirectory 'test-results.trx'
    New-Item -ItemType Directory -Force -Path $testReceiptDirectory | Out-Null
    [System.IO.File]::Delete([System.IO.Path]::GetFullPath($testReceipt))
    dotnet test PrimeAxiom.sln --configuration Release --no-build `
        --logger 'trx;LogFileName=test-results.trx' `
        --results-directory $testReceiptDirectory
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    [xml]$trx = Get-Content -LiteralPath $testReceipt -Raw
    $counters = $trx.SelectSingleNode("//*[local-name()='Counters']")
    if ($null -eq $counters) { throw 'The Build 002 TRX receipt has no Counters element.' }
    $total = [int]$counters.total
    $passed = [int]$counters.passed
    $failed = [int]$counters.failed + [int]$counters.error + [int]$counters.timeout + [int]$counters.aborted
    $skipped = [int]$counters.notExecuted + [int]$counters.inconclusive + [int]$counters.notRunnable + [int]$counters.disconnected + [int]$counters.warning
    if ($total -le 0 -or $passed -ne $total -or $failed -ne 0 -or $skipped -ne 0) {
        throw "Build 002 tests are not a complete zero-skip pass: total=$total passed=$passed failed=$failed skipped=$skipped"
    }

    $hdlRoot = [System.IO.Path]::GetFullPath($HdlOutputDirectory)
    $hdlSummary = Join-Path $hdlRoot 'verification-summary.json'
    $hdlSynthesis = Join-Path $hdlRoot 'synthesis-metrics.csv'
    $hdlToolchain = Join-Path $hdlRoot 'toolchain-bootstrap.json'
    if (-not $UseExistingHdlEvidence) {
        & (Join-Path $PSScriptRoot 'build002-hdl-verify.ps1') -OutputDirectory $HdlOutputDirectory
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    }
    foreach ($required in @($hdlSummary, $hdlSynthesis, $hdlToolchain)) {
        if (-not (Test-Path -LiteralPath $required -PathType Leaf)) {
            throw "Required Build 002 HDL evidence is missing: $required"
        }
    }

    $outputFull = [System.IO.Path]::GetFullPath($OutputDirectory)
    New-Item -ItemType Directory -Force -Path $outputFull | Out-Null
    $generatorArguments = @(
        'run', '--project', 'src/PrimeAxiom.Cli', '--configuration', 'Release', '--no-build', '--',
        'experiment-build002', '--output', $OutputDirectory,
        '--hdl-verification-summary', $hdlSummary,
        '--hdl-synthesis-metrics', $hdlSynthesis,
        '--hdl-toolchain', $hdlToolchain
    )
    dotnet @generatorArguments
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    $manifestPath = Join-Path $outputFull 'manifest.json'
    $firstManifestSha256 = (Get-FileHash -LiteralPath $manifestPath -Algorithm SHA256).Hash
    $firstManifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
    $firstHashes = @{}
    foreach ($file in $firstManifest.files) { $firstHashes[[string]$file.path] = [string]$file.sha256 }

    dotnet @generatorArguments
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    $secondManifestSha256 = (Get-FileHash -LiteralPath $manifestPath -Algorithm SHA256).Hash
    if ($firstManifestSha256 -ne $secondManifestSha256) {
        throw "Generated manifest is not deterministic across an immediate replay: first=$firstManifestSha256 second=$secondManifestSha256"
    }
    $coverage = Get-Content -LiteralPath (Join-Path $outputFull 'protocol_coverage.json') -Raw | ConvertFrom-Json
    if ($coverage.classification -ne 'NO_HARDWARE_ADVANTAGE' -or -not $coverage.decisionEarned) {
        throw "Build 002 terminal classification was not earned: $($coverage.classification)"
    }
    if (-not $coverage.hdl.complete -or $coverage.hdl.status -ne 'COMPLETE_VERIFIED') {
        throw 'Imported HDL evidence is not COMPLETE_VERIFIED.'
    }

    $manifest = Get-Content -LiteralPath (Join-Path $outputFull 'manifest.json') -Raw | ConvertFrom-Json
    foreach ($file in $manifest.files) {
        $relative = [string]$file.path
        $target = Join-Path $outputFull $relative.Replace('/', [System.IO.Path]::DirectorySeparatorChar)
        if (-not (Test-Path -LiteralPath $target -PathType Leaf)) { throw "Manifest file is missing: $relative" }
        $actual = (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash
        if ($actual -ne [string]$file.sha256) { throw "Manifest hash mismatch: $relative" }
        if (-not $firstHashes.ContainsKey($relative) -or $firstHashes[$relative] -ne $actual) {
            throw "Generated evidence is not deterministic across an immediate replay: $relative"
        }
    }

    foreach ($csvName in @('dynamic_operations.csv', 'workload_matrix.csv')) {
        $rows = @(Import-Csv -LiteralPath (Join-Path $outputFull $csvName))
        if ($rows.Count -eq 0 -or @($rows | Where-Object { $_.operation_class -eq 'UNCLASSIFIED' }).Count -ne 0) {
            throw "Operation-class coverage failed in $csvName"
        }
    }

    $receipt = [ordered]@{
        schema = 'prime-axiom-build002-verification-v1'
        protocol = 'PAH-BUILD002-CONF0001'
        status = 'PASS'
        classification = [string]$coverage.classification
        tests = [ordered]@{ total = $total; passed = $passed; failed = $failed; skipped = $skipped }
        arithmetic_checks = [long]$coverage.correctness.checkCount
        hdl_cases = [int]$coverage.hdl.verificationCaseCount
        formal_cases = [int]$coverage.hdl.formalCaseCount
        synthesis_rows = [int]$coverage.hdl.synthesisRowCount
        deterministic_replay = $true
        manifest_sha256 = $secondManifestSha256
    }
    $receiptPath = $outputFull + '-verification.json'
    [System.IO.File]::WriteAllText(
        $receiptPath,
        (($receipt | ConvertTo-Json -Depth 6) + "`n"),
        [System.Text.UTF8Encoding]::new($false))

    git diff --exit-code $baselineCommit -- BUILD_000_REPORT.md BUILD_001_REPORT.md results/build000 results/build001
    if ($LASTEXITCODE -ne 0) { throw 'Build 002 verification changed inherited Build 000/001 evidence.' }
    Write-Host "Build 002 verification passed: $passed/$total tests; 0 skipped; $($coverage.hdl.verificationCaseCount) HDL cases; deterministic terminal classification $($coverage.classification)."
} finally {
    Pop-Location
}
