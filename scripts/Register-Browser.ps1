param([Parameter(Mandatory=$true)][ValidatePattern('^[a-p]{32}$')][string]$ExtensionId, [string]$AppDirectory = (Split-Path -Parent $PSScriptRoot))
$ErrorActionPreference = 'Stop'
$hostExe = Join-Path (Resolve-Path -LiteralPath $AppDirectory).Path 'OpenDownload.NativeHost.exe'
if (!(Test-Path -LiteralPath $hostExe)) { throw "Native host not found: $hostExe. Use the published app directory." }
$manifestPath = Join-Path $AppDirectory 'native-host.json'
@{ name='org.opendownload.app'; description='OpenDownload browser integration'; path=$hostExe; type='stdio'; allowed_origins=@("chrome-extension://$ExtensionId/") } | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $manifestPath -Encoding UTF8
foreach ($browser in @('Google\Chrome','Microsoft\Edge','Chromium')) {
    $key = "HKCU:\Software\$browser\NativeMessagingHosts\org.opendownload.app"
    New-Item -Path $key -Force | Out-Null
    Set-Item -LiteralPath $key -Value $manifestPath
}
Write-Output 'Registered OpenDownload for this Windows account. Restart the browser and click Check connection in the extension.'
