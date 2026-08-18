# Profile format

A **profile** is the unit of intent in Open the Windows: it says *which* catalogue
entries apply to a machine and *how aggressively*, without naming every tweak by
hand. A profile is a small JSON document. The product ships seven built-in
profiles embedded in the binary; operators can also author their own and pass
them as files.

This document is the reference for that format: every field, how a profile is
turned into a concrete set of tweaks, how profiles are validated, and how they
are signed. The machine-readable contract is
[`catalog/schema/profile.schema.json`](../catalog/schema/profile.schema.json)
(JSON Schema draft 2020-12); this document explains it.

- The catalogue itself (the tweaks a profile references) is documented in
  [`docs/catalogue.md`](catalogue.md) and the entry schema.
- The CLI that consumes profiles is documented in the [README](../README.md);
  the subcommand reference is repeated at the end of this file.

## Where profiles come from

`otw` accepts three kinds of value wherever a profile is expected
(`--profile`, and the `profile` argument of `otw profile show|validate`):

| Value | Meaning | Example |
| ------- | --------- | --------- |
| A **level name** | Every category set to one level. The M2 shorthand; not a full profile. | `--profile balanced` |
| A **built-in id** | One of the seven embedded profiles, resolved through the profile engine. | `--profile enterprise-workstation` |
| A **file path** | A profile `.json` file on disk. | `--profile .\corp.json` |

An existing file path is tried first, then a built-in id, then a level name; an
argument that matches none is an error that lists the built-in ids. The level
names are `basic`, `balanced`, `strict`, `paranoid` (ascending); they map to a
single [`Level`](#the-level-dial) applied to every category and carry no
include/exclude, scope, options or applicability.

## Built-in profiles

Seven profiles ship embedded in the product (`catalog/profiles/*.json`). They
are the recommended starting points; copy one to a file and edit it to make a
custom profile. Run `otw profile list` to see them on the machine, or
`otw profile show <id>` for the full dial.

| id | Scope | Privacy | Updates | Security | Performance | Debloat | Shell | Intent |
| ---- | ------- | --------- | --------- | ---------- | ------------- | --------- | ------- | -------- |
| `home` | User | Balanced | Basic | Balanced | Balanced | Basic | Basic | Everyday consumer PC, zero functional loss a household would notice. |
| `power-user` | User | Strict | Balanced | Balanced | Strict | Balanced | Balanced | Enthusiast single-user desktop, minor well-understood trade-offs. |
| `developer` | User | Balanced | Balanced | Balanced | Strict | Balanced | Balanced | Workstation tuned for tooling without interference. |
| `gamer` | User | Balanced | Basic | Basic | Paranoid | Strict | Balanced | Maximum performance; updates near default so anti-cheat and drivers keep working. |
| `privacy-max` | User | Paranoid | Balanced | Strict | Balanced | Strict | Balanced | Privacy above convenience; expect some cloud features to stop working. |
| `enterprise-workstation` | AllUsers | Strict | Balanced | Strict | Basic | Balanced | Basic | Managed corporate device, applied to every user. |
| `kiosk-shared` | AllUsers | Strict | Balanced | Strict | Balanced | Strict | Strict | Locked-down shared or single-purpose device. |

Built-in profiles are held to stricter rules than operator files: they may never
set `allowBreaking` and may never include a `Breaking`-risk entry (see
[Validation](#validation)). The two all-users built-ins declare an edition
allow-list (`Pro`, `Enterprise`, `Education`) because Home has no meaningful
multi-user or policy story.

## The document

A complete profile, with every field shown:

```json
{
  "$schema": "https://github.com/RandyNorthrup/open-the-windows/catalog/schema/profile.schema.json",
  "schemaVersion": 1,
  "id": "corp-standard",
  "name": "Corp Standard",
  "description": "Managed corporate device baseline.",
  "levels": { "Privacy": "Strict", "Updates": "Balanced", "Security": "Strict", "Performance": "Basic", "Debloat": "Balanced", "Shell": "Basic" },
  "include": ["security.credentials.lsa-protection"],
  "exclude": ["debloat.apps.remove-feedback-hub"],
  "includeDraft": false,
  "scope": "AllUsers",
  "options": { "restorePoint": true, "restartExplorer": false, "allowAdvanced": true, "allowBreaking": false },
  "appliesTo": { "editions": ["Pro", "Enterprise", "Education"], "minBuild": null, "maxBuild": null, "architectures": [] }
}
```

Every field is required (`$schema` is the one optional field). Unknown fields are
rejected — the schema is `additionalProperties: false` throughout.

| Field | Type | Rules |
| ------- | ------ | ------- |
| `$schema` | string | Optional. The schema URL; ignored at load time, useful for editor completion. |
| `schemaVersion` | integer ≥ 1 | The profile format version. The runtime rejects a version it does not understand (currently `1`). |
| `id` | string | `^[a-z0-9]+(-[a-z0-9]+)*$`, 1–64 chars. Lower-case kebab. Unique among the profiles in play. |
| `name` | string | 1–80 chars. Human label shown in listings. |
| `description` | string | 1–400 chars. One or two sentences on intent. |
| `levels` | object | The level dial: one [`Level`](#the-level-dial) per category. All six categories required. |
| `include` | string[] | Tweak ids to force on regardless of the dial. Unique. See [Resolution](#resolution). |
| `exclude` | string[] | Tweak ids to force off regardless of the dial. Unique. Exclude always wins. |
| `includeDraft` | boolean | When `true`, `Draft` catalogue entries are eligible; when `false`, only `Verified` entries. |
| `scope` | `User` \| `AllUsers` | Which users the profile targets. See [Scope](#scope). |
| `options` | object | Apply-time options. See [Options](#options). |
| `appliesTo` | object | Machine applicability. See [Applicability](#applicability). |

### The level dial

Each category — `Privacy`, `Updates`, `Security`, `Performance`, `Debloat`,
`Shell` — is set to one level. Levels are ordered and **cumulative**: a higher
level includes every tweak of the lower levels in that category.

| Level | Meaning |
| ------- | --------- |
| `Off` | Nothing in this category is changed. Use it to opt a whole category out. |
| `Basic` | Zero functional loss. Safe for any machine, including managed ones. |
| `Balanced` | Recommended default. Minor, well-understood trade-offs. |
| `Strict` | Accepts noticeable functional loss for privacy/security gains. |
| `Paranoid` | Maximum. Expects the operator to understand every side effect. |

Every catalogue entry declares the lowest level at which it applies by default.
A profile with `"Privacy": "Strict"` selects every non-`Off` Privacy entry whose
declared level is `Basic`, `Balanced` or `Strict` — but never a `Paranoid` one.

### Resolution

`otw scan --profile` and `otw apply --profile` turn a profile into a concrete,
ordered list of catalogue entries by
[`ProfileResolver`](../src/OpenTheWindows.Core/Profiles/ProfileResolver.cs).
The process is deterministic and side-effect free:

1. **Levels.** For each category, take every entry the level dial selects
   (level at or below the dial, not `Off`, not deprecated; draft entries only
   when `includeDraft`). Drop `Advanced` and `Breaking`-risk entries unless
   `options` allow them (see [Options](#options)).
2. **Include.** Force in every `include` id that exists and is not deprecated.
   Includes bypass the level and risk gates — an explicit operator opt-in.
3. **Exclude.** Remove every `exclude` id. Exclude always wins, even over an
   include of the same id.
4. **Applicability.** Keep only entries whose own `appliesTo` matches the
   machine, returned in catalogue load order.

The result is the set `scan` reports drift for and `apply` would change.

### Options

`options` controls how an apply behaves and which risk tiers a profile may reach.

| Option | Effect |
| ------ | ------ |
| `allowAdvanced` | When `true`, `Advanced`-risk entries selected by the dial are kept. When `false`, they are dropped from the dial selection (an `include` still forces one on). |
| `allowBreaking` | Same, for `Breaking`-risk entries. Built-in profiles must leave this `false`. |
| `restorePoint` | Requests a System Restore point before applying. |
| `restartExplorer` | Requests an Explorer restart after applying shell changes. |

All four options are consumed. `allowAdvanced` and `allowBreaking` gate
resolution as described (which entries are selected), and when a profile is
applied they are also its apply defaults: the profile's own `options` seed the
run, and the `apply` command's flags still override — `--allow-advanced`,
`--allow-breaking` and `--restart-explorer` each turn a behaviour on, and
`--no-restore-point` turns the restore point off, whatever the profile says.
A profile that sets `allowAdvanced: true` therefore applies its Advanced entries
without the operator having to repeat `--allow-advanced` on the command line.

### Scope

`scope` names which users a profile targets:

- `User` — the interactive user's hive (`HKU\<SID>`, never the elevating
  account's `HKCU`).
- `AllUsers` — every user on the machine, including users who are not logged on
  and users created in the future.

**Current support.** `scope` is validated, shown and carried on every profile.
`AllUsers` *selection* works today; `AllUsers` *application* (writing into
logged-off and default user hives) lands with the all-users work package — until
then `apply` targets the interactive user regardless of `scope`, and the two
`AllUsers` built-ins select correctly but apply per-user. This limitation is
called out here rather than silently ignored.

### Applicability

`appliesTo` restricts a profile to matching machines. An empty array or a `null`
bound means "no restriction on this axis".

| Field | Meaning |
| ----- | ------- |
| `editions` | Windows editions the profile targets (`Home`, `Pro`, `Enterprise`, `Education`). Empty = any. |
| `minBuild` / `maxBuild` | Inclusive OS build bounds (22000–99999), or `null` for open-ended. |
| `architectures` | CPU architectures (`x64`, `arm64`). Empty = any. |

A profile that does not apply to the machine resolves to an empty set. This is
distinct from per-entry applicability (step 4 of [Resolution](#resolution)),
which filters individual tweaks; `appliesTo` here gates the whole profile.

## Validation

A profile is validated in two layers. `otw profile validate <profile>` runs
both and reports every issue.

1. **Schema.** The document is checked against
   [`profile.schema.json`](../catalog/schema/profile.schema.json): required
   fields, types, enum values, id patterns, no unknown fields. A schema failure
   is fatal — the profile does not load.
2. **Catalogue.** Rules that need the catalogue and so cannot be expressed in
   the schema, from
   [`ProfileValidator`](../src/OpenTheWindows.Core/Profiles/ProfileValidator.cs):

| Rule | Severity | Meaning |
| ---- | -------- | ------- |
| `include-unknown` | Error | An `include` id has no catalogue entry. |
| `builtin-include-breaking` | Error | A built-in profile includes a `Breaking`-risk entry. |
| `builtin-allow-breaking` | Error | A built-in profile sets `allowBreaking: true`. |
| `include-deprecated` | Warning | An `include` id refers to a deprecated entry; it is ignored at apply. |
| `exclude-unknown` | Warning | An `exclude` id has no catalogue entry; it has no effect. |
| `include-exclude-overlap` | Warning | An id is in both lists; exclude wins. |

The built-in rules apply only to profiles that ship in the product. An operator
file may set `allowBreaking: true` and include a `Breaking` entry — but it must
be a deliberate, signed choice (see below), and the risk badges make the
consequences explicit.

## Signing

Profiles can be signed so a machine can prove a file came from a trusted author
and was not altered. Signing is **ES256** — ECDSA on the NIST P-256 curve with
SHA-256 (decision D11: .NET 10 has no standalone Ed25519). The signature is
*detached*: signing `corp.json` writes `corp.json.sig` next to it.

### What is signed

The signature is taken over a **canonical** form of the profile JSON, not the
file bytes:

- object keys sorted by ordinal,
- no insignificant whitespace,
- comments and trailing commas removed.

So a signature survives pretty-printing or key re-ordering that does not change
the document's meaning, but breaks on any change to a value. The signer's
identity is a **keyId**: the lower-case hex SHA-256 of the key's
SubjectPublicKeyInfo. A signature file is:

```json
{ "alg": "ES256", "keyId": "9f86d081...", "signature": "MEUCIQ..." }
```

### Signing and verifying

```pwsh
# Create a P-256 key pair (OpenSSL shown; any ES256/P-256 PEM works).
openssl ecparam -name prime256v1 -genkey -noout -out corp-key.pem
openssl ec -in corp-key.pem -pubout -out corp-key.pub.pem

# Sign a profile, then verify it against the public key.
otw profile sign .\corp.json --key .\corp-key.pem      # writes corp.json.sig
otw profile verify .\corp.json --key .\corp-key.pub.pem
```

`sign` refuses a profile that fails validation, and refuses a key file that does
not hold a private key. `verify` returns exit code 0 for a valid signature and 4
for an invalid or unverifiable one, and never throws on malformed input — a bad
algorithm, key-id mismatch or unparseable signature simply verifies as false.

### Trust store

When `otw profile verify` is called without `--key`, it verifies against the
machine **trust store**: every `*.pem` public key under

```text
%ProgramData%\OpenTheWindows\trusted-keys\
```

The signature verifies if any trusted key's key-id matches and that key confirms
the signature. An empty trust store never verifies. Distributing a public key to
that directory (via GPO, Intune or an installer) is how an organisation pins the
authors it trusts.

## Requiring signatures by policy

Signing is optional by default: `otw scan --profile file.json` runs whether or
not `file.json` is signed. An organisation can make it mandatory with the
machine policy

```text
HKLM\SOFTWARE\Policies\OpenTheWindows\RequireSignedProfiles   (REG_DWORD)
```

When it is `1`, `otw scan` and `otw apply` refuse a profile supplied as a **file**
unless it carries a detached signature that verifies against a key in the trust
store; an unsigned file, a file whose signature does not parse, and a file signed
by an untrusted key are all rejected before anything is scanned or applied. When
the value is `0` or absent, file profiles are accepted as before.

The policy never restricts the **built-in** profiles (`home`,
`enterprise-workstation`, and so on) or the level names (`basic`..`paranoid`):
they ship inside the product itself, not as operator files, so there is nothing
to sign. Only the file form is gated.

The product ships a Group Policy administrative template,
[`admx/OpenTheWindows.admx`](../admx/OpenTheWindows.admx) with
[`admx/en-US/OpenTheWindows.adml`](../admx/en-US/OpenTheWindows.adml), that
exposes this setting. Copy both into the PolicyDefinitions store (a machine's
`%SystemRoot%\PolicyDefinitions` or the domain Central Store) and the setting
appears under **Computer Configuration → Administrative Templates → Open the
Windows**. Pair it with your organisation's public key in the trust store so that
only profiles your administrators signed are honoured on managed devices.

## CLI reference

```text
otw profile list [--json]                 List the built-in profiles.
otw profile show <profile> [--json]       Show one profile's dial, include/exclude, scope, options, applicability.
otw profile validate <profile> [--json] [--catalog-dir <dir>]
                                          Validate against the schema and the catalogue.
otw profile sign <file> --key <pem> [--out <sig>]
                                          Sign a profile file (writes <file>.sig).
otw profile verify <file> [--key <pem>] [--sig <path>]
                                          Verify a signature (trust store when --key is omitted).
```

`<profile>` is a built-in id or a file path; `sign`/`verify` require a file.
Exit codes: `0` ok; `4` invalid input (unknown profile, unreadable file, a
profile that fails validation, or a signature that does not verify).
</content>
</invoke>
