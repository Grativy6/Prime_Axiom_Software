[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateNotNullOrEmpty()]
    [string]$LinuxArtifactDirectory,
    [Parameter(Mandatory)]
    [ValidateNotNullOrEmpty()]
    [string]$WindowsArtifactDirectory,
    [ValidateNotNullOrEmpty()]
    [string]$OutputPath = 'artifacts/build002-cross-platform-verification.json'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$protocol = 'PAH-BUILD002-CONF0001'

function Require([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
}

function Read-Json([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { throw "Required receipt is missing: $Path" }
    Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
}

function File-Sha256([string]$Path) {
    (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash
}

function Verify-Manifest([string]$Root, [string]$ManifestPath) {
    $manifest = Read-Json $ManifestPath
    foreach ($file in @($manifest.files)) {
        $relative = [string]$file.path
        $target = Join-Path $Root $relative.Replace('/', [System.IO.Path]::DirectorySeparatorChar)
        Require (Test-Path -LiteralPath $target -PathType Leaf) "Manifest file is missing: $relative"
        Require ((File-Sha256 $target) -eq [string]$file.sha256) "Manifest hash mismatch: $relative"
        $bytesProperty = $file.psobject.Properties['bytes']
        if ($null -ne $bytesProperty) {
            Require ((Get-Item -LiteralPath $target).Length -eq [long]$bytesProperty.Value) "Manifest byte-count mismatch: $relative"
        }
    }
    $manifest
}

function Artifact-Paths([string]$DownloadRoot) {
    $root = [System.IO.Path]::GetFullPath($DownloadRoot)
    [pscustomobject]@{
        Root = $root
        Results = Join-Path $root 'artifacts/build002-ci'
        Verification = Join-Path $root 'artifacts/build002-ci-verification.json'
        Hdl = Join-Path $root '.artifacts/build002-hdl-ci'
    }
}

$linux = Artifact-Paths $LinuxArtifactDirectory
$windows = Artifact-Paths $WindowsArtifactDirectory
$linuxVerification = Read-Json $linux.Verification
$windowsVerification = Read-Json $windows.Verification

Require ($linuxVerification.status -eq 'PASS') 'Linux verifier did not pass.'
Require ($linuxVerification.evidence_role -eq 'CANONICAL_LINUX_TERMINAL') 'Linux receipt is not canonical terminal evidence.'
Require ($linuxVerification.hdl_platform -eq 'linux-x64') 'Linux receipt has the wrong HDL platform.'
Require ($linuxVerification.classification -eq 'NO_HARDWARE_ADVANTAGE') 'Linux terminal classification mismatch.'
Require ($windowsVerification.status -eq 'PASS') 'Windows verifier did not pass.'
Require ($windowsVerification.evidence_role -eq 'WINDOWS_REPRODUCIBILITY_NONTERMINAL') 'Windows receipt crossed its reproducibility role.'
Require ($windowsVerification.hdl_platform -eq 'windows-x64') 'Windows receipt has the wrong HDL platform.'
Require ($windowsVerification.classification -eq 'PARTIAL — FINAL DECISION NOT EARNED') 'Windows receipt must remain non-terminal.'

foreach ($receipt in @($linuxVerification, $windowsVerification)) {
    Require ([int]$receipt.tests.total -gt 0) 'A repository test receipt is empty.'
    Require ([int]$receipt.tests.passed -eq [int]$receipt.tests.total) 'A repository test receipt is not all-pass.'
    Require ([int]$receipt.tests.failed -eq 0 -and [int]$receipt.tests.skipped -eq 0) 'A repository test receipt has failures or skips.'
    Require ([long]$receipt.arithmetic_checks -eq 656810) 'Arithmetic check count mismatch.'
    Require ([int]$receipt.hdl_cases -eq 260) 'HDL case count mismatch.'
    Require ([int]$receipt.formal_cases -eq 15) 'Formal case count mismatch.'
    Require ([int]$receipt.synthesis_rows -eq 150) 'Synthesis row count mismatch.'
    Require $receipt.deterministic_replay 'A platform replay was not deterministic.'
}

$linuxResultManifestPath = Join-Path $linux.Results 'manifest.json'
$windowsResultManifestPath = Join-Path $windows.Results 'manifest.json'
$null = Verify-Manifest $linux.Results $linuxResultManifestPath
$null = Verify-Manifest $windows.Results $windowsResultManifestPath
$linuxRawManifestPath = Join-Path $linux.Hdl 'manifest.json'
$windowsRawManifestPath = Join-Path $windows.Hdl 'manifest.json'
$linuxRawManifest = Verify-Manifest $linux.Hdl $linuxRawManifestPath
$windowsRawManifest = Verify-Manifest $windows.Hdl $windowsRawManifestPath
Require (@($linuxRawManifest.files).Count -eq 751) 'Linux raw manifest does not contain 751 entries.'
Require (@($windowsRawManifest.files).Count -eq 751) 'Windows raw manifest does not contain 751 entries.'

$linuxSummary = Read-Json (Join-Path $linux.Hdl 'verification-summary.json')
$windowsSummary = Read-Json (Join-Path $windows.Hdl 'verification-summary.json')
foreach ($summary in @($linuxSummary, $windowsSummary)) {
    Require ($summary.protocol -eq $protocol -and $summary.scope -eq 'FULL_W4_W6_W8') 'HDL summary scope mismatch.'
    Require ($summary.status -eq 'PASS' -and [int]$summary.total_cases -eq 260 -and [int]$summary.failed_cases -eq 0) 'HDL summary is not a complete pass.'
}
$linuxCaseSemantics = @($linuxSummary.cases | ForEach-Object {
    [ordered]@{ phase = $_.phase; case = $_.case; status = $_.status; detail = $_.detail }
}) | ConvertTo-Json -Depth 4 -Compress
$windowsCaseSemantics = @($windowsSummary.cases | ForEach-Object {
    [ordered]@{ phase = $_.phase; case = $_.case; status = $_.status; detail = $_.detail }
}) | ConvertTo-Json -Depth 4 -Compress
Require ($linuxCaseSemantics -ceq $windowsCaseSemantics) 'Ordered HDL case semantics differ by platform.'

$linuxRows = @(Import-Csv -LiteralPath (Join-Path $linux.Results 'synthesis_metrics.csv'))
$windowsRows = @(Import-Csv -LiteralPath (Join-Path $windows.Results 'synthesis_metrics.csv'))
Require ($linuxRows.Count -eq 150 -and $windowsRows.Count -eq 150) 'Cross-platform synthesis row count mismatch.'
$varyingColumns = @('platform', 'tool_version', 'netlist_sha256')
$stableColumns = @($linuxRows[0].psobject.Properties.Name | Where-Object { $_ -notin $varyingColumns })
$platformDifferences = 0
$toolVersionDifferences = 0
$netlistHashDifferences = 0
$optimizedNetlistHashDifferences = 0
for ($index = 0; $index -lt $linuxRows.Count; $index++) {
    foreach ($column in $stableColumns) {
        Require ([string]$linuxRows[$index].$column -ceq [string]$windowsRows[$index].$column) "Stable synthesis field differs at row ${index}: $column"
    }
    if ($linuxRows[$index].platform -cne $windowsRows[$index].platform) { $platformDifferences++ }
    if ($linuxRows[$index].tool_version -cne $windowsRows[$index].tool_version) { $toolVersionDifferences++ }
    if ($linuxRows[$index].netlist_sha256 -cne $windowsRows[$index].netlist_sha256) {
        $netlistHashDifferences++
        if ($linuxRows[$index].evidence_class -eq 'STRUCTURAL_OPTIMIZED') { $optimizedNetlistHashDifferences++ }
    }
}
Require ($platformDifferences -eq 150) 'Not every synthesis row records a distinct platform.'
Require ($toolVersionDifferences -eq 150) 'Not every synthesis row records the platform-specific tool string.'
Require ($netlistHashDifferences -eq 150) 'Expected all per-row netlist hashes to differ by platform.'
Require ($optimizedNetlistHashDifferences -eq 75) 'Expected all optimized netlist hashes to differ by platform.'

$platformNeutralFiles = @(
    'correctness.json',
    'static_costs.csv',
    'dynamic_operations.csv',
    'workload_matrix.csv',
    'ingress_egress.csv',
    'representation_search.csv',
    'addition_adversary.csv',
    'hostile_support.csv',
    'figures/static_gate_counts.svg',
    'figures/representation_bits.svg'
)
foreach ($relative in $platformNeutralFiles) {
    $linuxPath = Join-Path $linux.Results $relative.Replace('/', [System.IO.Path]::DirectorySeparatorChar)
    $windowsPath = Join-Path $windows.Results $relative.Replace('/', [System.IO.Path]::DirectorySeparatorChar)
    Require ((File-Sha256 $linuxPath) -eq (File-Sha256 $windowsPath)) "Platform-neutral generated artifact differs: $relative"
}

$receipt = [ordered]@{
    schema = 'prime-axiom-build002-cross-platform-verification-v1'
    protocol = $protocol
    status = 'PASS'
    canonical_platform = 'linux-x64'
    reproducibility_platform = 'windows-x64'
    linux = [ordered]@{
        result_manifest_sha256 = File-Sha256 $linuxResultManifestPath
        raw_manifest_sha256 = File-Sha256 $linuxRawManifestPath
        verification_receipt_sha256 = File-Sha256 $linux.Verification
    }
    windows = [ordered]@{
        result_manifest_sha256 = File-Sha256 $windowsResultManifestPath
        raw_manifest_sha256 = File-Sha256 $windowsRawManifestPath
        verification_receipt_sha256 = File-Sha256 $windows.Verification
    }
    hdl_case_semantics_equal = $true
    synthesis_rows = 150
    stable_synthesis_field_differences = 0
    platform_differences = $platformDifferences
    tool_version_differences = $toolVersionDifferences
    netlist_hash_differences = $netlistHashDifferences
    optimized_netlist_hash_differences = $optimizedNetlistHashDifferences
    platform_neutral_files_equal = $platformNeutralFiles.Count
    notes = 'Linux is canonical; Windows is non-terminal reproducibility evidence. No mapped-netlist or physical identity is claimed.'
}
$outputFull = [System.IO.Path]::GetFullPath($OutputPath)
$parent = Split-Path -Parent $outputFull
if ($parent) { New-Item -ItemType Directory -Force -Path $parent | Out-Null }
[System.IO.File]::WriteAllText(
    $outputFull,
    (($receipt | ConvertTo-Json -Depth 8) + "`n"),
    [System.Text.UTF8Encoding]::new($false))
Write-Host "Build 002 cross-platform verification passed: 260 ordered HDL cases; 150 stable synthesis vectors; 75/75 optimized hashes differ; Linux canonical."
