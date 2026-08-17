# Catalogue format and authoring guide

The catalogue is data. Every tweak the product can apply is a JSON entry
validated by `catalog/schema/tweak.schema.json` (JSON Schema draft 2020-12)
and by `CatalogValidator` (structural rules). If it is not in the catalogue,
the engine cannot do it. This page is the contract for authors and agents.

## Files

- Built-in: `catalog/<category>/<topic>.json`, embedded into
  `OpenTheWindows.Core` at build time (logical name `catalog/<category>/<file>`).
- Overrides: any directory passed with `--catalog-dir`; entries with the same
  `id` replace built-in ones; duplicates inside the directory are errors.
- Document shape:

```json
{ "$schema": "../schema/tweak.schema.json", "entries": [ { ...entry... } ] }
```

Comments (`//`) and trailing commas are tolerated for hand editing; unknown
properties are **errors** (schema `additionalProperties:false` and STJ
`UnmappedMemberHandling = Disallow`).

## Entry fields (all required; use `null`/`[]` explicitly)

| Field | Type / values | Rules |
| ------- | --------------- | ------- |
| `id` | `segment(.segment)*`, segment = kebab-case `[a-z0-9-]`, ≤ 96 chars | Unique across the catalogue. Convention: `<category>.<topic>.<verb-object>` e.g. `privacy.advertising-id.disable`. Never rename a shipped id (journals reference it); deprecate instead. |
| `title` | string ≤ 80 | Imperative, user-facing: "Disable the advertising ID". |
| `description` | string | What changes, in user terms. |
| `rationale` | string | Why, and the honest cost. |
| `category` | `Privacy` `Updates` `Security` `Performance` `Debloat` `Shell` | |
| `level` | `Basic` `Balanced` `Strict` `Paranoid` | Lowest dial that applies it. `Basic` = zero functional loss. Never `Off`. |
| `risk` | `Safe` `Caution` `Advanced` `Breaking` | `Breaking` ⇒ level Strict/Paranoid; `Advanced` ⇒ not Basic. `Caution`+ should list `sideEffects` (warning otherwise). |
| `scope` | `Machine` `User` `AllUsers` | Must agree with registry hives: `User` hive actions ⇒ scope `User`/`AllUsers`; `LocalMachine` ⇒ `Machine`. |
| `status` | `Draft` `Verified` `Deprecated` | New entries are `Draft`. `Verified` requires ≥ 1 `verifiedOn`. Built-in profiles apply Verified entries only. |
| `appliesTo` | `{ editions: [Home,Pro,Enterprise,Education], minBuild, maxBuild, architectures: [x64,arm64] }` | Empty arrays / null = any. Use editions when the policy is documented Pro+/Enterprise-only (research 01/03 say which). |
| `actions` | array ≥ 1 of typed actions (below) | Applied in order; reverted in reverse. |
| `requires` | `None` `ExplorerRestart` `SignOut` `Reboot` | Be honest; the engine aggregates the max. |
| `dependsOn` / `conflictsWith` | arrays of ids | Must exist; no self-reference. |
| `managedBy` | `{ policyCsp, groupPolicyPath }` or `null` | Lets the engine/UI say "managed by your organisation". Fill both when known. |
| `sideEffects` | array of strings | One functional loss per item, concrete ("Consumer NAS with guest shares stops being reachable"). |
| `sources` | array ≥ 1 of `https://` URLs | Prefer learn.microsoft.com; every claim in the description must be backed. |
| `verifiedOn` | array of `{ build ≥ 22000, edition, date (YYYY-MM-DD), evidence }` | `evidence` = repo path (`docs/certification/M4/<id>.md`) or URL. Filled only by the VM procedure in `docs/milestones/M4-catalogue-population.md`. |
| `tags` | array of strings | e.g. `telemetry`, `gamer`, `developer`, `baseline`, `stig`, `cis-18.9.x`. |

## Actions (`kind` is the discriminator; PascalCase)

| kind | Fields | Semantics |
| ------ | -------- | ----------- |
| `Registry` | `hive` (`LocalMachine`\|`User`), `path` (no leading `\`, no `HKEY_`), `name` (`""` = default value), `type` (`Dword` `Qword` `Sz` `ExpandSz` `MultiSz` `Binary`), `value`, `default`, `valueKind` (`Preference`\|`Policy`) | `value`/`default` JSON must match `type`: number for Dword (≤ 4294967295)/Qword, string for Sz/ExpandSz, string array for MultiSz, base64 string for Binary. `default` = documented Windows default; JSON `null` = value absent by default (reset deletes it). `Policy` kind values live under a `Policies` key and are written through the Local GPO and mirrored; `Preference` values are written directly. `User` hive means the *interactive user's* `HKU\<SID>` (never HKCU of the elevating admin). |
| `Service` | `name`, `startType`, `default` (both `Automatic` `AutomaticDelayed` `Manual` `Disabled`), `stopIfRunning` | Never a `ProtectedServices` name (validator error). Prefer `Manual` over `Disabled` unless research 05 §3 says DISABLE-OK. |
| `ScheduledTask` | `path` (starts with `\`), `enabled`, `default` | Missing task ⇒ NotApplicable at scan. |
| `Appx` | `packageFamilyName` (`Name_PublisherId`), `mode` (`CurrentUser`\|`AllUsersAndDeprovision`), `reinstallHint` | Only families listed SAFE/CAUTION in research 05 §1; never Store, Terminal, WebView2, frameworks, codecs, Security Center. |
| `OptionalFeature` | `name` (DISM name), `enabled`, `default` | |
| `DefenderPreference` | `property`, `value`, `default` | `MSFT_MpPreference` property names; never anything that disables real-time protection or signature updates. |
| `PowerSetting` | `subgroupGuid`, `settingGuid`, `acValue`, `dcValue` | Active scheme only. |
| `Command` | `executable` (allow-list: `powercfg.exe` `netsh.exe` `auditpol.exe` `secedit.exe` `dism.exe` `fsutil.exe` `usoclient.exe`), `arguments[]`, `revertArguments[]` | Last resort; both directions required; no shell; arguments are passed verbatim (no interpolation). |

## Authoring workflow

1. Find the setting in the research report (`docs/research/01`, `03`, `04`,
   `05`); copy the mechanism exactly (hive/path/name/type/values, policy vs
   preference, edition applicability, side effects, level).
2. Write the entry as `Draft`; run `otw catalog validate` (or the Core tests).
3. Open a PR with the entry and its research row reference. CI runs the
   `catalog` gate.
4. Verification on the lab VM per M4 procedure fills `verifiedOn` and flips
   `status` to `Verified` in a second PR with the evidence file.

## Never

- No entry for anything in `docs/refusals.md` (WaaSMedic/wuauserv/UsoSvc
  Disabled, Defender off, UAC off, hosts blocking, Edge/WebView2 removal,
  Store removal on Home/Pro, `DoNotConnectToWindowsUpdateInternetLocations`
  on consumer machines).
- No entry from the snake-oil list (`docs/not-included.md`, research 05 §7).
- No CIS benchmark text; ids and paraphrase only (ADR 0002).
- No AGPL/GPL-derived content (privacy.sexy YAML etc.); learn from MIT
  projects with attribution in the research docs, re-implement here.
