# Security policy

The 0.1.x line is a preview. Security reports are welcome, but no response-time SLA is promised.

Use the repository's **Security → Report a vulnerability** when private reporting is enabled. Maintainers must enable private vulnerability reporting before the first public release. If it is unavailable, open an issue asking for a private contact without posting exploit details, credentials or personal data.

Include affected version, installation type, minimal reproduction, observed behavior and impact. Report issues in this updater here. Report vulnerabilities in OpenAI applications or Microsoft Windows through those vendors' reporting channels.

The updater trusts Windows, the current user's local installation tools, the Windows trust store and official HTTPS endpoints. It does not protect against an attacker already able to modify the current user's process or binaries. Release SHA-256 manifests detect corruption; they are not a substitute for Authenticode or an independent signature. Current community builds are not Authenticode signed.

Dependency alerts, private reporting and branch protection require repository settings; workflow files alone cannot enable them.
