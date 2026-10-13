param([switch]$Publish, [string]$DotNet = 'dotnet')
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Push-Location $root
try {
    $version = ([xml](Get-Content Directory.Build.props -Raw)).Project.PropertyGroup.Version
    & $DotNet build src/OpenDownload.Desktop/OpenDownload.Desktop.csproj -c Release --nologo
    if ($LASTEXITCODE -ne 0) { throw 'Desktop build failed' }
    & $DotNet build src/OpenDownload.NativeHost/OpenDownload.NativeHost.csproj -c Release --nologo
    if ($LASTEXITCODE -ne 0) { throw 'Native host build failed' }
    & $DotNet run --project tests/OpenDownload.Tests -c Release -- $root
    if ($LASTEXITCODE -ne 0) { throw 'Transfer tests failed' }
    & $DotNet run --project tests/OpenDownload.WindowsTests -c Release -- $root
    if ($LASTEXITCODE -ne 0) { throw 'Windows persistence tests failed' }
    node tests/extension.test.cjs
    if ($LASTEXITCODE -ne 0) { throw 'Extension behavior tests failed' }
    node --check browser-extension/background.js
    if ($LASTEXITCODE -ne 0) { throw 'Extension syntax check failed' }
    node --check browser-extension/popup.js
    if ($LASTEXITCODE -ne 0) { throw 'Popup syntax check failed' }
    if ($Publish) {
        $out = Join-Path $root 'artifacts/OpenDownload'
        New-Item -ItemType Directory -Path $out -Force | Out-Null
        & $DotNet publish src/OpenDownload.Desktop -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -o $out
        if ($LASTEXITCODE -ne 0) { throw 'Desktop publish failed' }
        & $DotNet publish src/OpenDownload.NativeHost -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -o $out
        if ($LASTEXITCODE -ne 0) { throw 'Host publish failed' }
        Copy-Item browser-extension -Destination $out -Recurse -Force
        Copy-Item scripts -Destination $out -Recurse -Force
        Copy-Item README.md,LICENSE -Destination $out -Force
        Copy-Item THIRD-PARTY.md -Destination $out -Force
        Copy-Item docs -Destination $out -Recurse -Force
        $env:OPENDOWNLOAD_TEST_MODE = '1'
        $env:OPENDOWNLOAD_DATA = Join-Path $root ('test-results/ui-' + [guid]::NewGuid().ToString('N'))
        $uiProcess = Start-Process -FilePath (Join-Path $out 'OpenDownload.Desktop.exe') -ArgumentList '--ui-self-test',("`"" + (Join-Path $root 'docs/images') + "`"") -WindowStyle Hidden -PassThru
        if (!$uiProcess.WaitForExit(60000)) { throw 'UI test timed out' }
        if ($uiProcess.ExitCode -ne 0 -or (Test-Path docs/images/ui-failure.txt)) { throw 'UI tests failed; see docs/images/ui-failure.txt' }
        Get-Content docs/images/ui-results.txt
        Remove-Item Env:OPENDOWNLOAD_TEST_MODE
        Remove-Item Env:OPENDOWNLOAD_DATA
        Copy-Item docs -Destination $out -Recurse -Force
        $licenses = Join-Path $out 'licenses'
        New-Item -ItemType Directory -Path $licenses -Force | Out-Null
        Copy-Item -Path 'licenses/*' -Destination $licenses -Force
        $cache = if ($env:NUGET_PACKAGES) { $env:NUGET_PACKAGES } else { Join-Path $env:USERPROFILE '.nuget/packages' }
        foreach ($package in @('microsoft.data.sqlite','microsoft.data.sqlite.core','sqlitepclraw.bundle_e_sqlite3','sqlitepclraw.core','sqlitepclraw.provider.e_sqlite3','sqlitepclraw.lib.e_sqlite3','sqlite')) {
            $packageDir = Join-Path $cache $package
            if (Test-Path -LiteralPath $packageDir) {
                foreach ($file in (Get-ChildItem -LiteralPath $packageDir -Recurse -File | Where-Object { $_.Name -match '(?i)license|notice' })) {
                    Copy-Item -LiteralPath $file.FullName -Destination (Join-Path $licenses ($package + '-' + $file.Directory.Name + '-' + $file.Name)) -Force
                }
            }
        }
        $zip = Join-Path $root ("artifacts/OpenDownload-$version-win-x64.zip")
        Compress-Archive -Path "$out/*" -DestinationPath $zip -Force
        & $DotNet publish src/OpenDownload.Setup -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o artifacts/setup
        if ($LASTEXITCODE -ne 0) { throw 'Installer publish failed' }
        $setup = Join-Path $root ("artifacts/OpenDownload-$version-Setup.exe")
        Copy-Item artifacts/setup/OpenDownload.Setup.exe $setup -Force
        @($zip, $setup) | ForEach-Object { (Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash.ToLowerInvariant() + '  ' + (Split-Path $_ -Leaf) } | Set-Content (Join-Path $root 'artifacts/SHA256SUMS.txt')
        $version | Set-Content artifacts/version.txt
    }
} finally { Pop-Location }
