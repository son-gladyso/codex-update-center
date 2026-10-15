# Changelog

## 0.1.1-preview

- Recheck network cost before npm installation as well as preparation, including reuse of verified candidates; a blocked attempt keeps the prepared state and never enters installation.
- Keep preferences consistent when persistence fails, and cancel downloads even if writing the pause journal fails.
- Distinguish user pauses from timeouts and unconfirmed installations; discard in-memory candidates after installation failure.
- Skip malformed npm candidates without hiding healthy installations; refresh preparation metadata after six hours.
- Add STA WPF behavioral regressions, source audits, strict release manifests, consistent version/tag validation, architecture documentation and dependency update workflows.

## 0.1.0 — preview

- Independent Windows x64 community edition with a per-user data directory.
- Detect official current-user MSIX and standard npm global installations without fixed drive letters or instance counts.
- Prepare official packages, verify signatures and content, and update one selected installation after confirmation.
- Preserve download checkpoints, check volume identity, prefer C when its space budget fits, and pause on metered or unknown network cost.
- Record interrupted or unconfirmed installations for review instead of reporting success.
- Require the Windows native dependency, verify its registry signature and SHA-512, resume bounded downloads and verify the actual CLI runtime after installation.
- Carry the system proxy into npm, retain TLS validation and recheck the target after preparation.
- Add native behavior tests, an opt-in real npm upgrade acceptance program, source auditing and GitHub draft-release workflows.

Windows MSIX deployment across clean machines, account types and OS versions remains a VM acceptance requirement. This version is not a claim that the compatibility matrix has passed.
