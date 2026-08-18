# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [3.0.0] - 2026-08-17

Full modernization pass. The offensive workflow and all 79 YAML templates are unchanged,
but the way the tool is configured, how it reports success or failure, and what it exits
with are all different. **Read the breaking changes before upgrading an existing install.**

### Breaking

- **Requires the .NET 10 runtime.** The target framework moves from `net8.0` to `net10.0`
  (LTS, supported until November 2028). A machine with only .NET 8 installed cannot run
  the release. Releases now also ship a **self-contained** win-x64 zip that needs no
  runtime at all.
- **`appsettings.json` replaces `OffensivePipeline.dll.config`.** Settings moved from
  `<appSettings>` XML into the `OffensivePipeline` section of `appsettings.json`; the key
  names are unchanged. The legacy file is *still read* and still takes effect: it outranks the
  shipped `appsettings.json` defaults, so a customised `BuildCSharpTools` keeps working across
  the upgrade instead of being silently replaced by the shipped default. Only a local
  `appsettings.Local.json` outranks it. The tool prints a deprecation warning naming every
  legacy key still in force. **This fallback is removed in v3.1.0** — see
  [README](README.md#migrating-from-offensivepipelinedllconfig) for a side-by-side migration
  table.
- **The `Version` app setting is gone.** The banner reads the assembly's own
  `AssemblyInformationalVersion` (with the SourceLink commit suffix trimmed), so a stale
  config file can no longer misreport which build is running. `--version` also exists for
  the first time.
- **Build and obfuscation failures are now reported as failures.** `Helpers.ExecuteCommand`
  always returned `true`, so every `if (!ExecuteCommand(...))` guard in `BuildCsharp` and
  `ConfuserEx` was dead code: a failed MSBuild or ConfuserEx run printed `[+] No errors!`
  and a green SUMMARY. `Donut` likewise discarded `Donut_Create`'s return code. Runs that
  used to look clean will now correctly report `ERROR`.
- **The process returns meaningful exit codes.** Previously *everything* exited 0 except an
  unknown verb or an unknown tool, which aborted the runtime with exit 134 and a stack
  trace. Now: a failed `all`/`t` returns 1; an unknown verb, an unknown tool or `t` with no
  tool name returns 1 with a readable message; help, `list` and `clean` return 0. A CI
  wrapper or `&&` chain that has "passed" for years may start failing — that is the point.
- **The command-line library changed** from the maintenance-mode
  `McMaster.Extensions.CommandLineUtils` to `System.CommandLine` 2.0.11 (GA). The verb
  surface is byte-identical (`list`, `all`, `t <tool> [-a|--args <args>]`, `clean`, with
  `-?`/`-h`/`--help` everywhere and the Examples block still in the root help), but the
  wording of unknown-verb and unknown-option errors is now System.CommandLine's.
- **`t <tool>` is case-insensitive** and reports a clean "not found" message instead of
  crashing. `t seatbelt` previously worked only because Windows filesystems are
  case-insensitive; on Linux it threw.
- **`log.txt` changed format.** Timestamps are ISO-8601 (`DateTimeOffset.Now.ToString("O")`)
  and the line layout differs. Anything parsing the log needs updating. Diagnostic lines no
  longer reach the console unless `--verbose` is passed (or `OFFENSIVEPIPELINE_VERBOSE` is
  set); they always went to the log file regardless.
- **`.crproj` files are written to the output directory**, not alongside the built exe.
  `Path.Combine(OutputPath, exe + ".crproj")` silently discarded `OutputPath` because `exe`
  was already rooted.
- **`nuget.exe` is pinned to a specific version** (`v6.14.0` instead of `latest`), and runtime
  downloads can be **SHA-256 verified** against hashes in `appsettings.json`. The ConfuserEx CLI
  ships with `ConfuserExSha256` populated and is verified before use; `NugetSha256` ships empty,
  so `nuget.exe` is downloaded unverified and prints a warning saying so until an operator pins
  it. An operator pointing at a different ConfuserEx build must update `ConfuserExSha256`.
- **`list` output is alphabetically ordered** and stable across platforms, instead of
  following filesystem enumeration order.
- **Generated fake copyright years** use a rolling range anchored on the current year
  rather than the hardcoded 2018-2021, which was conspicuous on a 2026 binary.

### Fixed

- The build and obfuscation modules could not run at all in a published build:
  `Microsoft.Build` was referenced with `ExcludeAssets="runtime"`, so `BuildCsharp.Run` and
  `ConfuserEx.SolveDependences` threw `FileNotFoundException: Could not load file or
  assembly 'Microsoft.Build'` before their `try` blocks were entered. The dependency on the
  MSBuild engine is now gone entirely, replaced by a small `SolutionFileReader` that parses
  the `Project(...)` lines of a `.sln` with a source-generated regex.
- `Helpers.ExecuteCommand` read stdout to completion before touching stderr and never
  called `WaitForExit()`. A chatty child such as MSBuild could fill the stderr pipe and
  deadlock. Both streams are now read concurrently and the process handle is disposed.
- `Helpers.CopyDirectory` always returned `true`: the catch logged but never set the status,
  and the recursive call discarded its result, so a half-copied tree was built anyway.
- A module throwing left `Status` true and the run summary recorded only the *last* module's
  status (`=` instead of `&=`), so tool-level failures never surfaced.
- `t <unknown-tool>` read a non-existent file outside the `try` and crashed.
- `RandomAssemblyInfo` read the same file twice, discarding the first result.
- `RandomGuid` rebuilt its GUID map once per file and relied on an empty `catch {}` to
  swallow duplicate-key exceptions as flow control.
- `GitHelpers` silently skipped the clone (and built a stale tree) when a locked checkout
  could not be deleted; that case is now a loud failure. `clean` no longer dies with a raw
  stack trace on a locked file.
- The reflection plugin lookup used `.First(...)`, which throws, behind an unreachable null
  check; an unknown plugin name in a template now reports which template it came from,
  before any work starts.
- A single malformed or empty `Tools/*.yml` template aborted the entire tool: `yaml.Load` and
  the structural casts ran outside any `try`, so one syntax error took down `list`, `all` and
  `validate` for every other template. A bad template is now reported and skipped.

### Added

- **`--json` output on `list` and `validate`.** Writes a single JSON document to stdout - the
  banner is suppressed and every human line is routed to stderr - so `list --json | jq` and CI
  consumers get clean machine output. `list --json` deliberately omits `authUser`/`authToken`,
  and `validate --json` returns the structured pass/fail report and still exits non-zero on an
  invalid template.
- **New `validate` verb.** Checks every `Tools/*.yml` template - required fields, an http(s) or
  local git link, a `.sln` solution path, and only known plugin names - without cloning or
  building, on any platform. Exits non-zero on the first invalid template, so it doubles as a
  pre-flight check and a CI gate.
- **First test suite**: `tests/OffensivePipeline.Tests`, xUnit v3, all runnable on Linux with no
  network. Includes a per-template theory over all 79 shipped YAML files, golden
  tests for the new `.sln` reader, text-transform tests for `RandomGuid`/`RandomAssemblyInfo`,
  assertions on the exact generated `buildSolution.bat` and `.crproj` content via a recording
  process-runner fake, configuration-precedence tests, and parse-only CLI tests encoding the
  exit-code matrix. Run with `dotnet test --solution OffensivePipeline.sln -c Release`.
- **Dependency injection and a real composition root.** A plain `ServiceCollection` (no
  generic host) wires `IConsoleUi`, `ILogger<T>`, `PipelinePaths`, `PipelineOptions`,
  `IProcessRunner`, `IResourceDownloader` and `IGitClient`. Modules are resolved by keyed DI
  under their template names instead of an `Assembly.GetTypes()` scan that ran 395 times for
  `all`, and their directory-creating side effects moved out of constructors.
- **Presentation and diagnostics are now separate.** `IConsoleUi` owns everything the operator
  sees, with the original colour scheme preserved and colour suppressed when output is
  redirected; `ILogger<T>` owns `log.txt` through a single buffered writer held open for the
  process, replacing a `FileStream` opened and closed per line.
- **`--verbose`** (and `OFFENSIVEPIPELINE_VERBOSE`) to echo diagnostics to the console.
- **`--version`.**
- **Central package management** (`Directory.Packages.props`), shared build properties
  (`Directory.Build.props`), `global.json` pinning the SDK, deterministic builds, embedded
  PDBs, repository metadata in the informational version, NuGet auditing at `low` severity
  across the full transitive graph (failing CI on `NU1901`-`NU1904`), and .NET analyzers at
  `latest-Recommended`. The solution builds with zero warnings.
- **`packageSourceMapping` in `nuget.config`**, restricting the `DonutCore` package ID to the
  vendored folder feed. The ID is unclaimed on nuget.org, so restore could previously have
  been redirected to a squatted package of the same name.
- **`ExternalResources/README.md`** recording the provenance, SHA-256, dependencies and known
  liabilities of the vendored `DonutCore.1.0.1.nupkg`.
- **CI/CD hardening.** `build` now runs on `windows-latest` *and* `ubuntu-latest`, runs the
  test suite, smoke-tests the CLI (79 templates listed, four verbs, `@aetsu` credit intact)
  and asserts the published payload is complete. `release` builds and tests before publishing,
  produces both a framework-dependent and a self-contained zip plus `SHA256SUMS.txt`, and
  attaches a build-provenance attestation. New `codeql` (C#, weekly + on PR) and
  `dependency-review` (PRs, fails on high severity) workflows. Every action is pinned to a
  commit SHA, every workflow declares least-privilege permissions and a concurrency group,
  and the release is created with the built-in `gh` CLI instead of a third-party action.
- **`LICENSE` now ships inside the release archive** alongside `README.md`, as GPL-3.0
  conveyance requires.

### Changed

- Dependencies: `LibGit2Sharp` 0.31.0 → 0.32.0, `YamlDotNet` 18.0.0 → 18.1.0,
  `Microsoft.Build` 17.11.4 → 18.9.6 (and then removed from the code path),
  `McMaster.Extensions.CommandLineUtils` 4.1.1 → removed, `System.Configuration.ConfigurationManager`
  8.0.1 → no longer referenced directly. Added `Microsoft.Extensions.*` 10.0.11
  (Configuration, Configuration.Json, Configuration.Binder, DependencyInjection, Logging,
  Logging.Console) and `System.CommandLine` 2.0.11. The `NU1903` high-severity advisory
  against `Microsoft.Build` 17.11.4 is resolved, and a transitive pin keeps a 2018-era
  `System.Security.Cryptography.Pkcs` asset from `PeNet` 1.1.1 out of the output.
- `Tools/*.yml` and `Resources/**` are copied to the output by a glob rather than a
  hand-maintained `<None Update>` list of 91 entries. **New tool templates are picked up
  automatically** — contributors no longer edit the csproj.
- Assets copy with `PreserveNewest` instead of `Always`, so a template edited in the output
  folder is no longer clobbered on every rebuild.
- Dependabot now covers the solution root, the app and the test project, groups
  `Microsoft.Extensions.*` and test packages, and waits seven days before proposing a
  freshly published version.
- Removed the stale `FolderProfile.pubxml` publish profile (targeted `net6.0`,
  `SelfContained=true`, and a hardcoded desktop path).

### Known follow-ups

- `Microsoft.Build` is still listed as a `PackageReference` even though no code uses it; the
  reference (and the `System.Configuration.ConfigurationManager` / `System.Diagnostics.EventLog`
  assemblies it drags into the publish output) can be dropped.
- `TreatWarningsAsErrors` is not enabled yet, so the zero-warning state is not enforced by
  the compiler; only the NuGet audit codes are treated as errors, and only in CI.
- `packages.lock.json` / `--locked-mode` restores are not in place, so CI caches NuGet by a
  hash of `Directory.Packages.props` and the project files rather than by a lock file.
- `DonutCore` remains the principal supply-chain liability: an unsigned 2019-era
  `netcoreapp2.1` build from an unmaintained fork. See `ExternalResources/README.md`.
- The `RandomGuid` regex also matches bare 32-hex runs, not just GUIDs.
- YAML parsing still walks the representation model by hand rather than deserializing into
  `ToolConfig`.
- The output root is fixed to `AppContext.BaseDirectory`, so the tool cannot run from a
  read-only or shared install.

## [2.1.0] - 2026-06-04

Maintenance / revival release. No changes to the offensive workflow or YAML
templates; focus is on bringing the project onto a supported, buildable platform.

### Changed
- Upgraded target framework from **.NET 6.0** (end of life) to **.NET 8.0 (LTS)**.
- Updated all NuGet dependencies to current versions:
  - `LibGit2Sharp` 0.26.2 → 0.31.0
  - `McMaster.Extensions.CommandLineUtils` 4.0.2 → 4.1.1
  - `Microsoft.Build` 17.4.0 → 17.11.4
  - `System.Configuration.ConfigurationManager` 7.0.0 → 8.0.1
  - Migrated from the unmaintained `YamlDotNet.NetCore` 1.0.0 to `YamlDotNet` 18.0.0
- Modernised the obsolete `WebClient` download path to `HttpClient`.
- Updated `GitHelpers` for the new LibGit2Sharp `CloneOptions.FetchOptions` API.

### Added
- `nuget.config` so the bundled `DonutCore` package resolves from `ExternalResources/`
  for reproducible restores (no manual NuGet feed setup required).
- GitHub Actions CI (`build`) and release (`release`) workflows.
- Dependabot configuration for NuGet and GitHub Actions.
- Community health files: `CONTRIBUTING.md`, `CODE_OF_CONDUCT.md`, `SECURITY.md`,
  issue/PR templates, `.editorconfig`, and this changelog.

## [2.0.0] - 2023

- Almost complete code rewrite.
- Cloning from private repositories (authentication via GitHub authToken).
- Copy a local folder instead of cloning from a remote repository.
- New module to generate shellcodes with Donut.
- New modules to randomise application GUIDs and AssemblyInfo.
- 60 new tools added.
