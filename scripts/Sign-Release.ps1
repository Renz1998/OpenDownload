param(
    [Parameter(Mandatory)][string[]]$Path,
    [Parameter(Mandatory)][string]$Thumbprint,
    [Parameter(Mandatory)][string]$SignTool,
    [string]$TimestampUrl = 'http://timestamp.digicert.com'
)
$ErrorActionPreference = 'Stop'
if (!(Test-Path -LiteralPath $SignTool -PathType Leaf)) { throw 'Provide the Windows SDK signtool.exe path.' }
$certificate = Get-Item -LiteralPath ("Cert:\CurrentUser\My\" + $Thumbprint)
if (!$certificate.HasPrivateKey -or $certificate.NotAfter -le (Get-Date)) { throw 'An unexpired code-signing certificate with an accessible private key is required.' }
if (!($certificate.EnhancedKeyUsageList.ObjectId -contains '1.3.6.1.5.5.7.3.3')) { throw 'This certificate is not enabled for code signing.' }
foreach ($file in $Path) {
    $target = (Resolve-Path -LiteralPath $file).Path
    & $SignTool sign /sha1 $Thumbprint /fd SHA256 /tr $TimestampUrl /td SHA256 $target
    if ($LASTEXITCODE -ne 0) { throw "Signing failed: $target" }
    & $SignTool verify /pa /v $target
    if ($LASTEXITCODE -ne 0 -or (Get-AuthenticodeSignature -LiteralPath $target).Status -ne 'Valid') { throw "Signature verification failed: $target" }
}
