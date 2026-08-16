# M1 — Catalogue model, schema, loader, `otw catalog`

Status: **in progress** (2026-08-16). The working tree contains uncommitted
M1 code that builds except for the four items listed under "Current WIP".

## Goal

The data model from ADR 0004 as C# records + JSON Schema, an embedded built-in
catalogue with directory overrides, structural validation with stable rule ids,
and read-only CLI commands. Nothing in M1 changes the machine.

## Preconditions

- M0 certified; `pwsh build/quality.ps1` green.
- Read: ADR 0004; PLAN.md §5.2, §5.4; `docs/catalog-format.md`.

## Files (create / modify)

| Path | Purpose |
| ------ | --------- |
| `catalog/schema/tweak.schema.json` | JSON Schema draft 2020-12; `additionalProperties:false` everywhere; enum values PascalCase; `kind`-discriminated `oneOf` for actions |
| `catalog/<category>/*.json` | `{ "$schema": "../schema/tweak.schema.json", "entries": [ ... ] }` |
| `src/OpenTheWindows.Core/Catalog/*.cs` | model (see "Types") |
| `src/OpenTheWindows.Core/Catalog/Actions/*.cs` | `ITweakAction` + one record per kind |
| `src/OpenTheWindows.Core/OpenTheWindows.Core.csproj` | `<EmbeddedResource Include="..\..\catalog\**\*.json" LogicalName="catalog/%(RecursiveDir)%(Filename)%(Extension)" />`; `PackageReference JsonSchema.Net` |
| `src/OpenTheWindows.Cli/CliServices.cs`, `Commands/CatalogCommand.cs` | CLI wiring |
| `tests/OpenTheWindows.Core.Tests/Catalog/*Tests.cs` | see "Tests" |
| `tests/OpenTheWindows.Cli.Tests/CatalogCommandTests.cs` | see "Tests" |
| `docs/catalog-format.md` | authoring guide (exists) |
| `build/quality.ps1` | add `catalog` gate |

## Types (exact names; namespace `OpenTheWindows.Core.Catalog` unless noted)

- Enums: `WindowsEdition {Home, Pro, Enterprise, Education}`; `TweakStatus
  {Draft, Verified, Deprecated}`; `RegistryHive {LocalMachine, User}`;
  `RegistryValueType {Dword, Qword, Sz, ExpandSz, MultiSz, Binary}` (no
  "String" — CA1720); `RegistryValueKind {Preference, Policy}`;
  `ServiceStartType {Automatic, AutomaticDelayed, Manual, Disabled}`;
  `AppxRemovalMode {CurrentUser, AllUsersAndDeprovision}`; `CatalogOrigin
  {BuiltIn, Override}`; `CatalogIssueSeverity {Error, Warning}`.
- `static class WindowsEditions { WindowsEdition? FromEditionId(string?) }` —
  maps registry `EditionID` (Core→Home, Professional/ProfessionalWorkstation/
  ProfessionalEducation→Pro, Enterprise/EnterpriseS/IoTEnterprise→Enterprise,
  Education→Education; unknown → null, never guess).
- `record Applicability(IReadOnlyList<WindowsEdition> Editions, int? MinBuild,
  int? MaxBuild, IReadOnlyList<string> Architectures)` with `static Any` and
  `bool Matches(OperatingSystemFacts)`.
- `record Verification(int Build, WindowsEdition Edition, DateOnly Date, string Evidence)`.
- `record ManagedBy(string? PolicyCsp, string? GroupPolicyPath)`.
- `interface ITweakAction { string Kind { get; } }` in `Catalog.Actions`,
  `[JsonPolymorphic(TypeDiscriminatorPropertyName="kind",
  UnknownDerivedTypeHandling=FailSerialization)]` + one `[JsonDerivedType]` per
  record: `RegistryAction(RegistryHive Hive, string Path, string Name,
  RegistryValueType Type, JsonElement Value, JsonElement Default,
  RegistryValueKind ValueKind)`, `ServiceAction(string Name, ServiceStartType
  StartType, ServiceStartType Default, bool StopIfRunning)`,
  `ScheduledTaskAction(string Path, bool Enabled, bool Default)`,
  `AppxAction(string PackageFamilyName, AppxRemovalMode Mode, string
  ReinstallHint)`, `OptionalFeatureAction(string Name, bool Enabled, bool
  Default)`, `DefenderPreferenceAction(string Property, JsonElement Value,
  JsonElement Default)`, `PowerSettingAction(Guid SubgroupGuid, Guid
  SettingGuid, uint AcValue, uint DcValue)`, `CommandAction(string Executable,
  IReadOnlyList<string> Arguments, IReadOnlyList<string> RevertArguments)`
  with `static IReadOnlySet<string> AllowedExecutables` = powercfg.exe,
  netsh.exe, auditpol.exe, secedit.exe, dism.exe, fsutil.exe.
- `record TweakDefinition(TweakId Id, string Title, string Description, string
  Rationale, Category Category, Level Level, RiskTier Risk, Scope Scope,
  TweakStatus Status, Applicability AppliesTo, IReadOnlyList<ITweakAction>
  Actions, RestartRequirement Requires, IReadOnlyList<TweakId> DependsOn,
  IReadOnlyList<TweakId> ConflictsWith, ManagedBy? ManagedBy,
  IReadOnlyList<string> SideEffects, IReadOnlyList<Uri> Sources,
  IReadOnlyList<Verification> VerifiedOn, IReadOnlyList<string> Tags)`.
- `record CatalogDocument([JsonPropertyName("$schema")] string? Schema,
  IReadOnlyList<TweakDefinition> Entries)`.
- `record CatalogSource(string Name, string Json, CatalogOrigin Origin)`.
- `record CatalogIssue(CatalogIssueSeverity Severity, string Source, string
  Location, string Rule, string Message)`.
- `record CatalogLoadResult(TweakCatalog? Catalog, IReadOnlyList<CatalogIssue>
  Issues)` with `IsValid`, `Errors`, `Warnings`, `GetCatalogOrThrow()`.
- `record CatalogValidationReport(bool IsValid, int EntryCount,
  IReadOnlyList<CatalogIssue> Issues)` with `static From(CatalogLoadResult)`.
- `sealed class TweakCatalog` (not `Catalog` — CA1724/MA0049): `Entries`,
  `Count`, `TryGet`, `Get`, `InCategory`, `Select(Category, Level, bool
  includeDraft)` (level ≤ dial, not Off, not Deprecated, Draft only if asked),
  `ApplicableTo(OperatingSystemFacts)`.
- `static class ProtectedServices { IReadOnlySet<string> Names; bool
  IsProtected(string) }` — the never-touch list (PLAN D21).
- `static class CatalogValidator { IReadOnlyList<CatalogIssue>
  Validate(IReadOnlyList<TweakDefinition>, Func<TweakDefinition,string>) }`.
- `static class CatalogLoader { LoadBuiltIn(); LoadWithDirectory(string);
  Load(IEnumerable<CatalogSource>); ValidateSchema(CatalogSource);
  SchemaText }` — schema evaluated with JsonSchema.Net (`JsonSchema.FromText`,
  `Evaluate(JsonElement, new EvaluationOptions { OutputFormat = List })`),
  then STJ deserialisation via `CatalogJsonContext` (source-generated,
  camelCase properties, `UseStringEnumConverter`, `UnmappedMemberHandling =
  Disallow`, comments/trailing commas allowed), then `CatalogValidator`.
  Override precedence: `Override` origin replaces `BuiltIn` by id; duplicate
  ids within one origin → `unique-id` error.
- `TweakId` gets `[JsonConverter(typeof(TweakIdJsonConverter))]`.

## Validator rules (stable ids; each needs a negative test)

`unique-id`, `title-required`, `title-length` (≤ 80), `description-required`,
`rationale-required`, `level-off`, `breaking-level` (Breaking ⇒ Strict/Paranoid),
`advanced-level` (Advanced ⇒ not Basic), `side-effects-expected` (warning:
Caution+ without sideEffects), `self-reference`, `unknown-reference`,
`sources-required`, `source-https`, `verified-evidence` (Verified ⇒ ≥1
verifiedOn; evidence non-empty), `verified-build` (≥ 22000),
`actions-required`, `protected-service`, `command-allowlist`, `revert-required`
(command needs revertArguments), `task-path` (starts with `\`),
`appx-family-name` (contains `_`), `registry-path` (no leading `\`, no `HKEY_`,
no `..`), `scope-hive` (User hive ⇒ scope User/AllUsers; LocalMachine ⇒
Machine), `policy-path` (warning: Policy kind outside `\Policies\` or
Preference kind inside), `default-required` (registry `default` present; JSON
null = absent), `registry-value-type` (value/default match `type`; Dword ≤
0xFFFFFFFF; MultiSz array of strings; Binary base64), plus `schema` (any JSON
Schema violation, location = JSON pointer) and `json` (parse errors).

## CLI

- `otw catalog list [--category <Category>] [--level <Level>] [--catalog-dir <dir>] [--json]`
  — table `ID  CATEGORY LEVEL RISK STATUS TITLE` + count; JSON = array of
  `TweakDefinition` (via `CatalogJsonContext`). Exit 0; 4 if catalogue invalid.
- `otw catalog show <id> [--catalog-dir] [--json]` — human block (id, tags in
  brackets, title, description, why, requires, applies-to, actions, side
  effects, managed by, sources, verified on) or JSON of the entry. Exit 0;
  4 for malformed id, unknown id, invalid catalogue.
- `otw catalog validate [--catalog-dir] [--json]` — prints every issue then
  `Catalogue valid: N entries, W warning(s).` or `Catalogue INVALID: E
  error(s), W warning(s).`; JSON = `CatalogValidationReport`. Exit 0 valid,
  4 invalid.
- `--catalog-dir` and `--json` are recursive options on `catalog`.
- Wiring: `CliServices(DoctorService Doctor, Func<string?, CatalogLoadResult>
  LoadCatalog)`; `CommandLineBuilder.Build(CliServices)`; tests pass fakes.

## Initial catalogue content (all `Draft`, ≥ 1 source each)

Already authored (24 entries): privacy ×9 (`diagnostic-data.json`,
`personalization.json`), updates ×4, security ×6, performance ×4, debloat ×3,
shell ×3. Do not add more in M1; M4 populates from research.

## Tests (must exist; names indicative)

- `Core.Tests/Catalog/BuiltInCatalogTests`: `LoadBuiltIn` valid, count ≥ 20,
  every category represented, every entry has ≥1 source, no `Verified` entry
  without evidence, ids unique, all sources https.
- `Core.Tests/Catalog/CatalogValidatorTests`: one `[Theory]` row per rule id
  above producing exactly that rule; a valid entry produces no errors.
- `Core.Tests/Catalog/CatalogLoaderTests`: schema violation reported with JSON
  pointer; unknown action kind fails; unknown property fails
  (`UnmappedMemberHandling`); comments/trailing commas accepted; override dir
  replaces built-in by id; duplicate within override → `unique-id`; missing
  directory → `directory-missing`; `GetCatalogOrThrow` throws with issues.
- `Core.Tests/Catalog/ApplicabilityTests` + `WindowsEditionsTests`: edition
  mapping incl. unknown → null; min/max build; architecture; empty = any.
- `Core.Tests/Catalog/TweakCatalogTests`: `Select` respects Off, level order,
  Deprecated exclusion, Draft inclusion flag.
- `Core.Tests/Catalog/CatalogSerializationTests`: round-trip
  `TweakDefinition` through `CatalogJsonContext`; `TweakId` converter rejects
  invalid ids with `JsonException`.
- `Core.Tests/Catalog/CatalogPerformanceTests`: loading 1 000 generated
  entries < 200 ms (Stopwatch; skip on CI slowness is NOT allowed — keep the
  budget generous but real).
- `Cli.Tests/CatalogCommandTests`: list table + `--json` + filters; show
  text + json + unknown id (exit 4) + malformed id (exit 4); validate valid
  (exit 0) and invalid dir (exit 4) with issues on stdout.

## Gates

- All M0 gates + new `catalog` gate in `build/quality.ps1`: run the built
  `otw.exe catalog validate` (fail on non-zero) — prove it fails by pointing
  `--catalog-dir` at a directory with a broken file, record in PLAN §7.2.
- Coverage thresholds unchanged (85/75).

## Docs

`docs/catalog-format.md` (exists — keep in sync with the schema), README
"Development" gains the three commands, CHANGELOG `[Unreleased]`, PLAN §8 M1
status + §7.1 (JsonSchema.Net 9.4.0 already listed).

## Security / performance checks

- Override directory loading: only `*.json`, no symlink following beyond
  `Directory.EnumerateFiles`; file names shown in issues but never executed.
- `CommandAction` allow-list enforced by schema **and** validator (defence in depth).
- Load 1 000 entries < 200 ms (test).

## Acceptance criteria

1. `otw catalog validate` → exit 0, `Catalogue valid: 24 entries` (warnings allowed, listed).
2. `otw catalog validate --catalog-dir tests/fixtures/catalog-bad` → exit 4 with rule ids.
3. `otw catalog list --category Privacy --level Basic` lists only Basic privacy entries.
4. `otw catalog show privacy.advertising-id.disable --json` round-trips through the schema.
5. All tests green; coverage ≥ 85/75; `quality.ps1` green incl. `catalog` gate.

## Current WIP (uncommitted on 2026-08-16) — finish these first

Build errors left when coding was paused:

1. `CatalogCommand.cs(131)`: `IDE0007` — use `var` for the `JsonSerializer`/obvious-type local.
2. `CatalogCommand.cs(172)`: `CA1859` — `WriteTable` parameter type `List<TweakDefinition>`.
3. `RT0002` "ProjectReference Core/Windows can be removed" — from
   `tests/OpenTheWindows.Cli.Tests` (Core/Windows types are reached through
   the Cli project); remove the direct references it does not need, or, if
   the test uses Core types directly, keep Core and remove Windows.
4. Then: write the tests listed above, add the `catalog` gate, docs, commit.
