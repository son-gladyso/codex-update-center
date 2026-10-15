# Codex Update Center

An independent community updater for Windows x64. It discovers supported Codex installations, prepares official updates and updates the selected installation after confirmation. It is not affiliated with OpenAI.

**0.1.1-preview is a preview. The Windows deployment compatibility matrix still requires VM acceptance.**

Download the Windows ZIP from this repository's Releases, verify its SHA-256, extract it and run `CodexUpdateCenter.exe`. The .NET runtime is included. The interface is currently Chinese. No API key is required.

Supported adapters: current-user, official Store-signed `OpenAI.Codex` x64 MSIX packages; stable `@openai/codex` installations under standard Node.js/npm global prefixes. PATH prefixes and the common user npm prefix are discovered automatically; custom prefixes can be added. Multiple installations are listed independently. Standalone binaries, custom launchers, WSL and ARM64 are not automatically updated.

Official HTTPS sources, Windows signature verification, MSIX block hashes, npm SHA-512 and registry signatures are checked. The launcher and Windows native package are both verified. Native packages use resumable 1 MiB ranges, then enter npm's integrity-addressed cache. npm installs the verified archive and exact native version with scripts disabled and the effective system proxy. An actual CLI version probe is required before success.

Downloads prefer C when the full budget and reserve fit. Metered/unknown-cost networks pause large downloads. Download checkpoints survive window closure; manual pauses stay paused. Updates never force-stop Codex. Interrupted installations require rediscovery instead of an automatic retry.

State lives in `%LOCALAPPDATA%\CodexUpdateCenter\Public`. The tool does not read account tokens or chat databases, collect telemetry or change existing launcher configurations. Windows determines the application deployment volume independently from download storage.

Build with Windows x64, PowerShell 7 and the SDK in `global.json`: `pwsh -File scripts/build.ps1`. See [release instructions](RELEASING.md), [validation scope](VALIDATION.md), [contributing](../CONTRIBUTING.md) and [security policy](../SECURITY.md). Code is MIT licensed; downloaded OpenAI software retains its own terms.
