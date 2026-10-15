# Contributing

Use Windows x64 and the SDK in `global.json`. Restore in locked mode, then run the full test suite. Keep platform adapters separate from UI and networking. Add regression tests when a failure could modify the wrong installation, accept an invalid package or falsely claim success.

Tests must exercise observable behavior: real file bytes, a journal after failure, a process exit, a cryptographic rejection, or a fresh installation inventory. A stub returning success does not prove a native Windows deployment. Label synthetic, native, online and VM evidence separately.

Do not commit `artifacts`, installed application payloads, credentials, personal paths, screenshots of user accounts or machine-specific state. `scripts/audit-source.ps1` and `scripts/build.ps1` are local checks; neither is a substitute for reviewing the files you stage.

For a pull request, describe the failure, the resulting behavior, tests actually run and remaining limitations. Keep changes focused. New installation types need documented discovery rules, a supported update mechanism, failure behavior and native evidence before they can be advertised as supported.

Do not weaken TLS, signatures, exact package identity, user confirmation or running-process checks to make a test pass. Do not add force-shutdown, blanket process termination, uninstall/reinstall or account migration as a workaround.
