# ADR 0003 — No code signing; compensating provenance controls

- Status: Accepted (2026-08-16)
- Decider: Randy Northrup ("I will not be signing this app").

## Context

The binaries will not be Authenticode-signed. Consequences on Windows:
SmartScreen shows an "unknown publisher" warning on first run of downloaded
executables and MSIs; MSIX packages cannot be installed at all without a
trusted certificate (so no MSIX / Store distribution); environments enforcing
WDAC or AppLocker publisher rules will block the binaries unless the
organisation adds hash or path rules; some EDR products raise the risk score
of unsigned elevated processes.

## Decision

- Distribute **portable ZIP + MSI (WiX) + winget manifest**; no MSIX.
- Compensate with verifiable provenance instead of a signature:
  - Deterministic, reproducible builds (`Deterministic`,
    `ContinuousIntegrationBuild`, locked restore).
  - SHA-256 checksums for every release asset published in the release notes.
  - SBOM (CycloneDX) attached to every release.
  - GitHub build provenance attestations (`actions/attest-build-provenance`)
    so anyone can verify an asset was built by our CI from a given commit.
  - Winget manifests carry the installer hash; winget verifies it.
- Document in `README.md` how an organisation can sign the MSI/EXE with its
  own certificate (`signtool`) before internal deployment, and how to author
  WDAC/AppLocker hash rules for it.
- Keep the door open: if a certificate (e.g. Azure Trusted Signing) becomes
  available later, signing is a release-pipeline change only.

## Consequences

- First-run friction for consumers (SmartScreen "More info, then Run anyway").
- Enterprise deployment via Intune Win32 app / SCCM works with the MSI, but
  organisations with publisher-based allow-listing must add rules or re-sign.
