# Windows release signing

These releases remain unsigned until the publisher supplies a publicly trusted code-signing certificate or signing-service credentials. No self-signed certificate is installed or presented as trusted signing.

For a certificate available in CurrentUser/My with its private key, install the Windows SDK signing tools and build using:

```powershell
./scripts/Build.ps1 -Publish -SigningThumbprint '<certificate thumbprint>' -SignTool '<absolute path to signtool.exe>'
```

The build signs and verifies the desktop and native helper before packaging, signs the installer, and only then creates final checksums. The script uses SHA-256 for file and timestamp digests and fails if verification fails. Hardware tokens or cloud services may require provider-specific signing; do not export private keys or put signing credentials in the repository.

A trusted signature identifies the publisher but does not promise that Windows will never display a reputation warning. Store account setup, certificate purchase and provider authentication are publisher actions.

[Microsoft SignTool documentation](https://learn.microsoft.com/en-us/windows/win32/seccrypto/signtool).
