$ErrorActionPreference = 'Stop'
foreach ($browser in @('Google\Chrome','Microsoft\Edge','Chromium')) {
    $key = "HKCU:\Software\$browser\NativeMessagingHosts\org.opendownload.app"
    if (Test-Path -LiteralPath $key) { Remove-Item -LiteralPath $key }
}
Write-Output 'OpenDownload browser registration removed. Other download managers were not modified.'
