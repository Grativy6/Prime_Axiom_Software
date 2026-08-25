[CmdletBinding()]
param(
    [ValidateNotNullOrEmpty()]
    [string]$SolutionPath = 'PrimeAxiom.sln'
)

$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false

$dotnetCommand = Get-Command dotnet -CommandType Application -ErrorAction Stop |
    Select-Object -First 1
$dotnetHostPath = $dotnetCommand.Source
if ([string]::IsNullOrWhiteSpace($dotnetHostPath)) {
    throw 'The dotnet CLI command did not resolve to an executable path.'
}

$transientDiagnostic = 'Unable to locate dotnet CLI. Ensure that it is on the PATH.'

function Invoke-FormatAttempt {
    $output = @(& $dotnetHostPath format $SolutionPath --verify-no-changes --no-restore 2>&1 |
        ForEach-Object { $_.ToString() })
    $exitCode = $LASTEXITCODE
    $output | ForEach-Object { Write-Host $_ }

    [pscustomobject]@{
        ExitCode = [int]$exitCode
        Output = $output
    }
}

$result = Invoke-FormatAttempt
if ($result.ExitCode -eq 0) {
    return 0
}

# dotnet/sdk#44957 records this intermittent formatter failure. Code-format
# differences use exit 2 and cannot enter this exact-signature retry branch.
$nonBlankLines = @($result.Output | Where-Object {
    -not [string]::IsNullOrWhiteSpace($_)
})
$isKnownTransient =
    $result.ExitCode -eq 4 -and
    $nonBlankLines.Count -eq 1 -and
    $nonBlankLines[0] -ceq $transientDiagnostic

if (-not $isKnownTransient) {
    return $result.ExitCode
}

Write-Warning 'dotnet format hit its known transient CLI-discovery race; retrying once.'
Start-Sleep -Seconds 2
$retryResult = Invoke-FormatAttempt
return $retryResult.ExitCode
