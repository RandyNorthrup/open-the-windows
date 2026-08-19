# Enterprise deployment

How to deploy and enforce Open The Windows across a managed Windows 11 fleet with
Microsoft Intune (or any tool that runs a detection/remediation script pair),
signed profiles, and Windows Defender Application Control (WDAC).

Open The Windows is an unsigned tool by design (see the project decisions in
`PLAN.md`). Everything below therefore assumes you either accept the unsigned
binary through a file-hash allow rule or re-sign it with your own certificate;
both paths are documented under [Application control](#application-control-wdac).

## Exit-code contract

The CLI and every automation wrapper share one stable exit-code contract
(`OpenTheWindows.Core.ExitCodes`). Values never change once released.

| Code | Name                | `scan` means           | `remediate` / `apply` means            |
| ---- | ------------------- | ---------------------- | -------------------------------------- |
| 0    | Success             | Compliant              | Applied, or already compliant          |
| 1    | Drift               | Drift from the profile | (not returned by remediate)            |
| 2    | Error               | Unexpected failure     | Unexpected failure                     |
| 3    | UnsupportedPlatform | Not a supported build  | Not a supported build                  |
| 4    | InvalidInput        | Bad profile/arguments  | Bad profile/arguments                  |
| 5    | ElevationRequired   | (scan needs no token)  | No elevated token                      |
| 3010 | RebootRequired      | (not returned by scan) | Applied; reboot required to complete   |

The Intune scripts in [`docs/enterprise/`](enterprise/) map this contract onto
Intune's detection contract (0 = healthy, non-zero = remediate) and remediation
contract (0 = success, non-zero = failure). The mapping is documented in each
script's header and summarised under [Intune Remediations](#intune-remediations).

## Deploying otw.exe

The packaged installer is delivered by milestone M7. Until then, place `otw.exe`
and its dependencies in a fixed machine-wide folder — the scripts default to
`%ProgramFiles%\OpenTheWindows\otw.exe` — or add that folder to the machine
`PATH`. Deploy it with your existing mechanism (Intune Win32 app, Configuration
Manager package, or a file copy in a provisioning script).

Both Intune scripts resolve the executable in this order:

1. the `$OtwPath` variable, if you set it at the top of the script;
2. `otw.exe` on the machine `PATH`;
3. `%ProgramFiles%\OpenTheWindows\otw.exe`.

If none resolves, the detection script reports the device as needing attention
and the remediation script fails with a clear message rather than silently
succeeding.

## Intune Remediations

Use the paired scripts in [`docs/enterprise/`](enterprise/):

- [`intune-detect.ps1`](enterprise/intune-detect.ps1) — runs `otw scan` and
  reports compliance with a named profile.
- [`intune-remediate.ps1`](enterprise/intune-remediate.ps1) — runs
  `otw remediate` to re-apply only the drifted settings, unattended, and writes
  the JSON run report to `%ProgramData%\OpenTheWindows\reports\last-remediate.json`.

### Before uploading

Edit the two configuration lines at the top of **both** scripts so they agree:

```powershell
$ProfileId = 'enterprise-workstation'   # a built-in id or a profile file path
$OtwPath = ''                           # empty = resolve from PATH / default folder
```

### Create the remediation in Intune

1. In the Microsoft Intune admin center, go to **Devices → Remediations →
   Create**.
2. Upload `intune-detect.ps1` as the **Detection script** and
   `intune-remediate.ps1` as the **Remediation script**.
3. Set **Run this script using the logged-on credentials** to **No** — both
   scripts must run as SYSTEM so `remediate` has an elevated token.
4. Set **Enforce script signature check** to **No** unless you sign the scripts
   with a certificate the device trusts (see [Re-signing](#re-signing)).
5. Set **Run script in 64-bit PowerShell** to **Yes**.
6. Assign the remediation to a device group and give it a daily schedule.

Intune reruns the detection script on the schedule; whenever it exits non-zero
the remediation script runs once, then detection confirms the result on the next
cycle.

## Scheduled drift task (agent-side alternative)

Where Intune is not the delivery channel, the CLI can install the same drift
loop as a local scheduled task that runs `otw remediate` as SYSTEM:

```text
otw task install --profile enterprise-workstation --daily
otw task remove
```

The task is registered at `\OpenTheWindows\Remediate` (SYSTEM, highest
privileges, hidden) and writes the same `last-remediate.json` report. See the
CLI reference in `README.md` for the full option list.

## All-users profiles and the per-user logon fallback

A profile with `scope: AllUsers` applies its user-scope entries to every real
user hive on the machine, loading the hives of users who are not logged on. To
also reach users who are logged off at apply time — and users created later — a
completed all-users `apply`/`remediate` registers a standard **Active Setup**
component:

```text
HKLM\SOFTWARE\Microsoft\Active Setup\Installed Components\{OTW-<profileId>}
    (Default)   = Open the Windows - <profile name>
    StubPath    = "…\otw.exe" remediate --user-scope --profile <profileId>
    Version     = <bumped each apply>
```

At each user's next sign-in Windows runs the stub in that user's own session,
non-elevated, and it applies only the profile's user-scope entries to that
user's hive (writing one's own `HKU\<SID>` needs no administrator token).
Registration is skipped while the machine is in OOBE
(`HKLM\SYSTEM\Setup\OOBEInProgress`), so it is safe to run during image build
without baking the component into a generalised image. `otw task remove` clears
the fallback (every `{OTW-…}` component) along with the drift task, so removing a
profile's re-enforcement footprint is a single command.

## Profiles and signing

A profile is a JSON document selecting the tweaks and levels a device should
carry; the format is specified in [`docs/profile-format.md`](profile-format.md).
Seven built-in profiles ship embedded in the product (`home`, `power-user`,
`enterprise-workstation`, `developer`, `gamer`, `kiosk-shared`, `privacy-max`).
For a custom fleet profile, author a file and reference it by path.

### Requiring signed profiles

Set the machine policy so the CLI refuses any profile **file** that is not
accompanied by a valid, trusted signature:

- Registry: `HKLM\SOFTWARE\Policies\OpenTheWindows`, value
  `RequireSignedProfiles` (REG_DWORD), `1` to require, `0`/absent to allow
  unsigned files.
- Group Policy: import the bundled ADMX/ADML in [`admx/`](../admx/) into your
  Central Store, then enable **Open The Windows → Require signed profiles**.

Built-in profiles are embedded and trusted; the policy applies to profile files
supplied on disk or via `--profile <path>`.

### Signing a profile

1. Generate an ECDSA P-256 key pair (the signature algorithm is ES256).
2. Sign the profile file — this writes a detached `<file>.sig` next to it:

   ```text
   otw profile sign --key fleet-key.pem path\to\fleet.json
   otw profile verify path\to\fleet.json
   ```

3. Pin the **public** key on every managed device by placing its PEM in
   `%ProgramData%\OpenTheWindows\trusted-keys\`. Any `*.pem` in that folder is
   trusted; the signature's `keyId` is the hex SHA-256 of the key's
   SubjectPublicKeyInfo, so rotating a key is a matter of adding the new PEM.

### Re-signing

Re-sign a profile whenever its content changes — the signature covers the exact
bytes of the file, so any edit invalidates it. Distribute the refreshed
`<file>.sig` alongside the profile.

If your organisation runs application control that keys on Authenticode
publishers, you may also re-sign `otw.exe` itself with your own code-signing
certificate before deployment; the product ships unsigned specifically so that
you can.

## Application control (WDAC)

Under WDAC in enforced mode, an unsigned `otw.exe` is blocked unless you allow
it. Two options:

- **File-hash rule (unsigned binary).** Generate a hash rule for the exact
  `otw.exe` you deploy:

  ```text
  New-CIPolicyRule -Level Hash -DriverFilePath 'C:\Program Files\Open the Windows\cli\otw.exe'
  ```

  Merge the rule into your base policy. A hash rule pins one build, so you must
  refresh it every time you update `otw.exe`.

- **Signer rule (re-signed binary).** If you re-sign `otw.exe` with your own
  certificate (see [Re-sign with your certificate](#re-sign-with-your-certificate)),
  author a publisher/signer rule instead so the allow rule survives version
  updates.

Whichever you choose, the scheduled task and the Intune remediation invoke the
same `otw.exe`, so a single allow rule covers both delivery paths.

## Re-sign and allow-list

The published MSI and binaries are unsigned (ADR 0003). An organisation that
deploys them internally has two ways to satisfy application control: **re-sign**
with its own certificate, or **allow-list by hash**. The MSI lays files down at:

- `%ProgramFiles%\Open the Windows\cli\otw.exe` — the CLI
- `%ProgramFiles%\Open the Windows\app\OpenTheWindows.exe` — the GUI

### Re-sign with your certificate

Re-sign the two executables (and, if you redistribute it, the MSI) with an
internal code-signing certificate before deployment, then author signer-based
WDAC/AppLocker rules that survive version updates:

```powershell
$cert = 'Cert:\CurrentUser\My\<thumbprint>'   # your code-signing cert
$ts   = 'http://timestamp.digicert.com'
signtool sign /fd SHA256 /a /tr $ts /td SHA256 `
  "C:\Program Files\Open the Windows\cli\otw.exe" `
  "C:\Program Files\Open the Windows\app\OpenTheWindows.exe"
# Re-sign the installer itself if you host it internally:
signtool sign /fd SHA256 /a /tr $ts /td SHA256 OpenTheWindows-<version>-win-x64.msi
```

Re-signing does not change behaviour; it only adds your Authenticode signature so
SmartScreen and publisher allow-lists recognise the binaries.

### AppLocker rules

For environments on AppLocker rather than (or alongside) WDAC:

- **Publisher rule (after re-signing).** Create an *Executable* rule scoped to
  your signer and the product name `Open the Windows`; it keeps working across
  version updates. Do the same as a *Windows Installer* rule for the MSI.
- **File-hash rule (unsigned).** If you deploy the unsigned binaries, generate a
  hash rule for the exact `otw.exe` and `OpenTheWindows.exe` you ship. As with the
  WDAC hash rule above, a hash pins one build, so refresh it on every update.

```powershell
Get-AppLockerFileInformation "C:\Program Files\Open the Windows\cli\otw.exe" |
  New-AppLockerPolicy -RuleType Hash -User Everyone -RuleNamePrefix OTW -Xml |
  Out-File otw-applocker.xml
```

### Package the MSI as an Intune Win32 app

Wrap the MSI with the Microsoft Win32 Content Prep Tool and upload it as a Win32
app (this delivers the GUI + CLI to user devices; the drift-remediation delivery
of `otw.exe` alone is covered under [Deploying otw.exe](#deploying-otwexe)).

```text
IntuneWinAppUtil.exe -c <folder-with-msi> -s OpenTheWindows-<version>-win-x64.msi -o <out>
```

- **Install command:** `msiexec /i OpenTheWindows-<version>-win-x64.msi /qn`
- **Uninstall command:** `msiexec /x {ProductCode} /qn` (read the ProductCode
  from the MSI, or use `winget` metadata)
- **Install behaviour:** System · **Device restart:** No (a reboot is only needed
  when a specific tweak requires it, surfaced by the exit-code contract)
- **Detection rule (recommended):** *File* — path
  `%ProgramFiles%\Open the Windows\cli`, file `otw.exe`, detection method
  **version**, operator *greater than or equal to*, value `<version>`. Keying the
  detection on the `otw.exe` file version makes upgrades detect correctly. (An MSI
  product-code detection also works but must be updated when the code changes
  between versions.)

## Auditing

Every apply and revert is journaled under
`%ProgramData%\OpenTheWindows\journal\` with a hash-chained record of what
changed, and each remediation run also writes `last-remediate.json` under
`%ProgramData%\OpenTheWindows\reports\`. Collect these with your existing log
pipeline to prove fleet state over time.

## Baseline compliance audits

`otw audit` scores a machine against a security baseline (`disa-stig-v2r7`,
`cis-win11-ent-v4.0`, `ms-baseline-25h2`) read-only — no elevation needed, no
change made. See [audit.md](audit.md) for the baselines, outcomes and scoring.

For a fleet, register a scheduled audit that drops a signed JSON report to a
collection share:

```powershell
# Weekly SYSTEM task that audits against the Microsoft baseline and drops the report.
otw task install --audit ms-baseline-25h2 --weekly --out \\fileserver\audits\%COMPUTERNAME%.json
```

The task (`\OpenTheWindows\Audit`, SYSTEM, hidden) runs `otw audit run --baseline
ms-baseline-25h2 --json --out <path>`. To make the drop tamper-evident, run a
signed audit from your own tooling with `--sign <org-key.pem>` (the same ES256 key
model as signed profiles, see *Profiles and signing* above): the JSON carries a
SHA-256 integrity `hash` and a `{ alg, keyId, signature }` envelope your collector
verifies against the org public key before trusting the score. For SARIF ingestion
(the rule ids are the baseline control ids), use `otw audit run --baseline <id>
--sarif --out <path>`.
