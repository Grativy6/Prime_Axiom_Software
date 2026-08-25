[CmdletBinding()]
param(
    [ValidateNotNullOrEmpty()]
    [string]$OutputDirectory = '.artifacts/build002-hdl',
    [ValidateRange(1, 3600)]
    [int]$CommandTimeoutSeconds = 600,
    [switch]$Quick,
    [switch]$SkipFormal,
    [switch]$SkipSynthesis
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$protocol = 'PAH-BUILD002-CONF0001'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
Push-Location $repoRoot
try {
    function Write-Utf8 {
        param([Parameter(Mandatory)][string]$Path,[Parameter(Mandatory)][string]$Content)
        $parent=Split-Path -Parent $Path
        if($parent){New-Item -ItemType Directory -Force -Path $parent|Out-Null}
        [System.IO.File]::WriteAllText($Path,$Content,[System.Text.UTF8Encoding]::new($false))
    }
    function Write-Json {
        param([Parameter(Mandatory)]$Value,[Parameter(Mandatory)][string]$Path)
        Write-Utf8 -Path $Path -Content (($Value|ConvertTo-Json -Depth 10)+[Environment]::NewLine)
    }
    function Invoke-Logged {
        param(
            [Parameter(Mandatory)][string]$FileName,
            [Parameter(Mandatory)][string[]]$Arguments,
            [Parameter(Mandatory)][string]$LogPath,
            [int]$TimeoutSeconds=$CommandTimeoutSeconds
        )
        $info=[System.Diagnostics.ProcessStartInfo]::new()
        $info.FileName=$FileName
        $info.WorkingDirectory=$repoRoot
        $info.UseShellExecute=$false
        $info.RedirectStandardOutput=$true
        $info.RedirectStandardError=$true
        foreach($argument in $Arguments){[void]$info.ArgumentList.Add($argument)}
        $process=[System.Diagnostics.Process]::new()
        $process.StartInfo=$info
        if(-not $process.Start()){throw "Could not start $FileName"}
        $stdoutTask=$process.StandardOutput.ReadToEndAsync()
        $stderrTask=$process.StandardError.ReadToEndAsync()
        $completed=$process.WaitForExit($TimeoutSeconds*1000)
        if(-not $completed){
            try{$process.Kill($true)}catch{}
            $process.WaitForExit()
        }
        $stdout=$stdoutTask.GetAwaiter().GetResult()
        $stderr=$stderrTask.GetAwaiter().GetResult()
        $exitCode=if($completed){$process.ExitCode}else{-1}
        $combined=$stdout
        if(-not [string]::IsNullOrEmpty($stderr)){$combined += "`n--- STDERR ---`n"+$stderr}
        Write-Utf8 -Path $LogPath -Content (($combined.TrimEnd())+[Environment]::NewLine)
        [pscustomobject]@{ExitCode=$exitCode;TimedOut=(-not $completed);Output=$combined}
    }
    function Relative-Path([string]$Path){
        [System.IO.Path]::GetRelativePath($repoRoot,[System.IO.Path]::GetFullPath($Path)).Replace('\','/')
    }
    function Yosys-Quote([string]$Path){
        '"'+([System.IO.Path]::GetFullPath($Path).Replace('\','/').Replace('"','\"'))+'"'
    }
    function Add-CaseResult {
        param([string]$Phase,[string]$Case,[bool]$Passed,[string]$Log,[string]$Detail='')
        $script:caseResults.Add([ordered]@{
            phase=$Phase;case=$Case;status=$(if($Passed){'PASS'}else{'FAIL'});
            log=$(Relative-Path $Log);detail=$Detail
        })
    }

    $outputFull=[System.IO.Path]::GetFullPath((Join-Path $repoRoot $OutputDirectory))
    New-Item -ItemType Directory -Force -Path $outputFull|Out-Null
    $toolReceipt=Join-Path $outputFull 'toolchain-bootstrap.json'
    $toolRootOutput=@(& (Join-Path $PSScriptRoot 'build002-hdl-bootstrap.ps1') -ReceiptPath (Relative-Path $toolReceipt))
    $toolRoot=[string]$toolRootOutput[-1]
    $bin=Join-Path $toolRoot 'bin'
    if($IsWindows){
        $yosys=Join-Path $bin 'yosys.exe';$iverilog=Join-Path $bin 'iverilog.exe';$vvp=Join-Path $bin 'vvp.exe';$verilator=Join-Path $bin 'verilator_bin.exe'
        $python=$env:PYTHON_EXECUTABLE
    }else{
        $yosys=Join-Path $bin 'yosys';$iverilog=Join-Path $bin 'iverilog';$vvp=Join-Path $bin 'vvp';$verilator=Join-Path $bin 'verilator'
        $python=(Get-Command python3 -ErrorAction Stop).Source
    }

    $rtl=@(
        'hdl/rtl/pa_nand.sv','hdl/rtl/pa_binary.sv','hdl/rtl/pa_binexp.sv',
        'hdl/rtl/pa_therm.sv','hdl/rtl/pa_acquisition_sidecar.sv','hdl/rtl/pa_wrappers.sv'
    )
    $widths=if($Quick){@(4)}else{@(4,6,8)}
    $topPatterns=@(
        'pa_bin_add_w{0}','pa_bin_sub_w{0}','pa_bin_compare_w{0}','pa_bin_mul_w{0}','pa_bin_fu_w{0}',
        'pa_bin_counter_w{0}','pa_bin_fu_registered_w{0}',
        'pa_binexp_compose_w{0}','pa_binexp_checked_compose_w{0}','pa_binexp_cancel_w{0}','pa_binexp_meet_w{0}','pa_binexp_join_w{0}',
        'pa_binexp_divides_w{0}','pa_binexp_valuation_w{0}','pa_binexp_power_w{0}',
        'pa_therm_compose_w{0}','pa_therm_meet_w{0}','pa_therm_join_w{0}','pa_therm_divides_w{0}',
        'pa_therm_validate_w{0}','pa_bin_to_therm_w{0}','pa_therm_to_bin_w{0}',
        'pa_cold_encode_w{0}','pa_vsc_query_w{0}','pa_bin_vsc_w{0}'
    )
    $tops=@(foreach($width in $widths){foreach($pattern in $topPatterns){$pattern -f $width}})
    $caseResults=[System.Collections.Generic.List[object]]::new()

    $analyzerTestLog=Join-Path $outputFull 'analyzer-regression.log'
    $analyzerTests=Invoke-Logged -FileName $python -Arguments @('-m','unittest','hdl/tools/test_analyze_netlist.py') -LogPath $analyzerTestLog
    $analyzerTestsPassed=(-not $analyzerTests.TimedOut -and $analyzerTests.ExitCode -eq 0 -and $analyzerTests.Output -match 'OK\s*$')
    Add-CaseResult -Phase 'ANALYZER_REGRESSION' -Case 'netlist_alias_and_failure_guards' -Passed $analyzerTestsPassed -Log $analyzerTestLog -Detail $(if($analyzerTests.TimedOut){'TIMEOUT'}elseif($analyzerTests.ExitCode -ne 0){"EXIT_$($analyzerTests.ExitCode)"}elseif($analyzerTestsPassed){''}else{'SUCCESS_MARKER_MISSING'})

    $lintDir=Join-Path $outputFull 'lint';New-Item -ItemType Directory -Force -Path $lintDir|Out-Null
    foreach($top in $tops){
        $log=Join-Path $lintDir "$top.log"
        $run=Invoke-Logged -FileName $verilator -Arguments (@('--lint-only','--Wall','--Wno-fatal','--top-module',$top)+$rtl) -LogPath $log
        $pass=(-not $run.TimedOut -and $run.ExitCode -eq 0 -and $run.Output -match 'Verilator')
        Add-CaseResult -Phase 'LINT' -Case $top -Passed $pass -Log $log -Detail $(if($run.TimedOut){'TIMEOUT'}elseif($run.ExitCode -ne 0){"EXIT_$($run.ExitCode)"}else{''})
    }

    $simDir=Join-Path $outputFull 'simulation';New-Item -ItemType Directory -Force -Path $simDir|Out-Null
    $simulationCases=@(@{Top='tb_primitives';File='hdl/tb/tb_primitives.sv';Width=$null})
    foreach($width in $widths){foreach($name in @('binary','counter','binexp','checked','therm','sidecar')){$simulationCases+=@{Top="tb_$name";File="hdl/tb/tb_$name.sv";Width=$width}}}
    foreach($simulation in $simulationCases){
        $id=if($null -eq $simulation.Width){$simulation.Top}else{"$($simulation.Top)_w$($simulation.Width)"}
        $vvpFile=Join-Path $simDir "$id.vvp";$compileLog=Join-Path $simDir "$id.compile.log";$runLog=Join-Path $simDir "$id.run.log"
        $arguments=@('-g2012','-Wall','-Wno-timescale','-s',$simulation.Top)
        if($null -ne $simulation.Width){$arguments+="-P$($simulation.Top).W=$($simulation.Width)"}
        $arguments+=@('-o',$vvpFile)+$rtl+@($simulation.File)
        $compile=Invoke-Logged -FileName $iverilog -Arguments $arguments -LogPath $compileLog
        if($compile.ExitCode -eq 0 -and -not $compile.TimedOut){
            $execute=Invoke-Logged -FileName $vvp -Arguments @($vvpFile) -LogPath $runLog
            $pass=(-not $execute.TimedOut -and $execute.ExitCode -eq 0 -and $execute.Output -match "PASS $($simulation.Top)")
            Add-CaseResult -Phase 'SIMULATION' -Case $id -Passed $pass -Log $runLog -Detail $(if($execute.TimedOut){'TIMEOUT'}elseif($execute.ExitCode -ne 0){"EXIT_$($execute.ExitCode)"}else{''})
        }else{
            Add-CaseResult -Phase 'SIMULATION' -Case $id -Passed $false -Log $compileLog -Detail $(if($compile.TimedOut){'COMPILE_TIMEOUT'}else{"COMPILE_EXIT_$($compile.ExitCode)"})
        }
    }

    if(-not $SkipFormal){
        $formalDir=Join-Path $outputFull 'formal';New-Item -ItemType Directory -Force -Path $formalDir|Out-Null
        foreach($width in $widths){foreach($family in @('binary','binexp','checked','therm','sidecar')){
            $top="formal_$($family)_w$width";$formalSource="hdl/formal/formal_$family.sv";$log=Join-Path $formalDir "$top.log"
            $sources=($rtl+@($formalSource)|ForEach-Object{Yosys-Quote $_}) -join ' '
            $command="read_verilog -formal -sv $sources; prep -top $top -flatten; chformal -lower; opt_clean; sat -verify -prove-asserts -set-assumes -set-def-inputs -show-inputs -show-outputs"
            $run=Invoke-Logged -FileName $yosys -Arguments @('-Q','-p',$command) -LogPath $log
            $pass=(-not $run.TimedOut -and $run.ExitCode -eq 0 -and $run.Output -match 'SAT proof finished - no model found: SUCCESS!' -and $run.Output -match 'End of script\.')
            Add-CaseResult -Phase 'FORMAL' -Case $top -Passed $pass -Log $log -Detail $(if($run.TimedOut){'TIMEOUT'}elseif($run.ExitCode -ne 0){"EXIT_$($run.ExitCode)"}elseif($pass){''}else{'SUCCESS_MARKER_MISSING'})
        }}
    }

    $metricObjects=[System.Collections.Generic.List[object]]::new()
    if(-not $SkipSynthesis){
        $synthDir=Join-Path $outputFull 'synthesis';New-Item -ItemType Directory -Force -Path $synthDir|Out-Null
        $sourceText=($rtl|ForEach-Object{Yosys-Quote $_}) -join ' '
        $notMap=Yosys-Quote 'hdl/synth/not_to_nand.v'
        foreach($top in $tops){foreach($mode in @('declared','optimized')){
            $json=Join-Path $synthDir "$top.$mode.json";$log=Join-Path $synthDir "$top.$mode.log";$metrics=Join-Path $synthDir "$top.$mode.metrics.json"
            $jsonQuoted=Yosys-Quote $json
            if($mode -eq 'declared'){
                $command="read_verilog -sv $sourceText; hierarchy -check -top $top; proc; delete t:`$scopeinfo; check -assert; write_json $jsonQuoted; stat -top $top"
            }else{
                $command="read_verilog -sv $sourceText; hierarchy -check -top $top; proc; flatten; opt; memory_map; techmap; opt; dffunmap; abc -g NAND; techmap -map $notMap; opt_clean; delete t:`$scopeinfo; check -assert; write_json $jsonQuoted; stat -top $top"
            }
            $run=Invoke-Logged -FileName $yosys -Arguments @('-Q','-p',$command) -LogPath $log
            $completion=(-not $run.TimedOut -and $run.ExitCode -eq 0 -and $run.Output -match 'End of script\.' -and (Test-Path -LiteralPath $json -PathType Leaf))
            if($mode -eq 'optimized'){$completion=$completion -and $run.Output -match 'Re-integrating ABC results\.' -and $run.Output -match 'ABC RESULTS:'}
            if($completion){
                $analysisLog=$metrics+'.log'
                $analyze=Invoke-Logged -FileName $python -Arguments @('hdl/tools/analyze_netlist.py','--input',$json,'--output',$metrics,'--top',$top,'--mode',$mode) -LogPath $analysisLog
                $completion=(-not $analyze.TimedOut -and $analyze.ExitCode -eq 0 -and (Test-Path -LiteralPath $metrics -PathType Leaf))
                if($completion){$metricObjects.Add((Get-Content -Raw -LiteralPath $metrics|ConvertFrom-Json))}
            }
            Add-CaseResult -Phase "SYNTHESIS_$($mode.ToUpperInvariant())" -Case $top -Passed $completion -Log $log -Detail $(if($run.TimedOut){'TIMEOUT'}elseif($run.ExitCode -ne 0){"EXIT_$($run.ExitCode)"}elseif(-not ($run.Output -match 'End of script\.')){'COMPLETION_MARKER_MISSING'}elseif($mode -eq 'optimized' -and -not ($run.Output -match 'ABC RESULTS:')){'ABC_COMPLETION_MARKER_MISSING'}elseif(-not $completion){'NETLIST_VALIDATION_FAILED'}else{''})
        }}
    }

    $metricsCsv=Join-Path $outputFull 'synthesis-metrics.csv'
    if($metricObjects.Count -gt 0){
        $rows=$metricObjects|Sort-Object top,evidence_class|Select-Object protocol,top,evidence_class,nand2_static,dff_static,state_bits,input_bits,output_bits,port_bits,wire_bits,connections_static,max_fanout,cross_lane_connections,unit_nand_critical_depth,combinational_loop_status,netlist_sha256,validation_status
        Write-Utf8 -Path $metricsCsv -Content ((($rows|ConvertTo-Csv -NoTypeInformation) -join [Environment]::NewLine)+[Environment]::NewLine)
    }
    $failed=@($caseResults|Where-Object{$_.status -ne 'PASS'})
    $summary=[ordered]@{
        schema='prime-axiom-build002-hdl-verification-v1';protocol=$protocol;scope=$(if($Quick){'QUICK_W4'}else{'FULL_W4_W6_W8'});
        status=$(if($failed.Count -eq 0){'PASS'}else{'FAIL'});total_cases=$caseResults.Count;passed_cases=$caseResults.Count-$failed.Count;failed_cases=$failed.Count;cases=@($caseResults)
    }
    $summaryPath=Join-Path $outputFull 'verification-summary.json';Write-Json -Value $summary -Path $summaryPath
    $manifestFiles=@(Get-ChildItem -LiteralPath $outputFull -File -Recurse|Where-Object{$_.Name -ne 'manifest.json'}|ForEach-Object{
        [ordered]@{path=[System.IO.Path]::GetRelativePath($outputFull,$_.FullName).Replace('\','/');sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant();bytes=$_.Length}
    }|Sort-Object path)
    Write-Json -Value ([ordered]@{schema='prime-axiom-build002-hdl-artifact-manifest-v1';protocol=$protocol;files=$manifestFiles}) -Path (Join-Path $outputFull 'manifest.json')
    if($failed.Count -gt 0){throw "Build 002 HDL verification failed in $($failed.Count)/$($caseResults.Count) cases. See $(Relative-Path $summaryPath)."}
    Write-Host "Build 002 HDL verification passed: $($caseResults.Count)/$($caseResults.Count) cases; widths $($widths -join ',')."
} finally {
    Pop-Location
}
