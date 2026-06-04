# Contributing to OffensivePipeline

Thanks for helping keep OffensivePipeline alive! Contributions of all kinds are
welcome: bug fixes, new plugins, new tool templates, and documentation.

## Getting started

1. Install the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).
2. Clone the repository (with the local NuGet source already configured via
   `nuget.config`, which points to the bundled `DonutCore` package in
   `ExternalResources/`).
3. Build:

   ```bash
   dotnet build OffensivePipeline.sln -c Release
   ```

> Note: the **build/obfuscation pipeline itself runs on Windows** (it shells out to
> `cmd.exe`, MSBuild Build Tools and the ConfuserEx CLI). The project compiles on any
> platform, but end-to-end tool building is expected to be run on Windows.

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

Add the new file to the `<ItemGroup>` of `None Update` entries in
`OffensivePipeline/OffensivePipeline.csproj` so it is copied to the output folder.

## Pull requests

- Branch off `main`.
- Keep changes focused and described in the PR template.
- Make sure `dotnet build OffensivePipeline.sln -c Release` succeeds (CI enforces this).
- Be mindful that this is a security tool — only submit tooling/templates intended for
  authorised testing.

## Code style

Follow the existing style and the rules in `.editorconfig`.
