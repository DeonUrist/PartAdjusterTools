param(
    [Parameter(Mandatory = $true)][string]$GameDir,
    [string]$Report = "$PSScriptRoot\..\..\research\native-regression.txt"
)

$ErrorActionPreference = 'Stop'
$GameDir = (Resolve-Path -LiteralPath $GameDir).Path
$Report = [IO.Path]::GetFullPath($Report)
$repo = (Resolve-Path -LiteralPath "$PSScriptRoot\..\..").Path
$installed = Join-Path $GameDir 'BepInEx\plugins\PartAdjustment\PartAdjustment.dll'
$temporary = Join-Path $GameDir 'BepInEx\plugins\PartAdjustmentNativeVerifier.dll'
$log = Join-Path $GameDir 'BepInEx\LogOutput.log'
$backup = $null
$started = $null

if (Get-Process Apocalypter -ErrorAction SilentlyContinue) { throw 'Close Apocalypter before running the native verifier.' }
if (Test-Path -LiteralPath $temporary) { throw "A verifier is already installed at $temporary." }

dotnet build "$repo\PartAdjustment.csproj" -c Release "-p:GameDir=$GameDir"
if ($LASTEXITCODE -ne 0) { throw 'Plugin build failed.' }
dotnet build "$PSScriptRoot\NativeVerifier.csproj" -c Release "-p:GameDir=$GameDir"
if ($LASTEXITCODE -ne 0) { throw 'Verifier build failed.' }
if ((Get-FileHash -LiteralPath $installed).Hash -ne (Get-FileHash -LiteralPath "$repo\bin\Release\PartAdjustment.dll").Hash) {
    throw 'Install the newly built production DLL before running this verifier.'
}

New-Item -ItemType Directory -Path ([IO.Path]::GetDirectoryName($Report)) -Force | Out-Null
if (Test-Path -LiteralPath $Report) { Remove-Item -LiteralPath $Report }
try {
    if (Test-Path -LiteralPath $log) {
        $backup = [IO.Path]::GetTempFileName()
        Copy-Item -LiteralPath $log -Destination $backup
    }
    Copy-Item -LiteralPath "$PSScriptRoot\bin\Release\PartAdjustmentNativeVerifier.dll" -Destination $temporary
    $arguments = @('-batchmode', '-nographics', '-logFile', ('"' + $Report + '.unity.log"'), ('-partadjustment-regression-report="' + $Report + '"'))
    $started = Start-Process -FilePath "$GameDir\Apocalypter.exe" -WorkingDirectory $GameDir -WindowStyle Hidden -ArgumentList $arguments -PassThru
    if (-not $started.WaitForExit(60000)) { throw 'Native regression run exceeded 60 seconds.' }
    $result = Get-Content -LiteralPath $Report
    $result
    if (-not ($result -match '^PASS:') -or ($result -match '^ERROR:')) { throw 'Native regression checks failed.' }
}
finally {
    if ($started -and -not $started.HasExited) { Stop-Process -Id $started.Id }
    if (Test-Path -LiteralPath $temporary) { Remove-Item -LiteralPath $temporary }
    if ($backup) {
        Copy-Item -LiteralPath $backup -Destination $log
        Remove-Item -LiteralPath $backup
    }
}
