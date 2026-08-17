# OffensivePipeline

![](img/banner.PNG)

[![build](https://github.com/poppopjmp/offensivepipeline/actions/workflows/build.yml/badge.svg)](https://github.com/poppopjmp/offensivepipeline/actions/workflows/build.yml)
[![codeql](https://github.com/poppopjmp/offensivepipeline/actions/workflows/codeql.yml/badge.svg)](https://github.com/poppopjmp/offensivepipeline/actions/workflows/codeql.yml)
[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4)](https://dotnet.microsoft.com/download/dotnet/10.0)
[![License: GPL v3](https://img.shields.io/badge/License-GPLv3-blue.svg)](LICENSE)
[![Maintained](https://img.shields.io/badge/maintained-yes-brightgreen.svg)](https://github.com/poppopjmp/offensivepipeline/commits/main)

**OffensivePipeline** allows you to download and build C# tools, applying certain modifications in order to improve their evasion for Red Team exercises.   
A common use of OffensivePipeline is to download a tool from a Git repository, randomise certain values in the project, build it, obfuscate the resulting binary and generate a shellcode.

> **Project status:** actively maintained. The codebase targets **.NET 10 (LTS)**, has a test
> suite, and CI builds and tests on Windows and Linux on every push. Maintained by
> [@poppopjmp](https://github.com/poppopjmp) (van1sh), continuing the original work of
> [@aetsu](https://github.com/aetsu). See [CHANGELOG.md](CHANGELOG.md) for details.
>
> **Upgrading from 2.x?** Version 3.0.0 has breaking changes: it needs the **.NET 10 runtime**,
> configuration moved from `OffensivePipeline.dll.config` to
> [`appsettings.json`](#configuration), and the tool now returns
> [non-zero exit codes](#exit-codes) and correctly reports build failures that earlier versions
> silently swallowed.

## Features

- Currently only supports C# (.Net Framework) projects
- Allows to clone public and private (you will need credentials :D) git repositories
- Allows to work with local folders
- Randomizes project GUIDs
- Randomizes application information contained in AssemblyInfo
- Builds C# projects
- Obfuscates generated binaries
- Generates shellcodes from binaries
- There are 79 tools parameterised in YML templates (not all of them may work :D)
- New tools can be added using YML templates
- It should be easy to add new plugins...

## What's new in version 3.0

- Targets **.NET 10 (LTS)**, supported until November 2028 — the release now requires the
  .NET 10 runtime, or you can use the new **self-contained** zip, which bundles it
- Configuration moved from `OffensivePipeline.dll.config` to **`appsettings.json`**; the old
  file still works for this release only, with a deprecation warning ([migration](#migrating-from-offensivepipelinedllconfig))
- **Build and obfuscation failures are finally reported as failures.** Previous versions
  ignored MSBuild's, ConfuserEx's and Donut's return codes and printed `[+] No errors!`
  regardless
- **Real exit codes**, so `OffensivePipeline.exe all && next-step` now behaves ([table](#exit-codes))
- Runtime downloads can be **SHA-256 verified**: the ConfuserEx CLI ships with its hash pinned,
  and `nuget.exe` warns that it is unverified until you pin `NugetSha256` yourself
- First **test suite** (267 tests, runs on Linux without network access), plus CodeQL and
  dependency-review in CI
- Command line migrated to `System.CommandLine`; same verbs, plus `--version` and `--verbose`
- `t <tool>` is case-insensitive and no longer crashes on an unknown tool; `list` is sorted
- New tool templates are picked up automatically — no more editing the csproj

## What's new in version 2.1

- Migrated from the end-of-life **.NET 6** to **.NET 8 (LTS)**
- All NuGet dependencies updated to current versions (LibGit2Sharp, Microsoft.Build, YamlDotNet, ...)
- Bundled `DonutCore` now resolves automatically via `nuget.config` — no manual NuGet feed setup
- GitHub Actions CI/release pipelines, Dependabot, and community health files added
- Obsolete `WebClient` download path replaced with `HttpClient`

## What's new in version 2.0

- Almost complete code rewrite (new bugs?)
- Cloning from private repositories possible (authentication via GitHub authToken)
- Possibility to copy a local folder instead of cloning from a remote repository
- New module to generate shellcodes with [Donut](https://github.com/TheWover/donut)
- New module to randomize GUIDs of applications
- New module to randomize the AssemblyInfo of each application
- 60 new tools added

## Examples

- List all tools:

```
OffensivePipeline.exe list
```

- Build all tools:

```
OffensivePipeline.exe all
```

- Build a tool

```
OffensivePipeline.exe t toolName
```

  The tool name is matched case-insensitively against the file names in `Tools/`, so
  `t seatbelt` and `t Seatbelt` both resolve `Seatbelt.yml`.

- Build a tool, overriding the shellcode arguments from the template

```
OffensivePipeline.exe t rubeus -a "-c All,GPOLocalGroup -d whatever.local"
OffensivePipeline.exe t rubeus --args "-c All,GPOLocalGroup -d whatever.local"
```

  `-a`/`--args` replaces the `toolArguments` value in the tool's YAML template. The arguments
  are embedded in the Donut shellcode.

- Clean cloned and built tools

```
OffensivePipeline.exe clean
```

- Echo diagnostics to the console as well as to `log.txt`

```
OffensivePipeline.exe --verbose all
```

  The `OFFENSIVEPIPELINE_VERBOSE` environment variable does the same thing.

- Show help or the version

```
OffensivePipeline.exe --help
OffensivePipeline.exe --version
```

### Exit codes

| Invocation | Exit code |
| --- | --- |
| no arguments, `-?` / `-h` / `--help`, `--version` | 0 |
| `list`, `clean` | 0 |
| `all` / `t <tool>` — everything succeeded | 0 |
| `all` / `t <tool>` — any tool or module failed | 1 |
| `t` with no tool name, unknown tool, unknown verb | 1 |
| any unhandled error | 1 |

> **Upgrading from 2.x:** every one of the non-zero rows used to be 0 (or an abrupt exit 134
> with a stack trace). Scripts that chained on success may start failing where they silently
> continued before.

### Output example

```
PS C:\OffensivePipeline> .\OffensivePipeline.exe t rubeus

                                                                                                   ooo
                                                                                           .osooooM M
      ___   __  __                _           ____  _            _ _                      +y.     M M
     / _ \ / _|/ _| ___ _ __  ___(_)_   _____|  _ \(_)_ __   ___| (_)_ __   ___           :h  .yoooMoM
    | | | | |_| |_ / _ \ '_ \/ __| \ \ / / _ \ |_) | | '_ \ / _ \ | | '_ \ / _ \          oo  oo
    | |_| |  _|  _|  __/ | | \__ \ |\ V /  __/  __/| | |_) |  __/ | | | | |  __/          oo  oo
     \___/|_| |_|  \___|_| |_|___/_| \_/ \___|_|   |_| .__/ \___|_|_|_| |_|\___|          oo  oo
                                                     |_|                            MoMoooy.  h:
                                                                                    M M     .y+
                                                                                    M Mooooso.
                                                                                    ooo

                                                                    @aetsu
                                                                                v3.0.0


[+] Loading tool: Rubeus
    Clonnig repository: Rubeus into C:\OffensivePipeline\Git\Rubeus
                 Repository Rubeus cloned into C:\OffensivePipeline\Git\Rubeus

    [+] Load RandomGuid module
        Searching GUIDs...
                > C:\OffensivePipeline\Git\Rubeus\Rubeus.sln
                > C:\OffensivePipeline\Git\Rubeus\Rubeus\Rubeus.csproj
                > C:\OffensivePipeline\Git\Rubeus\Rubeus\Properties\AssemblyInfo.cs
        Replacing GUIDs...
                File C:\OffensivePipeline\Git\Rubeus\Rubeus.sln:
                        > Replacing GUID 658C8B7F-3664-4A95-9572-A3E5871DFC06 with 3bd82351-ac9a-4403-b1e7-9660e698d286
                        > Replacing GUID FAE04EC0-301F-11D3-BF4B-00C04F79EFBC with 619876c2-5a8b-4c48-93c3-f87ca520ac5e
                        > Replacing GUID 658c8b7f-3664-4a95-9572-a3e5871dfc06 with 11e0084e-937f-46d7-83b5-38a496bf278a
                [+] No errors!
                File C:\OffensivePipeline\Git\Rubeus\Rubeus\Rubeus.csproj:
                        > Replacing GUID 658C8B7F-3664-4A95-9572-A3E5871DFC06 with 3bd82351-ac9a-4403-b1e7-9660e698d286
                        > Replacing GUID FAE04EC0-301F-11D3-BF4B-00C04F79EFBC with 619876c2-5a8b-4c48-93c3-f87ca520ac5e
                        > Replacing GUID 658c8b7f-3664-4a95-9572-a3e5871dfc06 with 11e0084e-937f-46d7-83b5-38a496bf278a
                [+] No errors!
                File C:\OffensivePipeline\Git\Rubeus\Rubeus\Properties\AssemblyInfo.cs:
                        > Replacing GUID 658C8B7F-3664-4A95-9572-A3E5871DFC06 with 3bd82351-ac9a-4403-b1e7-9660e698d286
                        > Replacing GUID FAE04EC0-301F-11D3-BF4B-00C04F79EFBC with 619876c2-5a8b-4c48-93c3-f87ca520ac5e
                        > Replacing GUID 658c8b7f-3664-4a95-9572-a3e5871dfc06 with 11e0084e-937f-46d7-83b5-38a496bf278a
                [+] No errors!


    [+] Load RandomAssemblyInfo module
        Replacing strings in C:\OffensivePipeline\Git\Rubeus\Rubeus\Properties\AssemblyInfo.cs
                [assembly: AssemblyTitle("Rubeus")] -> [assembly: AssemblyTitle("g4ef3fvphre")]
                [assembly: AssemblyDescription("")] -> [assembly: AssemblyDescription("")]
                [assembly: AssemblyConfiguration("")] -> [assembly: AssemblyConfiguration("")]
                [assembly: AssemblyCompany("")] -> [assembly: AssemblyCompany("")]
                [assembly: AssemblyProduct("Rubeus")] -> [assembly: AssemblyProduct("g4ef3fvphre")]
                [assembly: AssemblyCopyright("Copyright ©  2018")] -> [assembly: AssemblyCopyright("Copyright ©  2018")]
                [assembly: AssemblyTrademark("")] -> [assembly: AssemblyTrademark("")]
                [assembly: AssemblyCulture("")] -> [assembly: AssemblyCulture("")]


    [+] Load BuildCsharp module
        [+] Checking requirements...
        [*] Downloading nuget.exe from https://dist.nuget.org/win-x86-commandline/v6.14.0/nuget.exe
                [+] Download OK - nuget.exe
                [+] Path found - C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\Common7\Tools\VsDevCmd.bat
        Solving dependences with nuget...
        Building solution...
                [+] No errors!
                [+] Output folder: C:\OffensivePipeline\Output\Rubeus_vh00nc50xud


    [+] Load ConfuserEx module
        [+] Checking requirements...
        [+] Downloading ConfuserEx from https://github.com/mkaring/ConfuserEx/releases/download/v1.6.0/ConfuserEx-CLI.zip
                [+] Download OK - ConfuserEx
        Confusing...
                [+] No errors!


    [+] Load Donut module
        Generating shellcode...

Payload options:
        Domain: RMM6XFC3
        Runtime:v4.0.30319

Raw Payload: C:\OffensivePipeline\Output\Rubeus_vh00nc50xud\ConfuserEx\Donut\Rubeus.bin
B64 Payload: C:\OffensivePipeline\Output\Rubeus_vh00nc50xud\ConfuserEx\Donut\Rubeus.bin.b64

                [+] No errors!


    [+] Generating Sha256 hashes
                Output file: C:\OffensivePipeline\Output\Rubeus_vh00nc50xud


-----------------------------------------------------------------
                SUMMARY

 - Rubeus
         - RandomGuid: OK
         - RandomAssemblyInfo: OK
         - BuildCsharp: OK
         - ConfuserEx: OK
         - Donut: OK

-----------------------------------------------------------------
```

## Plugins

- **RandomGuid**: randomise the GUID in *.sln*, *.csproj* and *AssemblyInfo.cs* files
- **RandomAssemblyInfo**: randomise the values defined in *AssemblyInfo.cs*
- **BuildCsharp**: build c# project
- **ConfuserEx**: obfuscate c# tools
- **Donut**: use Donut to generate shellcodes. Arguments to embed in the shellcode come from the template's `toolArguments` field, or from `-a`/`--args` on the command line, which overrides it.

## Add a tool from a remote git

The scripts for downloading the tools are in the **Tools** folder in ***yml*** format. New tools can be added by creating new *yml* files with the following format:

- *Rubeus.yml* file:

```yml
tool:
  - name: Rubeus
    description: Rubeus is a C# toolset for raw Kerberos interaction and abuses
    gitLink: https://github.com/GhostPack/Rubeus
    solutionPath: Rubeus\Rubeus.sln
    language: c#
    plugins: RandomGuid, RandomAssemblyInfo, BuildCsharp, ConfuserEx, Donut
    authUser:
    authToken: 
    toolArguments: 
```

Where:

- Name: name of the tool
- Description: tool description
- GitLink: link from git to clone
- SolutionPath: solution (*sln* file) path
- Language: language used (currently only c# is supported)
- Plugins: plugins to use on this tool build process
- AuthUser: user name from github (not used for public repositories)
- AuthToken: auth token from github (not used for public repositories)

## Add a tool from a private git

```yml
tool:
  - name: SharpHound3-Custom
    description: C# Rewrite of the BloodHound Ingestor
    gitLink: https://github.com/aaaaaaa/SharpHound3-Custom
    solutionPath: SharpHound3-Custom\SharpHound3.sln
    language: c#
    plugins: RandomGuid, RandomAssemblyInfo, BuildCsharp, ConfuserEx, Donut
    authUser: aaaaaaa
    authToken: abcdefghijklmnopqrsthtnf
    toolArguments: "-c All,GPOLocalGroup -d whatever.youlike.local"
```

Where:

- Name: name of the tool
- Description: tool description
- GitLink: link from git to clone
- SolutionPath: solution (*sln* file) path
- Language: language used (currently only c# is supported)
- Plugins: plugins to user on this tool build process
- AuthUser: user name from GitHub
- AuthToken: auth token from GitHub (documented at GitHub: [creating a personal access token](https://docs.github.com/en/authentication/keeping-your-account-and-data-secure/creating-a-personal-access-token
))
- toolArguments: arguments to be embedded in the donut shellcode

## Add a tool from local git folder

```yml
tool:
  - name: SeatbeltLocal
    description: Seatbelt is a C# project that performs a number of security oriented host-survey "safety checks" relevant from both offensive and defensive security perspectives.
    gitLink: C:\Users\alpha\Desktop\SeatbeltLocal
    solutionPath: SeatbeltLocal\Seatbelt.sln
    language: c#
    plugins: RandomGuid, RandomAssemblyInfo, BuildCsharp, ConfuserEx, Donut
    authUser:
    authToken: 
    toolArguments:
```

Where:

- Name: name of the tool
- Description: tool description
- GitLink: path where the tool is located
- SolutionPath: solution (*sln* file) path
- Language: language used (currently only c# is supported)
- Plugins: plugins to user on this tool build process
- AuthUser: user name from github (not used for local repositories)
- AuthToken: auth token from github (not used for local repositories)
- toolArguments: arguments to be embedded in the donut shellcode

## Requirements for the release version (Visual Studio 2019/2022 is not required)

- **.NET 10 Runtime**: [https://dotnet.microsoft.com/download/dotnet/10.0](https://dotnet.microsoft.com/download/dotnet/10.0)
    - Not needed if you use the `...-win-x64-self-contained.zip` release archive, which bundles
      the runtime. It is larger, but runs on a host with no .NET installed at all.
- Microsoft .NET Framework 3.5 Service Pack 1 (for some tools): [https://www.microsoft.com/en-us/download/details.aspx?id=22](https://www.microsoft.com/en-us/download/details.aspx?id=22)
- Build Tools for Visual Studio 2022: [https://aka.ms/vs/17/release/vs_BuildTools.exe](https://aka.ms/vs/17/release/vs_BuildTools.exe)
    - Install .NET desktop build tools
    ![](img/2023-01-15-18-17-14.png)
- (Alternative) Build Tools for Visual Studio 2019: [https://aka.ms/vs/16/release/vs_BuildTools.exe](https://aka.ms/vs/16/release/vs_BuildTools.exe)
- Disable the antivirus :D
- Tested on Windows 10 Pro - Version 20H2 - Build 19045.2486

## Configuration

Settings live in **`appsettings.json`**, next to `OffensivePipeline.exe`, under the
`OffensivePipeline` section:

```json
{
  "OffensivePipeline": {
    "NugetUrl": "https://dist.nuget.org/win-x86-commandline/v6.14.0/nuget.exe",
    "NugetSha256": "",
    "BuildCsharpOptions": "/p:LangVersion=latest /p:Platform=\"Any CPU\" /p:Configuration=Release /p:AllowUnsafeBlocks=true",
    "BuildCSharpTools": "C:\\Program Files (x86)\\Microsoft Visual Studio\\2022\\BuildTools\\Common7\\Tools\\VsDevCmd.bat",
    "ConfuserExUrl": "https://github.com/mkaring/ConfuserEx/releases/download/v1.6.0/ConfuserEx-CLI.zip",
    "ConfuserExSha256": "a00de7cddc740f7edb1baab4c6c9073553dcc88f7e873d15b7fd34ddd33753d7"
  }
}
```

| Setting | Meaning |
| --- | --- |
| `NugetUrl` | Where `nuget.exe` is downloaded from when it is not already in `Resources/`. |
| `NugetSha256` | Expected SHA-256 of that download. Empty means *do not verify* (a warning is printed). |
| `BuildCsharpOptions` | MSBuild switches appended to the generated `buildSolution.bat`. |
| `BuildCSharpTools` | Path to `VsDevCmd.bat` from your Build Tools installation. |
| `ConfuserExUrl` | Where the ConfuserEx CLI archive is downloaded from. |
| `ConfuserExSha256` | Expected SHA-256 of that archive. Empty means *do not verify*. |

To switch build tools versions, change `BuildCSharpTools`:

- Build Tools 2019:

```json
"BuildCSharpTools": "C:\\Program Files (x86)\\Microsoft Visual Studio\\2019\\BuildTools\\Common7\\Tools\\VsDevCmd.bat"
```

- Build Tools 2022:

```json
"BuildCSharpTools": "C:\\Program Files (x86)\\Microsoft Visual Studio\\2022\\BuildTools\\Common7\\Tools\\VsDevCmd.bat"
```

Sources are layered, lowest precedence first: the shipped `appsettings.json` defaults, then the
deprecated `OffensivePipeline.dll.config`, then an optional `appsettings.Local.json` in the same
folder. Put machine-specific values in `appsettings.Local.json` so an upgrade does not overwrite
them — that file is gitignored and never shipped.

The legacy file deliberately outranks `appsettings.json`: it only exists if you edited it, and
`appsettings.json` ships with every key populated, so the other order would silently discard
your customisation on upgrade. It is still deprecated and stops being read in v3.1.0.

### Verified downloads

`nuget.exe` and the ConfuserEx CLI are fetched over HTTPS at run time and then executed, so
both are checked against a pinned SHA-256 before use. A file that fails the check is deleted
and the module fails.

If you deliberately point `ConfuserExUrl` (or `NugetUrl`) somewhere else, update the matching
hash, or the download will be rejected:

```powershell
# Download once, hash it, paste the value into appsettings.json
(Get-FileHash -Algorithm SHA256 .\ConfuserEx-CLI.zip).Hash.ToLower()
```

Leaving a hash empty disables verification for that download and prints a warning on every
run. `NugetSha256` ships empty because the pinned `nuget.exe` build has not been recorded
here yet — set it for your environment if you want that download pinned too.

### Migrating from `OffensivePipeline.dll.config`

Versions up to 2.1 kept these settings in `OffensivePipeline.dll.config`. The key names are
unchanged, so migration is a copy across:

| Old — `OffensivePipeline.dll.config` | New — `appsettings.json` |
| --- | --- |
| `<add key="BuildCSharpTools" value="C:\...\VsDevCmd.bat"/>` | `"BuildCSharpTools": "C:\\...\\VsDevCmd.bat"` |
| `<add key="BuildCsharpOptions" value="/p:..."/>` | `"BuildCsharpOptions": "/p:..."` |
| `<add key="NugetUrl" value="https://..."/>` | `"NugetUrl": "https://..."` |
| `<add key="ConfuserExUrl" value="https://..."/>` | `"ConfuserExUrl": "https://..."` |
| `<add key="Version" value="2.0.0"/>` | *removed* — the banner reads the assembly's own version |

Note the JSON escaping: backslashes in Windows paths must be doubled, and quotes inside
`BuildCsharpOptions` must be escaped as `\"`.

**The old file still works in 3.0.0**, at the lowest precedence, and the tool prints:

```
[!] OffensivePipeline.dll.config is deprecated and will be ignored from v2.3.0; migrate to appsettings.json
```

followed by a line for every legacy key whose value `appsettings.json` is overriding. (The
message names `v2.3.0`, the version number this removal was scheduled under before the 3.0
renumbering; it means **the next feature release**.) Migrate now and delete the file.

## Requirements for build

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) — the exact version is
  pinned by `global.json`
- Net framework 3.5.1 (for some target tools): https://www.microsoft.com/en-us/download/details.aspx?id=22
- (Optional) Visual Studio 2022 -> https://visualstudio.microsoft.com/thank-you-downloading-visual-studio/?sku=Community&rel=17
    - Install .NET desktop build tools

The bundled Donut NuGet package (`DonutCore.1.0.1.nupkg`, thanks to @n1xbyte) resolves
automatically from `ExternalResources/` via the repository's `nuget.config` — no manual NuGet
feed configuration is required. See
[`ExternalResources/README.md`](ExternalResources/README.md) for its provenance and checksum.

Build and test from the command line:

```bash
dotnet build OffensivePipeline.sln -c Release
dotnet test --solution OffensivePipeline.sln -c Release
```

Both work on Windows, Linux and macOS, and CI runs them on Windows and Linux. The test suite
needs no network access and no Windows.

Produce a release-shaped build:

```bash
dotnet publish OffensivePipeline/OffensivePipeline.csproj -c Release -r win-x64 --self-contained false -o publish
```

> Note: building/obfuscating the downloaded tools is performed on **Windows** (the
> pipeline shells out to `cmd.exe`, MSBuild Build Tools and the ConfuserEx CLI). The project
> itself compiles and its `list`, `clean` and `--help` verbs run anywhere.

## Verifying releases

Every release ships `SHA256SUMS.txt` and a build-provenance attestation.

```bash
# Checksums (run in the folder containing the zips and SHA256SUMS.txt)
sha256sum -c SHA256SUMS.txt

# Provenance: proves the zip was built by this repository's release workflow
gh attestation verify OffensivePipeline-v3.0.0-win-x64.zip --repo poppopjmp/offensivepipeline
```

Two archives are published per tag:

| Archive | Contents |
| --- | --- |
| `OffensivePipeline-<tag>-win-x64.zip` | Framework-dependent. Needs the .NET 10 runtime. |
| `OffensivePipeline-<tag>-win-x64-self-contained.zip` | Bundles the runtime. For hosts with no .NET installed. |

Both contain all 79 tool templates under `Tools/`, the `Resources/` templates,
`appsettings.json`, `README.md` and `LICENSE`.

## Maintainers

- Original author: [@aetsu](https://github.com/aetsu)
- Current maintainer: [@poppopjmp](https://github.com/poppopjmp) (van1sh)

Contributions are welcome — see [CONTRIBUTING.md](CONTRIBUTING.md).

## Credits

- ConfuserEx project: [https://github.com/mkaring/ConfuserEx](https://github.com/mkaring/ConfuserEx)
- Donut project: [https://github.com/TheWover/donut](https://github.com/TheWover/donut)
- Donut C# generator: [https://github.com/n1xbyte/donutCS](https://github.com/n1xbyte/donutCS)
- SharpCollection: [https://github.com/Flangvik/SharpCollection](SharpCollection)

## Supported tools

- **ADCollector**:  
  - Description: ADCollector is a lightweight tool that enumerates the Active Directory environment to identify possible attack vectors.
  - Link: [https://github.com/dev-2null/ADCollector](https://github.com/dev-2null/ADCollector) 
- **ADCSPwn**:
  - Description: A tool to escalate privileges in an active directory network by coercing authenticate from machine accounts (Petitpotam) and relaying to the certificate service.
  - Link: [https://github.com/bats3c/ADCSPwn](https://github.com/bats3c/ADCSPwn) 
- **ADFSDump**:
  - Description: A C# tool to dump all sorts of goodies from AD FS
  - Link: [https://github.com/mandiant/ADFSDump](https://github.com/mandiant/ADFSDump) 
- **ADSearch**:
  - Description: A tool written for cobalt-strike's execute-assembly command that allows for more efficent querying of AD.
  - Link: [https://github.com/tomcarver16/ADSearch](https://github.com/tomcarver16/ADSearch)
- **BetterSafetyKatz**:
  - Description: This modified fork of SafetyKatz dynamically fetches the latest pre-compiled release of Mimikatz directly from the gentilkiwi GitHub repo, runtime patching on detected signatures and   uses SharpSploit DInvoke to get it into memory.
  - Link: [https://github.com/Flangvik/BetterSafetyKatz](https://github.com/Flangvik/BetterSafetyKatz) 
- **Certify**:  
  - Description: Certify is a C# tool to enumerate and abuse misconfigurations in Active Directory Certificate Services (AD CS).  
  - Link: [https://github.com/GhostPack/Certify](https://github.com/GhostPack/Certify) 
- **DeployPrinterNightmare**:  
  - Description: C# tool for installing a shared network printer abusing the PrinterNightmare bug to allow other network machines easy privesc!  
  - Link: [https://github.com/Flangvik/DeployPrinterNightmare](https://github.com/Flangvik/DeployPrinterNightmare) 
- **EDD**:  
  - Description: Enumerate Domain Data is designed to be similar to PowerView but in .NET. PowerView is essentially the ultimate domain enumeration tool, and we wanted a .NET implementation that we   worked on ourselves. This tool was largely put together by viewing implementations of different functionality across a wide range of existing projects and combining them into EDD.
  - Link: [https://github.com/FortyNorthSecurity/EDD](https://github.com/FortyNorthSecurity/EDD) 
- **ForgeCert**:  
  - Description: C# tool to find vulnerabilities in AD Group Policy, but do it better than Grouper2 did.  
  - Link: [https://github.com/GhostPack/ForgeCert](https://github.com/GhostPack/ForgeCert) 
- **Group3r**:  
  - Description: Rubeus is a C# toolset for raw Kerberos interaction and abuses  
  - Link: [https://github.com/Group3r/Group3r](https://github.com/Group3r/Group3r) 
- **KrbRelay**:  
  - Description: C# Framework for Kerberos relaying  
  - Link: [https://github.com/cube0x0/KrbRelay](https://github.com/cube0x0/KrbRelay) 
- **KrbRelayUp**:  
  - Description: Simple wrapper around some of the features of Rubeus and KrbRelay  
  - Link: [https://github.com/Dec0ne/KrbRelayUp](https://github.com/Dec0ne/KrbRelayUp) 
- **LockLess**:  
  - Description: LockLess is a C# tool that allows for the enumeration of open file handles and the copying of locked files.  
  - Link: [https://github.com/GhostPack/LockLess](https://github.com/GhostPack/LockLess) 
- **PassTheCert**:  
  - Description: A small Proof-of-Concept tool that allows authenticating against an LDAP/S server with a certificate to perform different attack actions  
  - Link: [https://github.com/AlmondOffSec/PassTheCert](https://github.com/AlmondOffSec/PassTheCert) 
- **PurpleSharp**:  
  - Description: PurpleSharp is an open source adversary simulation tool written in C# that executes adversary techniques within Windows Active Directory environments  
  - Link: [https://github.com/mvelazc0/PurpleSharp](https://github.com/mvelazc0/PurpleSharp) 
- **Rubeus**:  
  - Description: Rubeus is a C# toolset for raw Kerberos interaction and abuses  
  - Link: [https://github.com/GhostPack/Rubeus](https://github.com/GhostPack/Rubeus) 
- **SafetyKatz**:  
  - Description: SafetyKatz is a combination of slightly modified version of @gentilkiwi's Mimikatz project and @subtee's .NET PE Loader.  
  - Link: [https://github.com/GhostPack/SafetyKatz](https://github.com/GhostPack/SafetyKatz) 
- **SauronEye**:  
  - Description: SauronEye is a search tool built to aid red teams in finding files containing specific keywords.  
  - Link: [https://github.com/vivami/SauronEye](https://github.com/vivami/SauronEye) 
- **SearchOutlook**:  
  - Description: A C# tool to search through a running instance of Outlook for keywords  
  - Link: [https://github.com/RedLectroid/SearchOutlook](https://github.com/RedLectroid/SearchOutlook) 
- **Seatbelt**:  
  - Description: Seatbelt is a C# project that performs a number of security oriented host-survey "safety checks" relevant from both offensive and defensive security perspectives.  
  - Link: [https://github.com/GhostPack/Seatbelt](https://github.com/GhostPack/Seatbelt) 
- Sharp-**SMBExec**:  
  - Description: A native C# conversion of Kevin Robertsons Invoke-SMBExec powershell script  
  - Link: [https://github.com/checkymander/Sharp-SMBExec](https://github.com/checkymander/Sharp-SMBExec) 
- **SharpAppLocker**:  
  - Description: C# port of the Get-AppLockerPolicy PowerShell cmdlet with extended features.  
  - Link: [https://github.com/Flangvik/SharpAppLocker](https://github.com/Flangvik/SharpAppLocker) 
- **SharpBypassUAC**:  
  - Description: C# tool for UAC bypasses  
  - Link: [https://github.com/FatRodzianko/SharpBypassUAC](https://github.com/FatRodzianko/SharpBypassUAC) 
- **SharpChisel**:  
  - Description: C# Wrapper of Chisel from https://github.com/jpillora/chisel  
  - Link: [https://github.com/shantanu561993/SharpChisel](https://github.com/shantanu561993/SharpChisel) 
- **SharpChromium**:  
  - Description: SharpChromium is a .NET 4.0+ CLR project to retrieve data from Google Chrome, Microsoft Edge, and Microsoft Edge Beta. Currently, it can extract  
  - Link: [https://github.com/djhohnstein/SharpChromium](https://github.com/djhohnstein/SharpChromium) 
- **SharpCloud**:  
  - Description: SharpCloud is a simple C# utility for checking for the existence of credential files related to Amazon Web Services, Microsoft Azure, and Google Compute.  
  - Link: [https://github.com/chrismaddalena/SharpCloud](https://github.com/chrismaddalena/SharpCloud) 
- **SharpCOM**:  
  - Description: SharpCOM is a c# port of Invoke-DCOM  
  - Link: [https://github.com/rvrsh3ll/SharpCOM](https://github.com/rvrsh3ll/SharpCOM) 
- **SharpCookieMonster**:  
  - Description: This is a Sharp port of @defaultnamehere's cookie-crimes module - full credit for their awesome work!  
  - Link: [https://github.com/m0rv4i/SharpCookieMonster](https://github.com/m0rv4i/SharpCookieMonster) 
- **SharpCrashEventLog**:  
  - Description: Crashes the Windows eventlog service locally or remotely using OpenEventLogA/ElfClearEventLogFileW.  
  - Link: [https://github.com/slyd0g/SharpCrashEventLog](https://github.com/slyd0g/SharpCrashEventLog) 
- **SharpDir**:  
  - Description: SharpDir is a simple code set to search both local and remote file systems for files using the same SMB process as dir.exe, which uses TCP port 445  
  - Link: [https://github.com/jnqpblc/SharpDir](https://github.com/jnqpblc/SharpDir) 
- **SharpDPAPI**:  
  - Description: SharpDPAPI is a C# port of some DPAPI functionality from @gentilkiwi's Mimikatz project.  
  - Link: [https://github.com/GhostPack/SharpDPAPI](https://github.com/GhostPack/SharpDPAPI) 
- **SharpDump**:  
  - Description: SharpDump is a C# port of PowerSploit's Out-Minidump.ps1 functionality  
  - Link: [https://github.com/GhostPack/SharpDump](https://github.com/GhostPack/SharpDump) 
- **SharpEDRChecker**:  
  - Description: Checks running processes, process metadata, Dlls loaded into your current process and each DLLs metadata, common install directories, installed services and each service binaries   metadata, installed drivers and each drivers metadata, all for the presence of known defensive products such as AV's, EDR's and logging tools.
  - Link: [https://github.com/PwnDexter/SharpEDRChecker](https://github.com/PwnDexter/SharpEDRChecker) 
- **SharPersist**:  
  - Description: Windows persistence toolkit written in C#  
  - Link: [https://github.com/mandiant/SharPersist](https://github.com/mandiant/SharPersist) 
- **SharpExec**:  
  - Description: SharpExec is an offensive security C# tool designed to aid with lateral movement.  
  - Link: [https://github.com/anthemtotheego/SharpExec](https://github.com/anthemtotheego/SharpExec) 
- **SharpGPOAbuse**:  
  - Description: SharpGPOAbuse is a .NET application written in C# that can be used to take advantage of a user's edit rights on a Group Policy Object (GPO) in order to compromise the objects that are   controlled by that GPO.
  - Link: [https://github.com/FSecureLABS/SharpGPOAbuse](https://github.com/FSecureLABS/SharpGPOAbuse) 
- **SharpHandler**:  
  - Description: This project reuses open handles to lsass to parse or minidump lsass, therefore you don't need to use your own lsass handle to interact with it. (Dinvoke-version)  
  - Link: [https://github.com/jfmaes/SharpHandler](https://github.com/jfmaes/SharpHandler) 
- **SharpHose**:  
  - Description: SharpHose is a C# password spraying tool designed to be fast, safe, and usable over Cobalt Strike's execute-assembly.  
  - Link: [https://github.com/ustayready/SharpHose](https://github.com/ustayready/SharpHose) 
- **SharpHound3**:  
  - Description: C# Rewrite of the BloodHound Ingestor  
  - Link: [https://github.com/BloodHoundAD/SharpHound3](https://github.com/BloodHoundAD/SharpHound3) 
- **SharpKatz**:  
  - Description: Porting of mimikatz sekurlsa::logonpasswords, sekurlsa::ekeys and lsadump::dcsync commands  
  - Link: [https://github.com/b4rtik/SharpKatz](https://github.com/b4rtik/SharpKatz) 
- **SharpLAPS**:  
  - Description: This executable is made to be executed within Cobalt Strike session using execute-assembly. It will retrieve the LAPS password from the Active Directory.  
  - Link: [https://github.com/swisskyrepo/SharpLAPS](https://github.com/swisskyrepo/SharpLAPS) 
- **SharpMapExec**:  
  - Description: Sharpen version of CrackMapExec  
  - Link: [https://github.com/cube0x0/SharpMapExec](https://github.com/cube0x0/SharpMapExec) 
- **SharpMiniDump**:  
  - Description: Create a minidump of the LSASS process from memory (Windows 10 - Windows Server 2016). The entire process uses dynamic API calls, direct syscall and Native API unhooking to evade the   AV / EDR detection.
  - Link: [https://github.com/b4rtik/SharpMiniDump](https://github.com/b4rtik/SharpMiniDump) 
- **SharpMove**:  
  - Description: .NET authenticated execution for remote hosts  
  - Link: [https://github.com/0xthirteen/SharpMove](https://github.com/0xthirteen/SharpMove) 
- **SharpNamedPipePTH**:  
  - Description: This project is a C# tool to use Pass-the-Hash for authentication on a local Named Pipe for user Impersonation. You need a local administrator or SEImpersonate rights to use this.  
  - Link: [https://github.com/S3cur3Th1sSh1t/SharpNamedPipePTH](https://github.com/S3cur3Th1sSh1t/SharpNamedPipePTH) 
- **SharpNoPSExec**:  
  - Description: File less command execution for lateral movement.  
  - Link: [https://github.com/juliourena/SharpNoPSExec](https://github.com/juliourena/SharpNoPSExec) 
- **SharpPrinter**:  
  - Description: Printer is a modified and console version of ListNetworks  
  - Link: [https://github.com/rvrsh3ll/SharpPrinter](https://github.com/rvrsh3ll/SharpPrinter) 
- **SharpRDP**:  
  - Description: Remote Desktop Protocol Console Application for Authenticated Command Execution  
  - Link: [https://github.com/0xthirteen/SharpRDP](https://github.com/0xthirteen/SharpRDP) 
- **SharpReg**:  
  - Description: SharpReg is a simple code set to interact with the Remote Registry service API using the same SMB process as reg.exe, which uses TCP port 445  
  - Link: [https://github.com/jnqpblc/SharpReg](https://github.com/jnqpblc/SharpReg) 
- **SharpSCCM**:  
  - Description: SharpSCCM is a post-exploitation tool designed to leverage Microsoft Endpoint Configuration Manager (a.k.a. ConfigMgr, formerly SCCM) for lateral movement and credential gathering   without requiring access to the SCCM administration console GUI.
  - Link: [https://github.com/Mayyhem/SharpSCCM](https://github.com/Mayyhem/SharpSCCM) 
- **SharpScribbles**:  
  - Description: Extracts data from the Windows Sticky Notes database. Works on Windows 10 Build 1607 and higher. This  
  - Link: [https://github.com/V1V1/SharpScribbles](https://github.com/V1V1/SharpScribbles) 
- **SharpSearch**:  
  - Description: Project to quickly filter through a file share for targeted files for desired information.  
  - Link: [https://github.com/djhohnstein/SharpSearch](https://github.com/djhohnstein/SharpSearch) 
- **SharpSecDump**:  
  - Description: .Net port of the remote SAM + LSA Secrets dumping functionality of impacket's secretsdump.py  
  - Link: [https://github.com/G0ldenGunSec/SharpSecDump](https://github.com/G0ldenGunSec/SharpSecDump) 
- **SharpShares**:  
  - Description: Quick and dirty binary to list network share information from all machines in the current domain and if they're readable.  
  - Link: [https://github.com/djhohnstein/SharpShares](https://github.com/djhohnstein/SharpShares) 
- **SharpSniper**:  
  - Description: SharpSniper is a simple tool to find the IP address of these users so that you can target their box.  
  - Link: [https://github.com/HunnicCyber/SharpSniper](https://github.com/HunnicCyber/SharpSniper) 
- **SharpSphere**:  
  - Description: SharpSphere gives red teamers the ability to easily interact with the guest operating systems of virtual machines managed by vCenter  
  - Link: [https://github.com/JamesCooteUK/SharpSphere](https://github.com/JamesCooteUK/SharpSphere) 
- **SharpSpray**:  
  - Description: SharpSpray a simple code set to perform a password spraying attack against all users of a domain using LDAP and is compatible with Cobalt Strike.  
  - Link: [https://github.com/jnqpblc/SharpSpray](https://github.com/jnqpblc/SharpSpray) 
- **SharpSQLPwn**:  
  - Description: C# tool to identify and exploit weaknesses with MSSQL instances in Active Directory environments  
  - Link: [https://github.com/lefayjey/SharpSQLPwn](https://github.com/lefayjey/SharpSQLPwn) 
- **SharpStay**:  
  - Description: .NET Persistence  
  - Link: [https://github.com/0xthirteen/SharpStay](https://github.com/0xthirteen/SharpStay) 
- **SharpSvc**:  
  - Description: SharpSvc is a simple code set to interact with the SC Manager API using the same DCERPC process as sc.exe, which open with TCP port 135 and is followed by the use of an ephemeral TCP   port
  - Link: [https://github.com/jnqpblc/SharpSvc](https://github.com/jnqpblc/SharpSvc) 
- **SharpTask**:  
  - Description: SharpTask is a simple code set to interact with the Task Scheduler service API using the same DCERPC process as schtasks.exe, which open with TCP port 135 and is followed by the use of   an ephemeral TCP port.
  - Link: [https://github.com/jnqpblc/SharpTask](https://github.com/jnqpblc/SharpTask) 
- **SharpUp**:  
  - Description: SharpUp is a C# port of various PowerUp functionality  
  - Link: [https://github.com/GhostPack/SharpUp](https://github.com/GhostPack/SharpUp) 
- **SharpView**:  
  - Description: .NET port of PowerView  
  - Link: [https://github.com/tevora-threat/SharpView](https://github.com/tevora-threat/SharpView) 
- **SharpWebServer**:  
  - Description: Red Team oriented simple HTTP & WebDAV server written in C# with functionality to capture Net-NTLM hashes  
  - Link: [https://github.com/mgeeky/SharpWebServer](https://github.com/mgeeky/SharpWebServer) 
- **SharpWifiGrabber**:  
  - Description: Retrieves in clear-text the Wi-Fi Passwords from all WLAN Profiles saved on a workstation  
  - Link: [https://github.com/r3nhat/SharpWifiGrabber](https://github.com/r3nhat/SharpWifiGrabber) 
- **SharpWMI**:  
  - Description: SharpWMI is a C# implementation of various WMI functionality.  
  - Link: [https://github.com/GhostPack/SharpWMI](https://github.com/GhostPack/SharpWMI) 
- **SharpZeroLogon**:  
  - Description: An exploit for CVE-2020-1472, a.k.a. Zerologon. This tool exploits a cryptographic vulnerability in Netlogon to achieve authentication bypass.  
  - Link: [https://github.com/nccgroup/nccfsas](https://github.com/nccgroup/nccfsas) 
- **Shhmon**:  
  - Description: While Sysmon's driver can be renamed at installation, it is always loaded at altitude 385201. The objective of this tool is to challenge the assumption that our defensive tools are   always collecting events.
  - Link: [https://github.com/matterpreter/Shhmon](https://github.com/matterpreter/Shhmon) 
- **Snaffler**:  
  - Description: Snaffler is a tool for pentesters and red teamers to help find delicious candy needles (creds mostly, but it's flexible) in a bunch of horrible boring haystacks (a massive Windows/AD   environment).
  - Link: [https://github.com/SnaffCon/Snaffler](https://github.com/SnaffCon/Snaffler) 
- **SqlClient**:  
  - Description: C# .NET mssql client for accessing database data through beacon.  
  - Link: [https://github.com/FortyNorthSecurity/SqlClient](https://github.com/FortyNorthSecurity/SqlClient) 
- **StandIn**:  
  - Description: StandIn is a small AD post-compromise toolkit  
  - Link: [https://github.com/FuzzySecurity/StandIn](https://github.com/FuzzySecurity/StandIn) 
- **SweetPotato**:  
  - Description: A collection of various native Windows privilege escalation techniques from service accounts to SYSTEM  
  - Link: [https://github.com/CCob/SweetPotato](https://github.com/CCob/SweetPotato) 
- **ThreatCheck**:  
  - Description: Modified version of Matterpreter's DefenderCheck  
  - Link: [https://github.com/rasta-mouse/ThreatCheck](https://github.com/rasta-mouse/ThreatCheck) 
- **TokenStomp**:  
  - Description: C# POC for the token privilege removal flaw reported  
  - Link: [https://github.com/MartinIngesen/TokenStomp](https://github.com/MartinIngesen/TokenStomp) 
- **TruffleSnout**:  
  - Description: Iterative AD discovery toolkit for offensive operators  
  - Link: [https://github.com/dsnezhkov/TruffleSnout](https://github.com/dsnezhkov/TruffleSnout) 
- **Watson**:  
  - Description: Watson is a .NET tool designed to enumerate missing KBs and suggest exploits for Privilege Escalation vulnerabilities.  
  - Link: [https://github.com/rasta-mouse/Watson](https://github.com/rasta-mouse/Watson) 
- **Whisker**:  
  - Description: Whisker is a C# tool for taking over Active Directory user and computer accounts by manipulating their msDS-KeyCredentialLink attribute, effectively adding "Shadow Credentials" to the   target account.
  - Link: [https://github.com/eladshamir/Whisker](https://github.com/eladshamir/Whisker) 
- **winPEAS**:  
  - Description: Privilege Escalation Awesome Scripts SUITE  
  - Link: [https://github.com/carlospolop/privilege-escalation-awesome-scripts-suite](https://github.com/carlospolop/privilege-escalation-awesome-scripts-suite) 
- **WMIReg**:  
  - Description: Whisker is a C# tool for taking over Active Directory user and computer accounts by manipulating their msDS-KeyCredentialLink attribute.  
  - Link: [https://github.com/airzero24/WMIReg](https://github.com/airzero24/WMIReg) 
