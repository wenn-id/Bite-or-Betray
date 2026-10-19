# Installs the automatic Unity runner on the owner's laptop (no admin needed):
#   - a separate CI copy of the project in ~\bob-ci\repo (Library and LFS objects copied from the owner's project to save time),
#   - Task Scheduler "BiteOrBetray Unity CI" every 2 hours while the laptop is on, running unity_runner.py,
#   - Task Scheduler "BiteOrBetray Health" every hour, running health.py.
# Run again after changing unity_runner.py or health.py:  powershell -ExecutionPolicy Bypass -File Tools\Autopilot\install_unity_runner.ps1
param([string]$Source = (Resolve-Path "$PSScriptRoot\..\..").Path)
$ErrorActionPreference = "Stop"
$CI = Join-Path $HOME "bob-ci"
$Repo = Join-Path $CI "repo"
New-Item -ItemType Directory -Force $CI, "$CI\logs", "$CI\bin" | Out-Null

if (-not (Test-Path "$Repo\.git")) {
    $env:GIT_LFS_SKIP_SMUDGE = "1"
    git clone -q $Source $Repo
    git -C $Repo remote set-url origin https://github.com/wenn-id/Bite-or-Betray.git
    robocopy "$Source\.git\lfs\objects" "$Repo\.git\lfs\objects" /E /NFL /NDL /NJH /NJS /NP | Out-Null
    git -C $Repo fetch -q origin
    git -C $Repo checkout -q -B develop origin/develop
    git -C $Repo lfs checkout
    git -C $Repo lfs pull
    Remove-Item Env:GIT_LFS_SKIP_SMUDGE
    # The owner's imported Library: the first run does not have to reimport every asset. Skipped while the owner's
    # project is open in Unity (copying a Library in use can corrupt it); the runner then imports on its own.
    $open = Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" | Where-Object { $_.CommandLine -like "*$Source*" }
    if ($open) { Write-Host "The owner's project is open in Unity: Library not copied, the first CI run will import on its own." }
    else { robocopy "$Source\Library" "$Repo\Library" /E /MT:8 /NFL /NDL /NJH /NJS /NP | Out-Null }
}

Copy-Item "$PSScriptRoot\unity_runner.py" "$CI\bin\unity_runner.py" -Force
$python = (Get-Command python).Source
$pythonw = Join-Path (Split-Path $python) "pythonw.exe"
if (-not (Test-Path $pythonw)) { $pythonw = $python }

$action = New-ScheduledTaskAction -Execute $pythonw -Argument "`"$CI\bin\unity_runner.py`"" -WorkingDirectory $CI
$trigger = New-ScheduledTaskTrigger -Once -At ((Get-Date).Date.AddHours(8).AddMinutes(30)) -RepetitionInterval (New-TimeSpan -Hours 2)
$settings = New-ScheduledTaskSettingsSet -StartWhenAvailable -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries `
    -ExecutionTimeLimit (New-TimeSpan -Hours 6) -MultipleInstances IgnoreNew
Register-ScheduledTask -TaskName "BiteOrBetray Unity CI" -Action $action -Trigger $trigger -Settings $settings -Force `
    -Description "Checks Bite or Betray develop with Unity (EditMode/PlayMode tests, Quest APK build, shaders) every 2 hours; see Docs/Autopilot/CI.md" | Out-Null

# Hourly worker health check: issues bob-alert/bob-status in the engineering repo, read by Cue (Manus cron).
Copy-Item "$PSScriptRoot\health.py" "$CI\bin\health.py" -Force
$haction = New-ScheduledTaskAction -Execute $pythonw -Argument "`"$CI\bin\health.py`"" -WorkingDirectory $CI
$htrigger = New-ScheduledTaskTrigger -Once -At ((Get-Date).Date.AddMinutes(5)) -RepetitionInterval (New-TimeSpan -Hours 1)
$hsettings = New-ScheduledTaskSettingsSet -StartWhenAvailable -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries `
    -ExecutionTimeLimit (New-TimeSpan -Minutes 15) -MultipleInstances IgnoreNew
Register-ScheduledTask -TaskName "BiteOrBetray Health" -Action $haction -Trigger $htrigger -Settings $hsettings -Force `
    -Description "Checks the VPS agents, blocked tasks, CI and the runner; reports to issues bob-alert/bob-status (read by Cue)" | Out-Null
Get-ScheduledTask -TaskName "BiteOrBetray Unity CI", "BiteOrBetray Health" | Select-Object TaskName, State
