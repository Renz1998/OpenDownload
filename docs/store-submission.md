# Browser-store submission

The extension package is prepared for submission, not yet published or approved. Create your own developer account before uploading. No store listing IDs have been issued yet.

Run `scripts/Package-Extension.ps1`. Upload `artifacts/OpenDownload-Extension-<version>.zip`, which contains manifest.json at its root. The installer also needs to be available from the GitHub release.

## Listing draft

Name: OpenDownload Integration

Short description: Send links and optional browser downloads to the free OpenDownload desktop app.

Description: Connect Chrome or Edge to OpenDownload for Windows 11. Right-click a link or image to download it in the desktop app. Optional automatic capture can handle website Download buttons; it is off by default and can be limited to selected websites. Pause, resume and manage files in the desktop app. The extension needs the OpenDownload desktop installer. Browser-only downloads and protected streams remain in the browser. No account, ads or subscription is required by OpenDownload.

Website: https://github.com/Renz1998/OpenDownload

Privacy policy: https://github.com/Renz1998/OpenDownload/blob/main/docs/privacy.md

Support: https://github.com/Renz1998/OpenDownload/issues

Permission explanations: nativeMessaging connects to the local Windows app; downloads performs user-enabled capture and a user-invoked test download; contextMenus adds download actions; storage remembers local settings and connection errors; alarms refreshes local connection status. activeTab is currently unused and is removed from the submission manifest.

Use the original 128px icon from browser-extension/icons. Capture store screenshots from the actual extension using example URLs and no personal downloads, credentials or browser history. Desktop screenshots in docs/images can be used as supporting screenshots, but store artwork requirements should be checked at submission time.

## After the first upload

Record the ID assigned by each store before publishing. Register those exact IDs in the native helper and add them to the desktop's browser status identity checks; the bundled development ID must remain supported. Do not assume the Chrome and Edge store IDs will match. Keep the store-issued public key and update URLs correct for that store. Verify Check connection and Test a real browser download using the store-installed package before switching the app's setup links to store listings.

This requires a release after the store IDs are known. The current setup keeps the working manual installation instructions until listings are live.

Official instructions: [Chrome registration](https://developer.chrome.com/docs/webstore/register/), [Chrome publication](https://developer.chrome.com/docs/webstore/publish/), [Edge publication](https://learn.microsoft.com/en-us/microsoft-edge/extensions/publish/publish-extension).
