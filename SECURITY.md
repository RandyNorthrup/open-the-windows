# Security Policy

Open the Windows runs elevated and changes operating-system state. We treat
every defect that can leave a machine less secure, less recoverable, or in a
state the user did not ask for as a security issue.

## Supported versions

Only the latest release on the `main` branch receives fixes. There are no
long-term-support branches at this stage of the project.

## Reporting a vulnerability

Please **do not** open a public issue for security reports.

- Use GitHub's private vulnerability reporting on this repository
  ("Security" tab → "Report a vulnerability"), or
- e-mail the maintainer at the address on the GitHub profile with the
  subject line `[open-the-windows] security report`.

Include: affected version or commit, Windows edition/build, exact steps or a
profile/CLI invocation that reproduces the problem, and the observed vs.
expected system state. Journals from `%ProgramData%\OpenTheWindows\journal`
are useful; redact anything you consider sensitive.

You will get an acknowledgement within 72 hours and a status update at least
every 7 days until the report is resolved. We follow coordinated disclosure:
we ask for up to 90 days to ship a fix before public disclosure, and we credit
reporters in the release notes unless they prefer otherwise.

## What counts as a security issue here

- Any tweak that weakens a security control beyond what its documented risk
  tier says (for example a "Balanced" tweak that disables a Defender feature).
- Any apply path that fails without rolling back, or a revert that does not
  restore the recorded prior state.
- Writing per-user settings into the wrong user's hive (elevation context
  confusion).
- Overriding MDM/Group Policy managed settings without the documented
  break-glass flag.
- Anything that lets a profile or catalogue file (which may come from a
  network share) execute arbitrary code, escape the declared action set, or
  bypass profile signature verification.
- Supply-chain issues: dependency vulnerabilities flagged by `NuGetAudit`,
  build non-reproducibility, tampered release assets.

## What the project does to stay safe

- The catalogue is data validated by schema and tests; only a closed set of
  typed actions can execute.
- Every apply is journaled first and verified after; failures roll back.
- No telemetry, no network access unless the user explicitly enables the
  update check.
- Releases after v0.2.0 are Authenticode-signed with Azure Artifact Signing
  (formerly Trusted Signing): `otw.exe`, `OpenTheWindows.exe`, the
  `OpenTheWindows` assemblies and the MSIs, each with an RFC 3161 timestamp.
  The publisher is **Randy Northrup**
  (`CN=Randy Northrup, O=Randy Northrup, L=Ventura, S=California, C=US`). The
  .NET runtime and third-party libraries in the bundles keep their own
  publishers' signatures. v0.2.0 and earlier are not signed. Every release also
  carries SHA-256 sums, a CycloneDX SBOM, and GitHub build-provenance
  attestations. Verify before running:

  ```powershell
  (Get-AuthenticodeSignature .\OpenTheWindows-<version>-win-x64.msi).Status                    # Valid
  (Get-AuthenticodeSignature .\OpenTheWindows-<version>-win-x64.msi).SignerCertificate.Subject # CN=Randy Northrup, ...
  Get-FileHash .\OpenTheWindows-<version>-win-x64.zip -Algorithm SHA256
  gh attestation verify .\OpenTheWindows-<version>-win-x64.zip --repo randynorthrup/open-the-windows
  ```

  Windows SmartScreen can still warn about a newly signed release until the
  publisher builds up a reputation.
- The release workflow stores no signing password, certificate or client
  secret. Azure accepts its GitHub OIDC token only from the repository's
  `release` environment, which only `v*` tags can use, and that identity may
  only sign with the one certificate profile.

- Continuous checks: analyzers at maximum severity, `NuGetAudit` (all,
  low), `gitleaks` on history and working tree, `semgrep`, `jscpd`, and the
  full test suite on every push.
