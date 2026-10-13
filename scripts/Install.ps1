param([string]$Destination = "$env:LOCALAPPDATA\Programs\OpenDownload")
$ErrorActionPreference = 'Stop'
$source = Split-Path -Parent $PSScriptRoot
if (!(Test-Path -LiteralPath (Join-Path $source 'OpenDownload.Desktop.exe'))) { throw 'Run this script from a published OpenDownload package.' }
New-Item -ItemType Directory -Path $Destination -Force | Out-Null
if ((Resolve-Path -LiteralPath $source).Path -ne (Resolve-Path -LiteralPath $Destination).Path) { Copy-Item -Path "$source/*" -Destination $Destination -Recurse -Force }
$shell = New-Object -ComObject WScript.Shell
$shortcut = $shell.CreateShortcut((Join-Path ([Environment]::GetFolderPath('Desktop')) 'OpenDownload.lnk'))
$shortcut.TargetPath = Join-Path $Destination 'OpenDownload.Desktop.exe'
$shortcut.WorkingDirectory = $Destination
$shortcut.Save()
Write-Output "Installed to $Destination. Launch OpenDownload from your desktop. Browser setup is described in README.md."
