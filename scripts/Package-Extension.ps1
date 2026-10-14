param([string]$OutputDirectory = (Join-Path $PSScriptRoot '../artifacts'))
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$source = Join-Path $root 'browser-extension'
$version = (Get-Content (Join-Path $source 'manifest.json') -Raw | ConvertFrom-Json).version
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$output = Join-Path $OutputDirectory "OpenDownload-Extension-$version.zip"
$files = @('manifest.json','background.js','popup.html','popup.js','popup.css','icons') | ForEach-Object { Join-Path $source $_ }
Compress-Archive -LiteralPath $files -DestinationPath $output -Force
Write-Output $output
