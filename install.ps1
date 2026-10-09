# Cogworks installer for Windows.
#   irm https://raw.githubusercontent.com/EVL2020/cogworks/main/install.ps1 | iex
# Downloads the latest release into %LOCALAPPDATA%\Cogworks (no admin needed), adds Start Menu and
# Desktop shortcuts, and launches the game.
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'   # Invoke-WebRequest is far faster without the progress bar

$repo = 'EVL2020/cogworks'
$dest = Join-Path $env:LOCALAPPDATA 'Cogworks'
$zip = Join-Path $env:TEMP 'Cogworks-Windows.zip'

Write-Host 'Downloading Cogworks for Windows...'
Invoke-WebRequest "https://github.com/$repo/releases/latest/download/Cogworks-Windows.zip" -OutFile $zip
if (Test-Path $dest) { Remove-Item $dest -Recurse -Force }
Expand-Archive $zip -DestinationPath $dest
Remove-Item $zip
# Files downloaded from the internet are tagged; unblock them so SmartScreen doesn't prompt on each launch.
Get-ChildItem $dest -Recurse | Unblock-File

$exe = Join-Path $dest 'Cogworks.exe'
$shell = New-Object -ComObject WScript.Shell
foreach ($dir in @([Environment]::GetFolderPath('Programs'), [Environment]::GetFolderPath('Desktop'))) {
    $link = $shell.CreateShortcut((Join-Path $dir 'Cogworks.lnk'))
    $link.TargetPath = $exe
    $link.WorkingDirectory = $dest
    $link.Save()
}
Write-Host "Installed to $dest (shortcuts on the Desktop and in the Start Menu)"
Start-Process $exe
