# Codex Update Center

### Less friction when updating Codex on Windows.

**Find your installation, prepare a verified official update, and confirm what actually installed — in one window.**

[![Windows build](https://github.com/son-gladyso/codex-update-center/actions/workflows/build.yml/badge.svg?branch=main)](https://github.com/son-gladyso/codex-update-center/actions/workflows/build.yml)

[Download the Windows preview](https://github.com/son-gladyso/codex-update-center/releases) · [中文](../README.md) · [Report a problem](https://github.com/son-gladyso/codex-update-center/issues/new/choose)

Windows x64 · Self-contained ZIP · MIT · Independent community project, not affiliated with OpenAI

![Installation selection, verified update status and explicit installation confirmation](images/update-center.png)

*Actual application with synthetic example data. The interface is currently Chinese; this image is not evidence of an account migration.*

## Built for the awkward parts of updating

- **Unsure which installation to update?** Discover official current-user MSIX installations and standard npm CLI prefixes. Multiple installations stay separate; update one selected target.
- **Interrupted downloads?** Keep checkpoints and reuse reverified official packages. Large CLI native packages use resumable 1 MiB ranges.
- **Still working?** Preparation and installation are separate. Installation requires confirmation and running-process checks; the tool never force-stops Codex.
- **“Success” but the CLI does not run?** Rediscover the installed version and execute the CLI version command before reporting success.
- **On a hotspot or short on disk space?** Block large downloads on metered or unknown-cost networks unless explicitly allowed. Check space budgets and prefer C only when reserves fit.

No API key required. The updater does not read account tokens or chat databases, collect telemetry, or replace your launcher configuration. Official package signatures and content hashes are verified before installation.

## Start in four steps

1. Download `CodexUpdateCenter-*-win-x64.zip` from [Releases](https://github.com/son-gladyso/codex-update-center/releases) and check `SHA256SUMS.txt`.
2. Extract to a writable folder and run `CodexUpdateCenter.exe`. The .NET runtime is included.
3. Select an installation and prepare its update. A single supported installation is selected and prepared automatically by default; this option can be disabled.
4. Save your work, exit that Codex installation normally, then confirm installation. Review the verified resulting version.

Custom npm prefixes can be added. Portable copies, custom multi-instance launchers, WSL, ARM64 and linked Node version-manager layouts are not automatically updated.

## Honest preview boundaries

This is a **community preview**, without commercial Authenticode signing. Do not disable Windows security to run it. The clean-Windows deployment matrix, physical accessibility testing and some proxy/network cases remain unverified.

A fresh isolated CLI installation was upgraded from 0.161.0 to 0.162.1 and executed successfully, using reverified official packages and cached npm metadata. This is not a cold-cache network result. The official desktop MSIX passed signature and block-hash validation; actual Windows deployment has not been accepted yet.

npm installation is not atomic: failures may require npm repair. The tool reports unconfirmed results and does not promise automatic rollback. It uses the effective system proxy with TLS validation, without controlling VPN nodes or promising maximum download speed.

State: `%LOCALAPPDATA%\CodexUpdateCenter\Public`. Cache locations are shown in the task view. Removing this updater does not uninstall Codex.

## Maintainable by design

71 program tests, 10 release-tool checks, isolated native acceptance, locked dependencies, source privacy audits, per-file release manifests and Windows CI. Evidence scopes are separate: see [validation](VALIDATION.md), [support](SUPPORT.md) and [GitHub Actions](https://github.com/son-gladyso/codex-update-center/actions).

Build on Windows x64 with PowerShell 7 and the SDK in `global.json`:

```powershell
pwsh -File scripts/build.ps1 -Output artifacts/release-local
```

[Architecture](ARCHITECTURE.md) · [Contributing](../CONTRIBUTING.md) · [Release process](RELEASING.md) · [Security](../SECURITY.md)

Tell us your installation type, the step that got in your way, and the outcome you expected. Never attach credentials, chats or raw account logs. Small reproducible fixes and clearer failure messages are especially welcome.

MIT covers this tool's source. Codex packages are downloaded from official sources at runtime and retain their own terms. See [NOTICE](../NOTICE.md).
