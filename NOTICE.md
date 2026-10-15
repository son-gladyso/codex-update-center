# Third-party notices

Codex Update Center is an independent community project. OpenAI, Codex and ChatGPT are names of their respective owner; their use describes compatibility, not sponsorship or endorsement.

The application has no third-party NuGet runtime packages. Self-contained distributions include Microsoft .NET and Windows Desktop runtime components under their respective licenses. Files prefixed `microsoft.netcore.app.runtime` and `microsoft.windowsdesktop.app.runtime` contain the license and available third-party notices from the exact runtime packages used during packaging. Source and licenses: [dotnet/runtime](https://github.com/dotnet/runtime), [dotnet/wpf](https://github.com/dotnet/wpf).

Development-only dependencies are pinned in `tests/UpdateCenter.Tests/packages.lock.json`: Microsoft.NET.Test.Sdk, xUnit and its Visual Studio runner, and their transitive dependencies. They are not shipped in the application ZIP.

OpenAI application packages and Codex CLI are downloaded directly at runtime. They are not redistributed as project release assets. The updater does not change their licensing or account requirements.
