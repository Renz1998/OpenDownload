# OpenDownload

A free, open-source download manager for Windows 11. A familiar toolbar, category sidebar and download table, with an independent transfer engine and Chrome/Edge extension. No trial, account, ads or subscription.

**Version 0.1.0 is an initial release, not full IDM parity.** OpenDownload has its own branding, source code and browser helper. It does not replace IDM's helper, reset its trial, or redistribute its extension.

## Features

- HTTP/HTTPS downloads with up to eight connections when the server supports validated byte ranges.
- Pause/resume, restart, retry after transient connection failure, and crash recovery.
- Shared bandwidth cap, concurrent download limit and daily queue scheduling, including windows spanning midnight.
- Batch URLs, filename search, categories, progress/connection details, tray controls and completion notifications.
- SHA-256 calculation, collision-safe filenames and no overwrite of completed files.
- SQLite history; URLs and authentication headers encrypted with Windows DPAPI for the current user.
- Manual Bearer/Authorization and Cookie headers for authenticated downloads; origin-sensitive redirects.
- Chrome/Edge Manifest V3 extension: link/media context menu, selected URL batches, URL popup, connection check and optional download capture limited to specified hosts.
- Self-contained Windows x64 package and per-user browser registration without administrator rights.

## Run

Extract a release ZIP to a permanent writable folder and launch `OpenDownload.Desktop.exe`. The .NET runtime is included. Keep the helper EXE and other package files alongside the app.

Default files go to `Downloads\OpenDownload`. Change the location, speed limit, connection count and schedule in **Options / Scheduler**. Your settings and encrypted history live at `%LOCALAPPDATA%\OpenDownload`. Set `OPENDOWNLOAD_DATA` before launch to use another local data folder, or put its absolute path in `data-path.txt` beside the app. The environment variable takes priority. Keep that machine-specific file out of shared release packages.

**Closing the window keeps the app in the tray by default.** Use the tray menu's **Exit** to stop it, or disable close-to-tray in Options. Downloads need Windows awake and the app running; there is no Windows service or automatic startup task.

To install a desktop shortcut, optionally run `scripts\Install.ps1` from the extracted release. Script execution depends on your existing Windows execution policy; no policy change is necessary to launch the EXE directly.

## Chrome / Edge setup

1. Open `chrome://extensions` or `edge://extensions`.
2. Enable **Developer mode**, select **Load unpacked**, and choose the release's `browser-extension` folder.
3. Copy the extension ID displayed by the browser.
4. In OpenDownload, click **Browser setup**, paste the ID and click **Register browser helper**.
5. Open the extension popup and choose **Check connection**. You should see “Connected to OpenDownload.”

Registration creates only `org.opendownload.app` under your current-user Chrome, Edge and Chromium native-messaging registry keys. It does not modify IDM. The extension sends messages to the helper, which forwards them through a same-user named pipe to the desktop app. It can start the app when needed.

Right-click a link, image or directly accessible video/audio file and select **Download with OpenDownload**. Alternatively paste a URL into the popup. Selected text can contain one or more complete URLs. This version does not inject a floating video panel into websites.

**Automatic capture is off by default.** Enable it only for downloads you want to hand over. A comma-separated host list restricts capture to exact hostnames. Private/incognito downloads are excluded. If the desktop handoff fails, the extension attempts to resume the browser download. Acceptance means the app saved the request, not that the server has authorized it or the transfer will succeed. Downloads requiring cookies may fail after handoff; use manual downloading for those sites.

No browsing-history collection, automatic cookie extraction or broad website-read permission is used. Unpacked extensions are a development distribution; browser-store distribution and store reviews are separate work.

## Resume and integrity

The engine checks ETag (or Last-Modified), length and range support before resuming, and checks each partial response. Changed sources, invalid ranges and ignored resume requests stop safely. Servers without a usable validator or ranges use one connection; a stopped transfer from such a server may require **Restart**. **Replace URL** lets you supply a fresh signed link for the same file, but resume still requires matching metadata.

Writes are flushed before persisted offsets advance. Completed files receive a SHA-256 digest, visible in the details area. This calculated digest detects differences when compared to a trusted publisher checksum; it is not independent proof of authenticity. Free-space checks are approximate on network drives and when other applications consume space concurrently; write failures remain errors rather than completed downloads.

**Remove** discards an entry and its unfinished partial file after confirmation. **Clear completed** removes history only. Completed downloaded files are kept. Restart discards the selected incomplete file after confirmation.

## Build and test

Install .NET SDK 10.0.401 and Node.js 24 or newer, then:

```powershell
./scripts/Build.ps1 -Publish
```

Or run the commands individually:

```powershell
dotnet build src/OpenDownload.Desktop -c Release
dotnet build src/OpenDownload.NativeHost -c Release
dotnet run --project tests/OpenDownload.Tests -c Release -- .
```

The test harness starts a localhost-only Node HTTP fixture. It checks exact file hashes for segmented and single-stream downloads, pause/serialization/resume, connection resets, unknown-length responses, redirects and authentication; also changed-source refusal, invalid ranges, HTTP errors, empty files, no overwrite, bandwidth limits, schedule windows and safe filenames. No real user URLs or external download traffic are needed.

There are 19 transfer tests, seven Windows persistence tests and seven extension behavior tests. Transfer tests include forcibly terminating a child download process and recovering its durable checkpoint, and checking that cross-origin redirects strip credentials. The Windows suite checks DPAPI encryption and recovery around the final file rename. Extension tests use a mocked browser API; actual browser installation still requires the setup steps above.

`tests/native-smoke.py` also tests the published app and binary native-messaging protocol end to end. It needs Python 3 and a fresh test data directory, and must run with other OpenDownload instances closed:

```powershell
python tests/native-smoke.py --app artifacts/OpenDownload --data test-results/native-smoke --root .
```

Release ZIPs and checksums are written to `artifacts/`. GitHub Actions builds, tests and uploads the Windows package. The first successful main-branch build publishes the `v0.1.0` preview release. Existing release assets are preserved; later version releases require a new `v*` tag and corresponding package version update. Packages are currently unsigned; commercial code signing is not configured.

## Scope and roadmap

Implemented: normal downloads, direct media-file URLs, manual authentication, desktop queue/scheduler and an independent browser extension.

Not implemented: IDM's proprietary extension protocol, arbitrary website video extraction, HLS/DASH assembly, DRM-protected media, FTP, recursive site crawling, multiple named queues, automatic cookie/token extraction, automatic URL refresh, automatic application updates or browser-store publishing. Server limits still apply; extra connections do not guarantee faster downloads.

IDM extension feasibility: the locally installed `IDMMsgHost.json` registers `com.tonec.idm` with an IDM-specific executable and extension IDs. No documented replacement-app protocol was identified. OpenDownload intentionally registers a separate helper. No proprietary extension source or binaries are included in this project.

## Contributing and privacy

Do not commit local databases, partial files, signed download links, access tokens, cookies, browser profiles or personal logs. The repository ignores generated builds, download history and test output. DPAPI protects local records for the current Windows account; moving the database to a different account will not decrypt it.

The app performs no telemetry and stores no credentials on GitHub. Report bugs with test URLs and redacted reproduction steps. Dependencies and their licenses are listed in `THIRD-PARTY.md`.

## License

MIT. See `LICENSE`. IDM is a trademark of its respective owner; this project is independent and unaffiliated.
