[CmdletBinding()]
param(
    [ValidateNotNullOrEmpty()]
    [string]$CacheRoot = '.artifacts/oss-cad-suite',
    [ValidateNotNullOrEmpty()]
    [string]$ReceiptPath = '.artifacts/build002-hdl/toolchain-bootstrap.json',
    [switch]$DownloadOnly,
    [switch]$ForceDownload
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$lockPath = Join-Path $repoRoot 'hdl/toolchain.lock.json'
$lock = Get-Content -Raw -LiteralPath $lockPath | ConvertFrom-Json

$architecture = [System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture.ToString()
if ($architecture -ne 'X64') {
    throw "OSS CAD Suite lock supports only x64; observed $architecture."
}

if ($IsWindows) {
    $platform = 'windows-x64'
} elseif ($IsLinux) {
    $platform = 'linux-x64'
} else {
    throw 'OSS CAD Suite lock supports only Windows x64 and Linux x64.'
}

$asset = $lock.assets.$platform
if ($null -eq $asset) {
    throw "No locked asset for $platform."
}

function Write-DeterministicJson {
    param([Parameter(Mandatory)]$Value, [Parameter(Mandatory)][string]$Path)
    $parent = Split-Path -Parent $Path
    if ($parent) { New-Item -ItemType Directory -Force -Path $parent | Out-Null }
    $json = $Value | ConvertTo-Json -Depth 8
    [System.IO.File]::WriteAllText(
        $Path,
        $json + [Environment]::NewLine,
        [System.Text.UTF8Encoding]::new($false))
}

function Assert-PathWithin {
    param([Parameter(Mandatory)][string]$Candidate, [Parameter(Mandatory)][string]$Root)
    $rootFull = [System.IO.Path]::GetFullPath($Root).TrimEnd(
        [System.IO.Path]::DirectorySeparatorChar,
        [System.IO.Path]::AltDirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
    $candidateFull = [System.IO.Path]::GetFullPath($Candidate)
    if (-not $candidateFull.StartsWith($rootFull, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing path outside cache root: $candidateFull"
    }
}

$cacheFull = [System.IO.Path]::GetFullPath((Join-Path $repoRoot $CacheRoot))
$receiptFull = [System.IO.Path]::GetFullPath((Join-Path $repoRoot $ReceiptPath))
New-Item -ItemType Directory -Force -Path $cacheFull | Out-Null
$archivePath = Join-Path $cacheFull $asset.file
$partialArchive = $archivePath + '.partial'
Assert-PathWithin -Candidate $archivePath -Root $cacheFull
Assert-PathWithin -Candidate $partialArchive -Root $cacheFull

$baseReceipt = [ordered]@{
    schema = 'prime-axiom-hdl-toolchain-bootstrap-v1'
    protocol = $lock.protocol
    release = $lock.release
    platform = $platform
    asset = $asset.file
    expected_bytes = [long]$asset.bytes
    expected_sha256 = ([string]$asset.sha256).ToLowerInvariant()
    source_url = $asset.url
}

try {
    $archiveValid = $false
    if (Test-Path -LiteralPath $archivePath -PathType Leaf) {
        $observedLength = (Get-Item -LiteralPath $archivePath).Length
        $observedHash = (Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash.ToLowerInvariant()
        $archiveValid =
            $observedLength -eq [long]$asset.bytes -and
            $observedHash -eq ([string]$asset.sha256).ToLowerInvariant()
        if (-not $archiveValid -and -not $ForceDownload) {
            throw "Existing archive failed the locked byte/hash check: $archivePath"
        }
    }

    if (-not $archiveValid) {
        if (Test-Path -LiteralPath $partialArchive) {
            [System.IO.File]::Delete($partialArchive)
        }
        Write-Host "Downloading locked OSS CAD Suite $($lock.release) $platform ($($asset.bytes) bytes)..."
        Invoke-WebRequest -Uri $asset.url -OutFile $partialArchive
        $downloadLength = (Get-Item -LiteralPath $partialArchive).Length
        $downloadHash = (Get-FileHash -LiteralPath $partialArchive -Algorithm SHA256).Hash.ToLowerInvariant()
        if ($downloadLength -ne [long]$asset.bytes -or
            $downloadHash -ne ([string]$asset.sha256).ToLowerInvariant()) {
            throw "Downloaded archive failed locked byte/hash check: length=$downloadLength sha256=$downloadHash"
        }
        [System.IO.File]::Move($partialArchive, $archivePath, $true)
    }

    $actualLength = (Get-Item -LiteralPath $archivePath).Length
    $actualHash = (Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash.ToLowerInvariant()
    $downloadReceipt = [ordered]@{} + $baseReceipt
    $downloadReceipt.archive_bytes = $actualLength
    $downloadReceipt.archive_sha256 = $actualHash
    $downloadReceipt.archive_status = 'HASH_VERIFIED'

    if ($DownloadOnly) {
        $downloadReceipt.status = 'DOWNLOAD_ONLY_COMPLETE'
        Write-DeterministicJson -Value $downloadReceipt -Path $receiptFull
        Write-Output $archivePath
        exit 0
    }

    $releaseRoot = Join-Path $cacheFull "$($lock.release)-$platform"
    $installRoot = Join-Path $releaseRoot 'oss-cad-suite'
    $exeSuffix = if ($IsWindows) { '.exe' } else { '' }
    $yosysPath = Join-Path $installRoot "bin/yosys$exeSuffix"

    if (-not (Test-Path -LiteralPath $yosysPath -PathType Leaf)) {
        New-Item -ItemType Directory -Force -Path $releaseRoot | Out-Null
        $extractRoot = Join-Path $cacheFull ".extract-$($lock.release)-$platform-$PID"
        Assert-PathWithin -Candidate $extractRoot -Root $cacheFull
        if (Test-Path -LiteralPath $extractRoot) {
            [System.IO.Directory]::Delete($extractRoot, $true)
        }
        New-Item -ItemType Directory -Force -Path $extractRoot | Out-Null
        try {
            & tar -xzf $archivePath -C $extractRoot
            if ($LASTEXITCODE -ne 0) { throw "tar extraction failed with exit $LASTEXITCODE" }
            $extractedSuite = Join-Path $extractRoot 'oss-cad-suite'
            if (-not (Test-Path -LiteralPath (Join-Path $extractedSuite "bin/yosys$exeSuffix") -PathType Leaf)) {
                throw 'Archive did not contain the expected oss-cad-suite/bin/yosys path.'
            }
            if (Test-Path -LiteralPath $installRoot) {
                Assert-PathWithin -Candidate $installRoot -Root $cacheFull
                [System.IO.Directory]::Delete($installRoot, $true)
            }
            [System.IO.Directory]::Move($extractedSuite, $installRoot)
        } finally {
            if (Test-Path -LiteralPath $extractRoot) {
                [System.IO.Directory]::Delete($extractRoot, $true)
            }
        }
    }

    $binPath = Join-Path $installRoot 'bin'
    if ($IsWindows) {
        # The packaged Windows tools load DLLs from lib. Exercise the suite's
        # own environment contract rather than assuming bin-only is enough.
        $env:YOSYSHQ_ROOT = $installRoot.TrimEnd('\', '/') + '\'
        . (Join-Path $installRoot 'environment.ps1')
        if (-not ('PrimeAxiom.ShortPath' -as [type])) {
            Add-Type -TypeDefinition @'
namespace PrimeAxiom {
    using System.Runtime.InteropServices;
    using System.Text;
    public static class ShortPath {
        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        public static extern uint GetShortPathName(string longPath, StringBuilder shortPath, int capacity);
    }
}
'@
        }
        $shortPathBuffer = [System.Text.StringBuilder]::new(32768)
        $shortPathLength = [PrimeAxiom.ShortPath]::GetShortPathName(
            $installRoot,
            $shortPathBuffer,
            $shortPathBuffer.Capacity)
        if ($shortPathLength -eq 0) {
            throw 'Windows could not resolve the suite to a short path required by verilator_bin.'
        }
        $env:VERILATOR_ROOT = Join-Path $shortPathBuffer.ToString() 'share/verilator'
    } else {
        $env:PATH = $binPath + [System.IO.Path]::PathSeparator + $env:PATH
    }
    $probeSpecs = [ordered]@{
        yosys = @{ Command = $(if ($IsWindows) { 'yosys.exe' } else { 'yosys' }); Prefix = @(); Arguments = @('--version'); Pattern = '^Yosys ' }
        iverilog = @{ Command = $(if ($IsWindows) { 'iverilog.exe' } else { 'iverilog' }); Prefix = @(); Arguments = @('-V'); Pattern = 'Icarus Verilog' }
        vvp = @{ Command = $(if ($IsWindows) { 'vvp.exe' } else { 'vvp' }); Prefix = @(); Arguments = @('-V'); Pattern = 'Icarus Verilog runtime' }
        # Windows ships an extensionless Perl wrapper, which PowerShell does
        # not resolve. verilator_bin is the underlying executable.
        verilator = @{ Command = $(if ($IsWindows) { 'verilator_bin.exe' } else { 'verilator' }); Prefix = @(); Arguments = @('--version'); Pattern = '^Verilator ' }
        # The tiny Windows Python launchers return exit 0 even when paths with
        # spaces make process creation fail. Invoke bundled Python explicitly.
        sby = @{
            Command = $(if ($IsWindows) { $env:PYTHON_EXECUTABLE } else { 'sby' })
            Prefix = $(if ($IsWindows) { @((Join-Path $installRoot 'bin/sby-script.py')) } else { @() })
            Arguments = @('--version')
            Pattern = '^SBY v'
        }
        'yosys-smtbmc' = @{
            Command = $(if ($IsWindows) { $env:PYTHON_EXECUTABLE } else { 'yosys-smtbmc' })
            Prefix = $(if ($IsWindows) { @((Join-Path $installRoot 'bin/yosys-smtbmc-script.py')) } else { @() })
            Arguments = @('-h')
            # The Windows Python entry point names the script; the Linux
            # launcher prints its installed command name. Both must expose
            # the same recognizable help contract rather than merely exit 0.
            Pattern = '^yosys-smtbmc(?:-script\.py)?\s+\[options\]'
        }
        z3 = @{ Command = $(if ($IsWindows) { 'z3.exe' } else { 'z3' }); Prefix = @(); Arguments = @('--version'); Pattern = '^Z3 version ' }
    }
    $versions = [ordered]@{}
    $resolvedCommands = [ordered]@{}
    foreach ($commandName in $lock.required_commands) {
        $spec = $probeSpecs.$commandName
        $resolved = Get-Command $spec.Command -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($null -eq $resolved) { throw "Locked suite is missing required command: $commandName" }
        $arguments = @($spec.Prefix) + @($spec.Arguments)
        $output = (& $resolved.Source @arguments 2>&1 | Out-String).Trim()
        if ($LASTEXITCODE -ne 0) { throw "$commandName version probe failed with exit $LASTEXITCODE" }
        if ($output -match '(?i)failed to create process' -or $output -notmatch $spec.Pattern) {
            throw "$commandName probe returned an unrecognized success shape: $output"
        }
        $firstRecognizedLine = @($output -split "`r?`n" | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })[0]
        $versions[$commandName] = $firstRecognizedLine
        $resolvedParts = @((Split-Path -Leaf $resolved.Source))
        $resolvedParts += @($spec.Prefix | ForEach-Object { Split-Path -Leaf $_ })
        $resolvedCommands[$commandName] = $resolvedParts -join ' '
    }

    $receipt = [ordered]@{} + $downloadReceipt
    $receipt.install_layout = 'oss-cad-suite/bin'
    $receipt.resolved_commands = $resolvedCommands
    $receipt.tool_versions = $versions
    $receipt.status = 'TOOLCHAIN_VERIFIED'
    Write-DeterministicJson -Value $receipt -Path $receiptFull
    Write-Output $installRoot
} catch {
    $failure = [ordered]@{} + $baseReceipt
    $failure.status = 'BLOCKED_TOOLCHAIN'
    $failure.error = $_.Exception.Message
    Write-DeterministicJson -Value $failure -Path $receiptFull
    throw
}
