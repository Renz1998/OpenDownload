# Architecture

`OpenDownload.Core` contains a UI-independent HTTP engine, job/segment models, filenames, bandwidth limiter and schedule predicate. A shared HttpClient handles explicit redirects, never passes authentication to a different origin, requests identity encoding and validates range metadata.

`OpenDownload.Desktop` is WPF with a Windows Forms tray icon. The dispatcher owns the collection, queue operations, settings and SQLite connection. Transfer work runs in background tasks; cancellation joins those tasks before orderly exit. Persistent data contains committed offsets, rather than optimistic socket progress. Protected URL/header/job payloads are encrypted via current-user DPAPI; settings remain ordinary JSON inside SQLite. URLs are never printed to logs.

The engine serializes no UI state. It saves a SHA-256 checkpoint before the final no-overwrite file rename; the database loader can verify a file after a crash between that rename and the final status commit. Unknown-length and unvalidated sources use a single connection. Resume is refused if source length, validator or range support changed.

`OpenDownload.NativeHost` uses the browser's binary 32-bit little-endian length + UTF-8 JSON protocol. It validates message length and URL scheme and forwards one request over a CurrentUserOnly named pipe. The desktop stores the request before acknowledging it. The helper can start the published desktop executable beside itself. Only JSON protocol frames go to stdout.

`browser-extension` is an MV3 service worker and popup. Native messaging permissions are scoped to `org.opendownload.app`; its host manifest explicitly permits installed extension IDs. No HTTP API listener or browser-cookie scraping is used. Browser interception is off by default and leaves incognito downloads alone.

Tests use a deterministic localhost HTTP server and exact SHA-256 comparisons. Windows tests verify encrypted persistence and crash-boundary recovery. `native-smoke.py` checks the complete published helper → IPC → queue → engine path using isolated test directories and only terminates children it starts.

## Boundaries

The pipe is restricted to the Windows user, not a sandbox against other malicious programs already running as that user. DPAPI prevents another account reading copied database payloads, but malware running as the same account can use DPAPI too. The software does not elevate or install background services. Standard system proxy configuration is used by HttpClient; a custom proxy editor is not implemented.

## Future work

Multiple named queues, store-signed extension distribution, HLS/DASH assembly, website adapters, an optional video panel, explicit credential refresh and application update signing need separate implementations and tests. Unchanged IDM-extension compatibility remains unproven and is outside the initial release.
