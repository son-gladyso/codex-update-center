# Architecture and maintenance

`UpdateCenter.Core` owns discovery, official network access, package verification, storage policy and update transactions. `UpdateCenter.App` owns WPF selection, confirmation, preferences and presentation. Tests use temporary directories and injectable boundaries; ordinary CI never installs Codex or reads account data.

## Update boundaries

1. Discovery identifies official current-user Windows packages and independent npm prefixes. Malformed candidates produce warnings without hiding healthy installations. Unsupported layouts remain visible and cannot be updated.
2. Preparation checks network cost, free space and cache volume identity. Both the main package and native npm dependency are verified. Prepared offers expire after six hours; expired bytes are retained and a fresh offer is queried.
3. Installation requires confirmation. `UpdateService` takes an exclusive operation lock, verifies the package again, rediscovers the exact target and checks that it is idle. npm performs a further target check after dependency preparation.
4. Success requires rediscovery with the expected version. npm additionally executes the isolated installed CLI with an empty home. Interrupted or failed installations require review; npm deployment does not provide atomic rollback.

The per-user `window.lock` prevents competing windows. `operation.lock` protects mutation. `job.json` records phases; `preparation.json` records resumable download identity. Preferences are committed to memory only after persistence succeeds. UI errors are caught at event/operation boundaries; installation cancellation never claims a paused download.

## Working on this project

- Keep native process invocation in platform adapters. Pass arguments as arrays, retain TLS validation and never interpolate untrusted values into shell source.
- Add a regression for observable failures: actual bytes, locked files, real dispatcher state, recorded phases, rejected signatures or process outputs. UI regression tests run isolated STA dispatchers and never display update prompts.
- `Directory.Build.props` is the release version authority. UI, HTTP user agent and archive names derive from it. Tags must match `v<Version>`.
- Run `scripts/build.ps1` for a release candidate. It restores locked dependencies, tests, publishes, audits sources and verifies manifests. The explicit native acceptance runner is outside ordinary CI.
- Review dependency PRs and lock-file changes together. Run the release gate after any runtime or SDK upgrade. Keep machine-specific evidence in ignored `artifacts`, with an accurate public summary in `VALIDATION.md`.

See [RELEASING.md](RELEASING.md) for the publication checklist and [SUPPORT.md](SUPPORT.md) for supported installations.
