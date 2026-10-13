# Dependencies

OpenDownload's original code is MIT licensed. Runtime and dependency licenses remain their own.

- Microsoft .NET / WPF / Windows Forms: MIT; https://github.com/dotnet/runtime and https://github.com/dotnet/wpf
- Microsoft.Data.Sqlite 10.0.13 and Microsoft.Data.Sqlite.Core: MIT; https://github.com/dotnet/efcore
- SQLitePCLRaw.bundle_e_sqlite3 3.0.5 and transitive managed components: Apache-2.0; https://github.com/ericsink/SQLitePCL.raw
- SQLite 3.53.4 native runtime package: see `licenses/SQLite-LICENSE.txt` and https://www.sqlite.org/copyright.html

Release packages include .NET license and third-party notices, Apache-2.0 terms and the SQLite package license in `licenses/`. SQLitePCLRaw: Copyright 2014-2025 SourceGear, LLC. Microsoft .NET and Microsoft.Data.Sqlite: Copyright .NET Foundation and Contributors. The build also copies available dependency license and notice files from restored NuGet packages. Node.js is a development/test dependency and is not bundled with the desktop app. No IDM code, images, native helper, browser extension or license system is distributed.
