# Pasang runner Unity otomatis di laptop owner (tanpa admin):
#   - salinan project CI terpisah di ~\bob-ci\repo (Library dan objek LFS disalin dari project owner supaya cepat),
#   - Task Scheduler "BiteOrBetray Unity CI" tiap 2 jam selama laptop menyala, menjalankan unity_runner.py.
# Jalankan ulang setelah unity_runner.py diubah:  powershell -ExecutionPolicy Bypass -File Tools\Autopilot\install_unity_runner.ps1
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
    # Library hasil import project owner: run pertama tidak perlu import ulang semua aset. Dilewati kalau project
    # owner sedang terbuka di Unity (salinan Library yang sedang dipakai bisa rusak); runner lalu import sendiri.
    $open = Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" | Where-Object { $_.CommandLine -like "*$Source*" }
    if ($open) { Write-Host "Project owner sedang terbuka di Unity: Library tidak disalin, run CI pertama akan import sendiri." }
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
    -Description "Cek develop Bite or Betray dengan Unity (EditMode, build APK Quest, shader) tiap 2 jam; lihat Docs/Autopilot/CI.md" | Out-Null

# Pengecek kesehatan worker tiap jam: issue bob-alert/bob-status di repo engineering, dibaca Cue (cron Manus).
Copy-Item "$PSScriptRoot\health.py" "$CI\bin\health.py" -Force
$haction = New-ScheduledTaskAction -Execute $pythonw -Argument "`"$CI\bin\health.py`"" -WorkingDirectory $CI
$htrigger = New-ScheduledTaskTrigger -Once -At ((Get-Date).Date.AddMinutes(5)) -RepetitionInterval (New-TimeSpan -Hours 1)
$hsettings = New-ScheduledTaskSettingsSet -StartWhenAvailable -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries `
    -ExecutionTimeLimit (New-TimeSpan -Minutes 15) -MultipleInstances IgnoreNew
Register-ScheduledTask -TaskName "BiteOrBetray Health" -Action $haction -Trigger $htrigger -Settings $hsettings -Force `
    -Description "Cek agent VPS, tugas blocked, CI dan runner; lapor ke issue bob-alert/bob-status (dibaca Cue)" | Out-Null
Get-ScheduledTask -TaskName "BiteOrBetray Unity CI", "BiteOrBetray Health" | Select-Object TaskName, State
