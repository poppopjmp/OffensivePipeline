# Contributing to OffensivePipeline

Thanks for helping keep OffensivePipeline alive! Contributions of all kinds are
welcome: bug fixes, new plugins, new tool templates, and documentation.

## Getting started

1. Install the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0). The exact
   version is pinned by `global.json`, so the SDK you get is the SDK CI uses.
2. Clone the repository. The bundled `DonutCore` package resolves from `ExternalResources/`
   via the repository's `nuget.config`, which also pins that package ID to the local folder
   feed — no NuGet feed setup, and no way for restore to pick up a same-named package from
   nuget.org.
3. Build and test:

   ```bash
   dotnet build OffensivePipeline.sln -c Release
   dotnet test --solution OffensivePipeline.sln -c Release
   ```

   Note the `--solution` flag: `dotnet test` requires it when given a solution under the
   test runner this repository selects in `global.json`.

The build must stay warning-free — analyzers run at `latest-Recommended`, and CI treats the
NuGet audit codes (`NU1901`-`NU1904`) as errors.

> Note: the **build/obfuscation pipeline itself runs on Windows** (it shells out to
> `cmd.exe`, MSBuild Build Tools and the ConfuserEx CLI). The project compiles and the test
> suite runs on any platform — CI builds and tests on both `windows-latest` and
> `ubuntu-latest` — but end-to-end tool building is expected to be run on Windows.

## Tests

The suite lives in `tests/OffensivePipeline.Tests` (xUnit v3). It must stay **offline and
platform-independent**:

- Nothing may reach `github.com` or `dist.nuget.org`. Use the `IGitClient` and
  `IResourceDownloader` seams, and never resolve the real `IProcessRunner`.
- Tests run in parallel, so anything touching the filesystem must build its own
  `PipelinePaths` rooted at a unique temp directory. Do not rely on `AppContext.BaseDirectory`.
- Genuinely Windows-bound cases use the `WindowsOnlyFact` attribute so the suite stays green
  on Linux.

The 79 shipped templates are linked into the test output rather than copied, so a template
test always asserts against the exact files that ship. Adding a tool automatically adds a
test case for it.

## Adding a new tool

Tools are described by YAML templates in `OffensivePipeline/Tools/`. Copy an existing
template (for example `Seatbelt.yml`) and adjust the fields:

```yaml
tool:
  - name: MyTool
    description: Short description of the tool.
    gitLink: https://github.com/author/MyTool
    solutionPath: MyTool\MyTool.sln
    language: c#
    plugins: RandomGuid, RandomAssemblyInfo, BuildCsharp, ConfuserEx, Donut
    authUser:
    authToken:
    toolArguments:
```

New templates under `OffensivePipeline/Tools/` are picked up automatically — the csproj
copies them with a glob, so **no csproj edit is needed**. `plugins:` entries must be one of
`RandomGuid`, `RandomAssemblyInfo`, `BuildCsharp`, `ConfuserEx`, `Donut`. Anything else is
reported before any cloning or building starts, and fails the shipped-template test.

## Out of scope

These have been evaluated and deliberately rejected; please do not open PRs for them:

- **`PublishSingleFile`** — buys nothing. `Tools/`, `Resources/` and `appsettings.json` stay
  loose on disk by design, so the deployment is a folder either way.
- **NativeAOT** — impossible while LibGit2Sharp P/Invoke and reflection-based YAML parsing
  exist.
- **`PackAsTool`** — the tool writes `Git/`, `Output/` and `log.txt` next to itself; as a
  global tool it would write them into `~/.dotnet/tools/.store`.

## Pull requests

- Branch off `main`.
- Keep changes focused and described in the PR template.
- Make sure both `dotnet build OffensivePipeline.sln -c Release` and
  `dotnet test --solution OffensivePipeline.sln -c Release` succeed (CI enforces both, on
  Windows and Linux).
- Be mindful that this is a security tool — only submit tooling/templates intended for
  authorised testing.

## Code style

Follow the existing style and the rules in `.editorconfig`.
