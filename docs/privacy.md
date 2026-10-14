# OpenDownload privacy

OpenDownload is a local Windows download manager with an optional Chrome/Edge extension. It has no advertising, analytics or telemetry service.

The extension sends URLs selected by you, or downloads included in your optional automatic-capture settings, to the locally installed native helper. The helper forwards them to the desktop app on the same Windows account. Local extension storage holds capture settings, connection status and error explanations. Incognito downloads are excluded from automatic capture. It does not collect browsing history or automatically read cookies.

The desktop stores download history and settings locally. Download URLs and manually supplied authentication headers are encrypted using Windows DPAPI. Download servers receive the requested URL and necessary HTTP headers to serve a file.

Optional update checks contact GitHub to read public release metadata at most once a day; they can be disabled in Settings. Manual update checks also contact GitHub. These requests include the app version and normal network information such as the requesting IP address, but do not send download history, tokens or local files. The user-invoked browser capture test downloads a tiny public file from GitHub.

Ko-fi and external documentation open only when you click their links and are governed by their own policies. Store accounts and purchases are not part of the app.

Delete downloads through the app if desired. Uninstalling preserves local settings/history and downloaded files. Privacy questions and redacted bug reports can be submitted through https://github.com/Renz1998/OpenDownload/issues; do not include credentials or private download URLs.
