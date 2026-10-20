# Codex Update Center

[简体中文](../README.md) · [English](README.en.md) · [日本語](i18n/README.ja.md) · [한국어](i18n/README.ko.md) · [Español](i18n/README.es.md) · [Français](i18n/README.fr.md) · [Deutsch](i18n/README.de.md) · [Português (Brasil)](i18n/README.pt-BR.md) · [Русский](i18n/README.ru.md) · [繁體中文](i18n/README.zh-TW.md)

### Less friction when updating Codex on Windows.

**Find your installation, prepare a verified official update, and confirm what actually installed — in one window.**

[![Windows build](https://github.com/son-gladyso/codex-update-center/actions/workflows/build.yml/badge.svg?branch=main)](https://github.com/son-gladyso/codex-update-center/actions/workflows/build.yml)

[Download the Windows preview](https://github.com/son-gladyso/codex-update-center/releases) · [中文](../README.md) · [Report a problem](https://github.com/son-gladyso/codex-update-center/issues/new/choose)

Windows x64 · Self-contained ZIP · MIT · Independent community project, not affiliated with OpenAI

![Installation selection, verified update status and explicit installation confirmation](images/update-center.png)

*Actual application with synthetic example data. The interface is currently Chinese; this image is not evidence of an update to a real user installation.*

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

## Installation coverage and operational details

| Installation | Support |
| --- | --- |
| Current-user, official Store-signed `OpenAI.Codex` x64 desktop installation | Discover and verify official MSIX, then update through Windows deployment services. Actual Windows deployment still awaits full acceptance. |
| Stable `@openai/codex` global installation under standard Node.js/npm | Discover, verify the main package and Windows native components, and update after confirmation; tested with a real upgrade in an isolated directory. |
| Multiple npm global directories or desktop and CLI side by side | Show them separately and update one at a time; npm prefixes can be added manually. |
| Portable copies, custom multi-instance launchers, WSL, ARM64, linked Node version-manager layouts | No automatic takeover; use the original installation tool. |

The desktop app and CLI are maintained separately; the updater does not insert a standalone CLI into the desktop app. Moving the download cache to another disk does not change the desktop installation location managed by Windows. If Codex is not installed yet, the window provides official installation instructions.

Network cost is rechecked before npm installation. When C has insufficient reserves, eligible internal fixed disks are considered and the cache location is displayed. Download checkpoints survive a pause or window closure; an explicit user pause is not automatically resumed.

A failed, interrupted, or unconfirmed installation asks you to inspect again; an installer merely returning is not success. The tool does not downgrade to an older candidate, migrate accounts or chats, or start a persistent background service. npm index requests can still fail with some proxies; cache and failure records are retained. Inspect state and cache files before cleanup.

The 81 program tests cover changed targets, invalid signatures, resume, concurrent locks, pause, and installation network-cost gates. The 10 release-tool checks cover package tampering, omitted hidden files, out-of-bounds paths, and source privacy boundaries. The isolated CLI upgrade left existing installation manifests unchanged. Independent builds from the source package and extraction/launch of the runtime package have also been verified.

Release gates audit source, restore locked dependencies, run tests, generate self-contained packages, and check manifests. The output directory must be new; ordinary tests do not install or modify Codex. Include your Windows version in feedback and use the [issue or suggestion templates](https://github.com/son-gladyso/codex-update-center/issues/new/choose). See also the [changelog](../CHANGELOG.md). Release archives do not contain Codex software.

## Honest preview boundaries

This is a **community preview**, without commercial Authenticode signing. Do not disable Windows security to run it. The clean-Windows deployment matrix, physical accessibility testing and some proxy/network cases remain unverified.

A fresh isolated CLI installation was upgraded from 0.161.0 to 0.162.1 and executed successfully, using reverified official packages and cached npm metadata. This is not a cold-cache network result. The official desktop MSIX passed signature and block-hash validation; actual Windows deployment has not been accepted yet.

npm installation is not atomic: failures may require npm repair. The tool reports unconfirmed results and does not promise automatic rollback. It uses the effective system proxy with TLS validation, without controlling VPN nodes or promising maximum download speed.

State: `%LOCALAPPDATA%\CodexUpdateCenter\Public`. Cache locations are shown in the task view. Removing this updater does not uninstall Codex.

## Start with an inspection, not an installation

To decide whether the tool fits your environment, first extract it and inspect the discovered installations and current versions. You do not need to confirm installation immediately. Preparation can use the network and download files; a single supported installation is selected and prepared automatically by default, and that option can be disabled.

- To run: Windows x64 and a writable extraction folder. .NET is included; no API key is required.
- To check/prepare: access to official sources, subject to proxy behavior, network-cost gates, and disk reserves.
- To install: save work, exit the selected Codex normally, recheck the target, and confirm only when ready.

**First success means:** you can identify the selected installation type, location, and version and understand its verification or blocking state. An upgrade is successful only after the required post-installation checks confirm it.

## Common questions

**Are the desktop app and CLI one installation?** No. They are discovered and updated separately; a CLI version result is not desktop deployment evidence.

**Why is my portable copy or custom multi-instance setup missing?** Those layouts are outside automatic takeover. Use their original installation tool rather than disguising them as supported installations.

**Why is installation blocked after downloading?** The candidate still needs verification, target confirmation, and applicable running-process/network-cost gates. Download completion is not installation completion.

**Why block downloads when network cost is unknown?** That is the default cost safeguard. Check your network and explicitly allow the current download if appropriate; do not weaken TLS or signature validation.

**Will failure automatically restore the old version?** No rollback guarantee is made. npm installation is not atomic. Inspect the outcome and [support guide](SUPPORT.md) instead of repeatedly confirming installation.

**What if Windows warns about the tool?** The preview has no commercial Authenticode signature. Verify its source and checksums, do not disable system security, and do not run it if you cannot establish trust.

### Find the boundary before changing it

| Area | Source entry | Explain in the PR |
| --- | --- | --- |
| Discovery and target selection | `src/UpdateCenter.Core/Discovery.cs` | How wrong-target updates are prevented and unsupported layouts are handled. |
| Official network and verification | `OfficialNetwork.cs`, `Verification.cs` | Observable failure/resume/rejection cases, without weakening trust checks. |
| Update flow and persistence | `UpdateService.cs`, `Storage.cs` | Pause, interruption, restart state, and post-installation verification. |
| UI and messages | `src/UpdateCenter.App/MainWindow.xaml` and its code-behind | Repeated-action, state-transition, and error-message regression coverage. |
| Build and release | `scripts/`, `docs/RELEASING.md` | Actual audit, test, and manifest-check results. |

Short Core filenames above are under `src/UpdateCenter.Core/`. See the [architecture](ARCHITECTURE.md); a simulated success response does not prove native Windows deployment.


## Maintainable by design

81 program tests, 10 release-tool checks, isolated native acceptance, locked dependencies, source privacy audits, per-file release manifests and Windows CI. Evidence scopes are separate: see [validation](VALIDATION.md), [support](SUPPORT.md) and [GitHub Actions](https://github.com/son-gladyso/codex-update-center/actions).

Build on Windows x64 with PowerShell 7 and the SDK in `global.json`:

```powershell
pwsh -File scripts/build.ps1 -Output artifacts/release-local
```

[Architecture](ARCHITECTURE.md) · [Contributing](../CONTRIBUTING.md) · [Release process](RELEASING.md) · [Security](../SECURITY.md)

Tell us your installation type, the step that got in your way, and the outcome you expected. Never attach credentials, chats or raw account logs. Small reproducible fixes and clearer failure messages are especially welcome.

### Where to make a first contribution

- **Documentation and messages:** turn an observed failure into reproducible steps, or make installation types, cache locations, and error messages easier to understand.
- **Regression tests:** cover boundaries such as a changed installation target, pause/resume, or package-verification failure, and explain the observable result.
- **Windows validation:** record the exact system, installation type, versions, and test scope. Keep synthetic tests, native operations, and online verification separate.

Read the [contribution guide](../CONTRIBUTING.md) and [validation record](VALIDATION.md), then choose a focused problem. Use an isolated environment for tests that perform a real installation; do not treat your everyday work installation as the default test target. Discuss discovery rules, the update mechanism, and failure handling before proposing a new installation type as supported.

This page currently shows an actual application screenshot with example data, not a continuous recording of preparation through completed installation. Reproducible walkthrough improvements are welcome, but demonstration states must not be presented as real upgrade results.

MIT covers this tool's source. Codex packages are downloaded from official sources at runtime and retain their own terms. See [NOTICE](../NOTICE.md).

